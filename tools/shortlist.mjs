#!/usr/bin/env node
// The fragments Ajmal ACTUALLY uses, ranked, put in front of the assistant on every message.
//
// WHY THIS EXISTS. His idea, 2026-08-22, in his own words: *"if i use more that tool it will
// automaticly change that to native tools like that praoity based."* The instinct is right and the
// mechanism he pictured is not: registering and unregistering MCP tools by usage would churn the tool
// list, break every skill note that names a tool, and only take effect on a restart.
//
// The thing a native tool actually buys, now that run_fragment exists, is NOT saved code - it is that
// the assistant HAS it without searching. Measured: the search puts the right answer at #1 on 5 of 28
// real questions (semantic-index/score-history.md). So the fix for "I use this every day and it still
// gets lost" is to stop it being searched for at all.
//
// This does that for ALL 311 fragments instead of the ~8 that could ever be hand-promoted, updates on
// every single message rather than every restart, and cannot break a documented tool name because it
// registers nothing.
//
// THE DECISION ORDER IT SERVES (AGENT-SPEC 2.4, and his own words):
//   1. native tool or this shortlist   - already in hand, no search
//   2. a proven fragment               - search for it, run it with run_fragment
//   3. new C#                          - only when nothing above covers the job
//
// RANKING. Runs in the last 30 days, because "as per my usage" means lately, not ever. If that window
// is empty - a holiday, a new machine - it falls back to all-time and SAYS so, rather than printing
// nothing and looking broken.
//
// COST. Reading and counting one .jsonl, in-process. It must never spawn a child: the fragment index
// was 426 ms as a spawn and 38 ms as a read (knowledge/brain-log.md, 2026-08-21), and this runs on
// every message.
//
//   node tools/shortlist.mjs              the full list, as a person reads it
//   node tools/shortlist.mjs --block      the compact block the hook injects
//   node tools/shortlist.mjs --days 90    a different window

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const brainRoot = path.join(here, "..");
const RUNS = path.join(brainRoot, "job-log", "revit-runs.jsonl");

export const DEFAULT_DAYS = 30;
export const DEFAULT_LIMIT = 8;

// `date` is written as YYYY-MM-DD, so string comparison is date comparison. No Date parsing, no
// timezone to get wrong.
function cutoffDate(days, today = new Date()) {
  const d = new Date(today.getTime() - days * 86400000);
  return d.toISOString().slice(0, 10);
}

// Returns {ranked, window, total, days} — never throws, and returns an empty list rather than an
// error when there is no log at all, which is the normal case on a fresh checkout.
export function rankFragments({ days = DEFAULT_DAYS, limit = DEFAULT_LIMIT, today, file = RUNS } = {}) {
  const empty = { ranked: [], window: "none", total: 0, days };
  let lines;
  try {
    if (!fs.existsSync(file)) return empty;
    lines = fs.readFileSync(file, "utf8").split("\n");
  } catch {
    return empty;
  }

  const cutoff = cutoffDate(days, today ? new Date(today) : undefined);
  const recent = new Map();
  const allTime = new Map();
  let total = 0;

  for (const line of lines) {
    if (!line.trim()) continue;
    let rec;
    try {
      rec = JSON.parse(line);
    } catch {
      continue; // a half-written last line must not lose the whole log
    }
    const frags = Array.isArray(rec.fragments) ? rec.fragments : [];
    if (!frags.length) continue;
    total++;
    const isRecent = typeof rec.date === "string" && rec.date >= cutoff;
    for (const f of frags) {
      if (typeof f !== "string" || !f.trim()) continue;
      allTime.set(f, (allTime.get(f) || 0) + 1);
      if (isRecent) recent.set(f, (recent.get(f) || 0) + 1);
    }
  }

  // Lately is what he asked for. All-time only when lately is empty, and it says which it used.
  const useRecent = recent.size > 0;
  const source = useRecent ? recent : allTime;
  if (source.size === 0) return empty;

  const ranked = [...source.entries()]
    // Count first; then all-time as the tiebreaker, so two fragments used twice this week are
    // ordered by which one has carried more work overall. Name last, so the order is stable
    // across runs instead of shuffling on every message.
    .sort((a, b) => b[1] - a[1] || (allTime.get(b[0]) || 0) - (allTime.get(a[0]) || 0) || a[0].localeCompare(b[0]))
    .slice(0, limit)
    .map(([name, count]) => ({ name, count, allTime: allTime.get(name) || 0 }));

  return { ranked, window: useRecent ? "recent" : "all-time", total, days };
}

// The compact block injected into every message. Kept to four lines on purpose: it is paid for on
// EVERY message, which is the same reason the search hook emits a compact block and not full results.
export function shortlistBlock(opts = {}) {
  const { ranked, window, days } = rankFragments(opts);
  if (!ranked.length) return "";

  const when = window === "recent" ? `last ${days} days` : "all time — nothing in the recent window";
  const items = ranked.map((r) => `${r.count}x ${r.name}`).join(" · ");
  return [
    `YOUR MOST-USED FRAGMENTS (${when}, from job-log) — CHECK HERE BEFORE SEARCHING:`,
    `  ${items}`,
    `  Run one with run_fragment. Not here? Then search the Brain. Still nothing? Only then write new C#.`,
  ].join("\n");
}

// --- CLI ---------------------------------------------------------------------------------------
if (import.meta.url === `file://${process.argv[1]}`) {
  const argv = process.argv.slice(2);
  const valueOf = (n, d) => {
    const i = argv.indexOf(n);
    return i >= 0 && argv[i + 1] ? Number(argv[i + 1]) : d;
  };
  const days = valueOf("--days", DEFAULT_DAYS);
  const limit = valueOf("--limit", DEFAULT_LIMIT);

  if (argv.includes("--block")) {
    const b = shortlistBlock({ days, limit });
    if (b) console.log(b);
  } else {
    const { ranked, window, total } = rankFragments({ days, limit });
    if (!ranked.length) {
      console.log("No fragment runs recorded yet.\n");
      console.log("This file is written on your PC as you work, and never travels in git, so a fresh");
      console.log("checkout always shows this. It fills from the next Revit job you run.");
      console.log(`\n  looking in: job-log/revit-runs.jsonl`);
    } else {
      const when = window === "recent" ? `last ${days} days` : "ALL TIME (nothing in the recent window)";
      console.log(`Most-used fragments — ${when}, from ${total} recorded Revit job(s)\n`);
      const width = Math.max(...ranked.map((r) => r.name.length));
      const top = ranked[0].count;
      for (const r of ranked) {
        const bar = "█".repeat(Math.max(1, Math.round((r.count / top) * 28)));
        console.log(`  ${r.name.padEnd(width)}  ${String(r.count).padStart(4)}  ${bar}`);
      }
      console.log(`\n  These are what a native tool would be worth building for — evidence, not memory.`);
    }
  }
}
