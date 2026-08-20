#!/usr/bin/env node
// Stop hook - rebuild the semantic index if anything was edited this turn.
//
// Paired with tools/reindex-mark.mjs, which sets the flag on every edit. Doing the rebuild
// here means one rebuild per turn instead of one per edited file.
//
// NEVER call semantic-index/index-brain.cmd from here. That wrapper ends with `pause`, which
// blocks forever waiting for a keypress - it would hang the session, not just the hook. Call
// brain_index.py through the venv Python directly. Same rule applies to score-brain.cmd.
//
// Timing, measured 2026-08-13: ~2 s when nothing changed, ~12 s after editing
// knowledge/brain-log.md (206 of the index's 2,984 chunks live in that one file). The
// semantic-index README's "2.8 s" figure is for an ordinary small file.
//
// Exits 0 in every case. A failed rebuild is worth a warning, never a blocked turn - and the
// search prints its own STALE INDEX banner if this did not run, so the failure is not silent.

import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const semanticRoot = path.join(here, "..", "semantic-index");
const flag = path.join(semanticRoot, "run-temp", ".reindex-needed");

if (!fs.existsSync(flag)) process.exit(0);

// Windows puts the venv interpreter in Scripts/, every other platform in bin/. Checking both
// keeps this working on Claude Code for web and any Linux/macOS container - the portability
// lesson already paid for by verify-consistency-hook.ps1 silently never firing off-Windows.
const candidates = [
  path.join(semanticRoot, "venv", "Scripts", "python.exe"),
  path.join(semanticRoot, "venv", "bin", "python"),
];
const python = candidates.find((p) => fs.existsSync(p));

if (!python) {
  console.error(
    `Semantic index not rebuilt: no venv Python found at\n  ${candidates.join("\n  ")}\n` +
      `Searches will warn STALE INDEX until semantic-index\\index-brain.cmd is run by hand.`
  );
  process.exit(0);
}

const result = spawnSync(python, [path.join(semanticRoot, "brain_index.py")], {
  encoding: "utf8",
  cwd: semanticRoot,
  windowsHide: true, // else Windows 11 flashes a Terminal window over Revit on every rebuild
});

if (result.error || result.status !== 0) {
  const detail = result.error
    ? result.error.message
    : `${result.stdout || ""}${result.stderr || ""}`.trim();
  console.error(`Semantic index rebuild failed:\n${detail}`);
  process.exit(0); // warn, never block the turn
}

try {
  fs.unlinkSync(flag);
} catch {
  // Flag already gone. Worst case the next turn does a ~2 s no-op rebuild.
}

process.exit(0);
