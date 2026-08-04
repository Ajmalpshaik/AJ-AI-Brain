#!/usr/bin/env node
// PostToolUse hook wrapper around verify-consistency.mjs (wired up in .claude/settings.json).
//
// WHY THIS EXISTS ALONGSIDE verify-consistency-hook.ps1:
// The .ps1 wrapper hardcodes `powershell`, so on any session without Windows PowerShell — Claude Code
// on the web, a Linux/macOS container, a cloud runner — the hook silently never fires at all. It does
// not warn; it just doesn't run. A whole session of edits on 2026-08-04 went through with zero
// automatic checking for exactly that reason, and the drift it would have caught was found only
// because the checker happened to be run by hand.
//
// Node is the safer dependency of the two: this repo already requires it for the MCP relay
// (SETUP.md step 2), and it exists on Windows, Linux and macOS alike, whereas `powershell` is Windows
// only. So the hook now runs the portable checker, and the .ps1 pair stays for manual use.
//
// Behaviour matches the .ps1 wrapper exactly: quiet exit 0 when the Brain is clean; on drift, exit 2
// with the checker's report on stderr, so the AI session sees it immediately and fixes the drift in
// the same turn. Exit 2 is the ONLY PostToolUse exit code whose stderr is fed back to the model — the
// checker's own exit 1 would reach the user only.

import { spawnSync } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const checker = path.join(here, "verify-consistency.mjs");

// process.execPath, not "node": this resolves to the very interpreter already running, so the inner
// call works even when node isn't on PATH under whatever shell the hook was launched from.
const result = spawnSync(process.execPath, [checker], { encoding: "utf8" });

if (result.error) {
  console.error(`Brain consistency check could not run (tools/verify-consistency.mjs): ${result.error.message}`);
  process.exit(2);
}

if (result.status !== 0) {
  const report = `${result.stdout || ""}${result.stderr || ""}`;
  console.error(`Brain consistency drift detected (tools/verify-consistency.mjs):\n${report}`);
  process.exit(2);
}

process.exit(0);
