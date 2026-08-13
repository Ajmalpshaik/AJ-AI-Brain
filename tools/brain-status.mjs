#!/usr/bin/env node
// One honest answer to "what is the state of this Brain right now?"
//
// WHY THIS EXISTS: every problem found on 2026-08-04 was the same shape - the Brain's description of
// itself had drifted ahead of what it had actually proven. README said 8 skills against 9 on disk,
// AGENT-SPEC said 206 fragments against 264, and 147 fragments had no verification status at all, so
// they read as fine. An agent has no reason to doubt its own documentation, which is exactly what makes
// that kind of drift expensive.
//
// So this reports state COMPUTED FROM DISK, never from a checked-in summary. Nothing here is written
// down anywhere it could go stale - same reasoning .gitignore already applies to graphify-out/ ("a
// stale index in this repo is worse than no index").
//
// Stance is warn-and-keep-working, by the user's decision (2026-08-04): it reports what is unproven and
// gets out of the way. It never blocks, and it is never a reason not to do the work.
//
// Usage:
//   node tools/brain-status.mjs                 compact - what the session-start hook prints
//   node tools/brain-status.mjs --full          adds per-bucket detail and the open-items text
//   node tools/brain-status.mjs --capabilities  what this Brain can actually do, grouped by job
//   node tools/brain-status.mjs --json          machine-readable

import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const brainRoot = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
const args = new Set(process.argv.slice(2));

function walk(dir, filterFn, results = []) {
  if (!fs.existsSync(dir)) return results;
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) walk(full, filterFn, results);
    else if (filterFn(entry.name)) results.push(full);
  }
  return results;
}

function read(rel) {
  const p = path.join(brainRoot, rel);
  return fs.existsSync(p) ? fs.readFileSync(p, "utf8") : "";
}

// --- skills -----------------------------------------------------------------
const skillsDir = path.join(brainRoot, "skills");
const skills = fs.existsSync(skillsDir)
  ? fs.readdirSync(skillsDir, { withFileTypes: true })
      .filter((e) => e.isDirectory() && fs.existsSync(path.join(skillsDir, e.name, "SKILL.md")))
      .map((e) => {
        const fm = fs.readFileSync(path.join(skillsDir, e.name, "SKILL.md"), "utf8");
        const desc = fm.match(/^description:\s*([\s\S]*?)(?:\r?\n[a-z_]+:|\r?\n---)/m);
        // First sentence only - the frontmatter descriptions are deliberately long for trigger accuracy.
        // Mask the abbreviations that would otherwise end the sentence early ("e.g." cut one summary
        // off mid-clause), split, then restore.
        const flat = (desc ? desc[1] : "").replace(/\s+/g, " ")
          .replace(/\be\.g\./g, "e<DOT>g<DOT>").replace(/\bi\.e\./g, "i<DOT>e<DOT>");
        const first = flat.split(/(?<=\.)\s/)[0].replace(/<DOT>/g, ".").trim();
        return { name: e.name, summary: first };
      })
  : [];

// --- script fragments -------------------------------------------------------
const BUCKETS = ["filters", "actions", "recipes", "creators", "commands", "examples", "context", "lib"];
const scriptsDir = path.join(brainRoot, "scripts");
const byBucket = {};
let fragmentTotal = 0;
for (const b of BUCKETS) {
  byBucket[b] = walk(path.join(scriptsDir, b), (n) => n.endsWith(".cs")).length;
  fragmentTotal += byBucket[b];
}

// --- how much of the library has actually been run against a real model ------
// Read off scripts/README.md's own per-fragment markers - the same convention the library already
// maintains by hand. A row with no marker either way is the important category: never run, and never
// flagged as never-run, so it silently reads as fine.
const scriptsReadme = read("scripts/README.md");
const rows = scriptsReadme.split(/\r?\n/).filter((l) => l.startsWith("| [`"));
// `verified 2026-08-07` is the house wording, but rows written in a hurry say "verified live 2026-08-14"
// or "re-verified 2026-08-06" and a strict /verified 2026/ silently dropped those into "no status" — the
// exact undercount this tool exists to prevent, in the tool itself (found 2026-08-14: 9 rows, all really
// verified, all counted as unproven). Allow one adverb between the word and the date.
const verified = rows.filter(
  (l) => /verified(?:\s+\w+)? 2026-\d\d-\d\d/.test(l) && !/NOT yet live-verified/.test(l),
);
const flagged = rows.filter((l) => /NOT yet live-verified/.test(l));
const blocked = rows.filter(
  (l) => /BLOCKED|CONFIRMED IMPOSSIBLE/.test(l) && !verified.includes(l) && !flagged.includes(l),
);
const noStatus = rows.filter(
  (l) => !verified.includes(l) && !flagged.includes(l) && !blocked.includes(l),
);

// --- native MCP tools -------------------------------------------------------
const nativeTools = walk(path.join(brainRoot, "mcp-server", "tools"), (n) => n.endsWith(".js")).length;

// --- knowledge files, and any that outgrew the repo's own splitting rule -----
// knowledge/INDEX.md: "If a file grows past ~300 lines, split it and update the relevant index."
const knowledgeFiles = walk(path.join(brainRoot, "knowledge"), (n) => n.endsWith(".md"));
// A file can opt out by recording WHY it was kept whole, in a "split-review: kept whole" marker. The
// brain-self-maintain skill calls the 300-line rule "a split candidate, not a mandate - if it's one
// coherent job read as a unit, splitting adds hops and makes things worse; say so and leave it." The
// marker is that "say so", made durable: the decision stays visible in the file instead of being
// re-argued every time someone notices the line count, and the report stops crying wolf.
const measured = knowledgeFiles.map((f) => {
  const text = fs.readFileSync(f, "utf8");
  return {
    rel: path.relative(brainRoot, f).split(path.sep).join("/"),
    lines: text.split(/\r?\n/).length,
    keptWhole: /split-review:\s*kept whole/i.test(text),
  };
});
// brain-log.md is an append-only record, not a routed topic file - the rule was never meant for it.
const overLimit = measured.filter((f) => f.lines > 300 && !f.rel.endsWith("brain-log.md"));
const oversized = overLimit.filter((f) => !f.keptWhole);
const keptWhole = overLimit.filter((f) => f.keptWhole);

// --- open items -------------------------------------------------------------
const log = read("knowledge/brain-log.md");
const openSection = log.split(/^## Open items/m)[1]?.split(/^## /m)[0] ?? "";
const openGroups = [];
for (const m of openSection.matchAll(/^\*\*(.+?)[:*]{0,2}\*\*(.*)$/gm)) {
  const title = m[1].replace(/\*+$/, "").trim();
  const after = openSection.slice(m.index + m[0].length).split(/^\*\*/m)[0];
  const count = (after.match(/^\d+\.\s/gm) || []).length;
  const isNone = /none right now/i.test(m[2] || "");
  // "CONFIRMED IMPOSSIBLE" is an explicitly closed category ("answered, not outstanding") - listing it
  // as an open item at 0 is exactly the kind of noise that made the old flat list read as unfinished.
  if (/CONFIRMED IMPOSSIBLE/i.test(title)) continue;
  openGroups.push({ title, count: isNone ? 0 : count });
}

// --- consistency ------------------------------------------------------------
const checker = path.join(brainRoot, "tools", "verify-consistency.mjs");
const check = spawnSync(process.execPath, [checker], { encoding: "utf8" });
const consistencyClean = check.status === 0;
const consistencyIssues = consistencyClean
  ? []
  : (check.stdout || "").split(/\r?\n/).filter((l) => l.trim().startsWith("- "));

// --- output -----------------------------------------------------------------
const pct = (n) => (rows.length ? Math.round((n / rows.length) * 100) : 0);

if (args.has("--json")) {
  console.log(JSON.stringify({
    skills: skills.length,
    fragments: { total: fragmentTotal, byBucket },
    nativeTools,
    proven: {
      verified: verified.length, flaggedUntested: flagged.length,
      blocked: blocked.length, noStatus: noStatus.length, rows: rows.length,
    },
    knowledge: { files: knowledgeFiles.length, oversized, keptWhole },
    openItems: openGroups,
    consistencyClean,
  }, null, 2));
  process.exit(0);
}

if (args.has("--capabilities")) {
  console.log("AJ AI Brain — what this can actually do (generated from disk, never stored)\n");
  console.log("Skills — a whole job, start to finish:");
  for (const s of skills) console.log(`  ${s.name}\n    ${s.summary}`);
  console.log("\nScript fragments — composed per request as filter + action:");
  for (const b of BUCKETS) {
    const subs = fs.existsSync(path.join(scriptsDir, b))
      ? fs.readdirSync(path.join(scriptsDir, b), { withFileTypes: true }).filter((e) => e.isDirectory())
      : [];
    if (subs.length) {
      const detail = subs
        .map((s) => `${s.name} ${walk(path.join(scriptsDir, b, s.name), (n) => n.endsWith(".cs")).length}`)
        .join(" · ");
      console.log(`  ${b.padEnd(9)} ${String(byBucket[b]).padStart(3)}   ${detail}`);
    } else {
      console.log(`  ${b.padEnd(9)} ${String(byBucket[b]).padStart(3)}`);
    }
  }
  console.log(`\n  ${nativeTools} native MCP tools (no code generation step — prefer these when one fits)`);
  console.log("\nRouting: START-HERE.md picks the skill; scripts/README.md picks the fragment.");
  console.log("Route by request SHAPE, not by element noun — fragments are generic, never named 'duct'.");
  process.exit(0);
}

const bucketLine = BUCKETS.map((b) => `${b} ${byBucket[b]}`).join(" · ");
console.log(
  `AJ AI Brain — ${skills.length} skills · ${fragmentTotal} fragments · ${nativeTools} native tools · ` +
  `consistency ${consistencyClean ? "clean" : `${consistencyIssues.length} ISSUE(S)`}`,
);

if (!consistencyClean) {
  console.log("\nDrift found — fix before finishing this turn:");
  for (const i of consistencyIssues.slice(0, 10)) console.log(`  ${i.trim()}`);
}

console.log("\nProven against a real Revit model:");
console.log(`  ${String(verified.length).padStart(3)} verified            (${pct(verified.length)}%)`);
console.log(`  ${String(flagged.length).padStart(3)} flagged untested    (${pct(flagged.length)}%)`);
console.log(`  ${String(blocked.length).padStart(3)} blocked/impossible  (${pct(blocked.length)}%)`);
console.log(`  ${String(noStatus.length).padStart(3)} no status either way (${pct(noStatus.length)}%)  <- unproven, and not flagged as such`);
console.log("  Not a blocker: run one element first, check the result, then trust it for a batch.");

if (openGroups.length) {
  console.log("\nOpen items:");
  for (const g of openGroups) console.log(`  ${String(g.count).padStart(2)}  ${g.title}`);
}

if (oversized.length) {
  console.log("\nPast the ~300-line split rule (knowledge/INDEX.md) — not yet reviewed:");
  for (const f of oversized) console.log(`  ${f.rel} (${f.lines} lines)`);
}
if (keptWhole.length && args.has("--full")) {
  console.log("\nOver 300 lines but reviewed and deliberately kept whole:");
  for (const f of keptWhole) console.log(`  ${f.rel} (${f.lines} lines) — reason in the file's split-review marker`);
}

if (args.has("--full")) {
  console.log(`\nFragments by bucket:\n  ${bucketLine}`);
  console.log("\nSkills:");
  for (const s of skills) console.log(`  ${s.name}`);
  if (openSection.trim()) {
    console.log("\nOpen items, in full:\n");
    console.log(openSection.trim().split(/\r?\n/).map((l) => `  ${l}`).join("\n"));
  }
} else {
  console.log("\n  node tools/brain-status.mjs --full | --capabilities | --json");
}

process.exit(0);
