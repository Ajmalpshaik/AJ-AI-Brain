#!/usr/bin/env node
// PostToolUse hook - mark the semantic index as needing a rebuild.
//
// This does NOT rebuild. It writes a zero-byte flag and exits, because PostToolUse fires
// once per file edit while a rebuild costs seconds: editing eight files in one turn would
// mean eight rebuilds of an index that is only read at the end anyway. tools/reindex-run.mjs
// does the actual rebuild once, on Stop.
//
// It deliberately does not parse the hook's stdin to check WHICH file was edited. Over-marking
// is cheap and correct - a rebuild with nothing changed re-embeds nothing and costs ~2 s - while
// mis-parsing an undocumented payload would silently stop marking altogether, which is the exact
// failure this whole pair exists to remove. Cheap-and-always-right beats clever-and-sometimes-off.
//
// WHY THIS EXISTS AT ALL: the index is a snapshot. Nothing used to refresh it, so any session
// that edited the Brain and forgot `index-brain.cmd` left every later session searching an older
// copy - answering confidently out of text that no longer existed.
//
// Must never fail an edit. Every path exits 0.

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

try {
  const here = path.dirname(fileURLToPath(import.meta.url));
  const flagDir = path.join(here, "..", "semantic-index", "run-temp");
  fs.mkdirSync(flagDir, { recursive: true });
  fs.writeFileSync(path.join(flagDir, ".reindex-needed"), "");
} catch {
  // A missing flag costs one stale search and a STALE INDEX warning the user will see.
  // A thrown hook costs the user their edit. Stay silent and let the edit through.
}

process.exit(0);
