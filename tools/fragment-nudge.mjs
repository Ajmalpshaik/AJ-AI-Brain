#!/usr/bin/env node
// Stop hook - say out loud when fresh C# was written instead of composed from the 269
// existing fragments, and when nothing from a session was saved back.
//
// WHY THIS EXISTS: on 2026-08-13, during one real working session, 13 run_csharp calls went
// to the model and NOT ONE used a saved fragment. Every one was written from scratch, and
// none was saved back afterwards. Both halves of the Brain's core promise were skipped at
// once - "search the 269 first" (CLAUDE.md) and "save what surfaced" (START-HERE.md) - and
// Ajmal is the one who noticed, by asking whether it worked that way at all.
//
// job-log/revit-runs.jsonl had recorded every one of those 13 calls the whole time. What was
// missing was anything that made the gap VISIBLE. That is now the fourth time today the same
// lesson has landed, after reindex-run, score-check and test-row-nudge:
//
//     a step that depends on remembering is a step that gets skipped.
//
// Detection is honest rather than clever: composed C# keeps its "// FRAGMENT (kind) - name.cs"
// headers, so a run_csharp call with NO fragment names is code that came from nowhere. That is
// not automatically wrong - a genuine one-off is fine, and most are - but 13 in a row is not
// one-offs, it is a habit that has quietly stopped.
//
// Reports, never blocks and never writes. Whether a script deserves saving is a judgement
// call, and the Script Writer agent or Ajmal makes it, not a hook.
//
//   node tools/fragment-nudge.mjs           today's ad-hoc runs
//   node tools/fragment-nudge.mjs --all     every day in the log
//
// Always exits 0.

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const logFile = path.join(here, "..", "job-log", "revit-runs.jsonl");

// Below this, a script is a throwaway one-liner and not worth a fragment.
const WORTH_NOTICING_LINES = 12;

try {
  if (!fs.existsSync(logFile)) process.exit(0);

  const all = fs
    .readFileSync(logFile, "utf8")
    .split("\n")
    .filter((l) => l.trim())
    .map((l) => {
      try {
        return JSON.parse(l);
      } catch {
        return null;
      }
    })
    .filter(Boolean);

  const today = new Date().toISOString().slice(0, 10);
  const scope = process.argv.includes("--all") ? all : all.filter((r) => r.date === today);

  const adhoc = scope.filter(
    (r) => r.tool === "run_csharp" && !(r.fragments || []).length && (r.codeLines || 0) >= WORTH_NOTICING_LINES
  );
  const composed = scope.filter((r) => (r.fragments || []).length).length;

  if (adhoc.length < 3) process.exit(0); // one or two one-offs is normal, not a habit

  const lines = adhoc.map((r) => r.codeLines).sort((a, b) => b - a);
  console.error(
    `\n${adhoc.length} script(s) written from scratch against the model` +
      (composed ? `, ${composed} composed from saved fragments.` : `, none composed from saved fragments.`) +
      `\n   sizes: ${lines.slice(0, 8).join(", ")} lines` +
      `\n   Two questions worth answering before this session ends:` +
      `\n     1. Did an existing fragment already cover any of these?  node tools/fragment-index.mjs --find <word>` +
      `\n     2. Is any of them worth SAVING as a new fragment for next time?\n`
  );
} catch {
  // A nudge that fails must never disturb the work it is nudging about.
}

process.exit(0);
