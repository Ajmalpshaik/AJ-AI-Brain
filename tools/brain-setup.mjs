#!/usr/bin/env node
// First-run setup for a machine that has the Brain's FILES but none of its MACHINERY.
//
// WHY THIS EXISTS: installing this repo as a Claude Code plugin brings the skills, the knowledge,
// the fragments and the MCP relay's source - and none of the derived layers, because they are
// gitignored on purpose (a stale index is worse than no index, see .gitignore). Before this script,
// someone who installed the plugin got a Brain whose search was dead and whose bridge would not
// start, with no single command to fix it and no message saying which piece was missing. The setup
// steps existed only as prose in SETUP.md, which is exactly the kind of instruction that gets half
// followed.
//
// It is SAFE TO RE-RUN. Every step checks whether it is already done and skips it. Nothing here
// touches Revit, and nothing reaches outside this folder except the two package installs (npm, pip)
// and the embedding model the index fetches on its first build.
//
//   node tools/brain-setup.mjs            do the missing steps
//   node tools/brain-setup.mjs --check    report what is missing, change nothing
//
// It deliberately does NOT build the knowledge graph or the Obsidian vault. Those need an AI
// session rather than a script - see skills/brain-update-layers/SKILL.md.

import fs from "node:fs";
import path from "node:path";
import { spawn, spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const root = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
const win = process.platform === "win32";
const checkOnly = process.argv.includes("--check");

const semantic = path.join(root, "semantic-index");
const venvPy = win
  ? path.join(semantic, "venv", "Scripts", "python.exe")
  : path.join(semantic, "venv", "bin", "python");
const nodeModules = path.join(root, "mcp-server", "node_modules");
const chromaDir = path.join(semantic, "chroma-db");

const missing = [];
if (!fs.existsSync(nodeModules)) missing.push("relay dependencies");
if (!fs.existsSync(venvPy)) missing.push("search environment");
if (!fs.existsSync(chromaDir)) missing.push("search index");

// --- keeping the search current after a plugin UPDATE -----------------------------------------
// WHY THIS IS HERE AND NOT SOMEWHERE CLEVERER: when this Brain is installed as a plugin, an update
// replaces the FILES on disk and nothing else. New fragments arrive, but the search index still
// describes yesterday's library, so the newest work is the one thing that cannot be found - the
// exact silent-staleness fault the whole manifest mechanism exists to prevent, arriving through a
// door nobody was watching. There is no post-update hook to hang this on, and the plugin ships no
// Stop hook (a plugin user is not editing the Brain, so the in-session reindex never fires either).
// SessionStart is the first moment after an update that anything of ours runs at all.
//
// It compares file mtimes against the index's own stamped build time - cheap, a few hundred stat
// calls - and when the files are ahead it starts the incremental rebuild DETACHED and returns
// immediately. A hook must never make someone wait, and this one would otherwise cost 3-20 s at the
// start of every session that followed an update.
function indexIsBehind() {
  let builtMs;
  try {
    const m = JSON.parse(fs.readFileSync(path.join(semantic, "index-manifest.json"), "utf8"));
    builtMs = Date.parse(m.built_at);
    if (!Number.isFinite(builtMs)) return false;   // built before the stamp existed - leave it alone
  } catch {
    return false;                                   // no manifest: that is "missing", handled above
  }
  // The set the indexer actually covers, mirroring semantic-index/brain_common.py.
  const SKIP = new Set([".git", "node_modules", "venv", "graphify-out", "chroma-db", "model-cache",
                        "pip-cache", "pip-temp", "run-temp", ".voice-runtime", "__pycache__"]);
  // Machine-written every session; judging by these would report stale forever.
  const CHURN = new Set(["brain-log.md", "score-history.md"]);
  const stack = ["scripts", "knowledge", "skills"].map((d) => path.join(root, d));
  for (const rel of ["AGENT-SPEC.md", "START-HERE.md", "README.md", "SETUP.md", "CLAUDE.md"]) {
    const p = path.join(root, rel);
    try { if (fs.statSync(p).mtimeMs > builtMs) return true; } catch { /* absent is fine */ }
  }
  while (stack.length) {
    let entries;
    try { entries = fs.readdirSync(stack.pop(), { withFileTypes: true }); } catch { continue; }
    for (const e of entries) {
      if (SKIP.has(e.name)) continue;
      const p = path.join(e.parentPath || e.path, e.name);
      if (e.isDirectory()) { stack.push(p); continue; }
      if (!/\.(md|cs)$/.test(e.name) || CHURN.has(e.name)) continue;
      try { if (fs.statSync(p).mtimeMs > builtMs) return true; } catch { /* raced, ignore */ }
    }
  }
  return false;
}

// One rebuild at a time. Several sessions opened at once would otherwise each start their own, and
// concurrent builds against one database is exactly what wedged this machine on 2026-08-22.
function claimRebuildLock() {
  const lock = path.join(semantic, "run-temp", ".reindex-lock");
  try {
    fs.mkdirSync(path.dirname(lock), { recursive: true });
    const age = Date.now() - fs.statSync(lock).mtimeMs;
    if (age < 10 * 60 * 1000) return false;         // someone else is on it, or just finished
  } catch { /* no lock yet */ }
  try { fs.writeFileSync(lock, String(process.pid)); return true; } catch { return false; }
}

if (missing.length === 0) {
  if (indexIsBehind() && claimRebuildLock()) {
    // Detached, output discarded, unref'd: this process exits now and the rebuild finishes on its
    // own. By the next message the search knows about the new files.
    try {
      const child = spawn(venvPy, ["brain_index.py"],
        { cwd: semantic, detached: true, stdio: "ignore", windowsHide: true });
      child.unref();
      console.log("AJ AI Brain: files are newer than the search index - rebuilding it in the");
      console.log("background now. Search stays usable throughout and picks up the new files");
      console.log("within a minute. Nothing for you to do.");
    } catch { /* never block a session over this */ }
    process.exit(0);
  }
  // SILENT under --check when there is nothing to do. --check runs on every SessionStart as a
  // plugin hook, and a line that prints "all good" every single session is a line people stop
  // reading - which is precisely when it stops working as a warning. Speak only when it matters.
  if (!checkOnly) {
    console.log("Brain setup: already complete - relay dependencies, search environment and");
    console.log("search index are all present.");
    console.log("  Verify with:  node tools/brain-status.mjs");
  }
  process.exit(0);
}

if (checkOnly) {
  console.log("AJ AI Brain is NOT SET UP on this machine yet - missing: " + missing.join(", "));
  console.log("Skills, knowledge and script fragments all work, but plain-English search does not,");
  console.log("and the Revit bridge will not start until this is built. It is one command:");
  console.log("");
  console.log("    /brain-setup");
  console.log("");
  console.log("A few minutes, safe to re-run, needs internet once.");
  process.exit(0);
}

console.log("Brain setup - missing: " + missing.join(", "));

// A step that must succeed. Streams its output so a slow install does not look like a hang, and
// stops the whole run on failure - a half-built environment that reports success is the failure
// mode this whole file exists to avoid.
function run(label, cmd, args, opts = {}) {
  console.log("");
  console.log("=== " + label + " ===");
  const r = spawnSync(cmd, args, { stdio: "inherit", shell: win, ...opts });
  if (r.error || r.status !== 0) {
    console.error("");
    console.error("FAILED: " + label);
    console.error("  command: " + cmd + " " + args.join(" "));
    if (r.error) console.error("  " + r.error.message);
    console.error("");
    console.error("Nothing after this step ran. Fix the above and run this again -");
    console.error("it resumes from where it stopped.");
    process.exit(1);
  }
}

// Find a usable Python. Reports every candidate it tried rather than a bare "not found", because
// "install Python" is useless advice to someone who already has it under another name.
function findPython() {
  const tried = [];
  for (const [cmd, args] of [["python", []], ["python3", []], ["py", ["-3"]]]) {
    tried.push(cmd + (args.length ? " " + args.join(" ") : ""));
    const probe = [...args, "-c", "import sys; print('%d.%d' % sys.version_info[:2])"];
    const r = spawnSync(cmd, probe, { encoding: "utf8", shell: win });
    if (!r.error && r.status === 0) return { cmd, args, version: (r.stdout || "").trim() };
  }
  console.error("");
  console.error("FAILED: no Python found. Tried: " + tried.join(", "));
  console.error("Install Python 3.11 or newer, make sure it is on PATH, then run this again.");
  process.exit(1);
}

if (!fs.existsSync(nodeModules)) {
  run("Relay dependencies (npm install)", "npm", ["install"],
      { cwd: path.join(root, "mcp-server") });
}

if (!fs.existsSync(venvPy)) {
  const py = findPython();
  console.log("");
  console.log("Using Python " + py.version + " (" + py.cmd + ")");
  run("Search environment (create venv)", py.cmd, [...py.args, "-m", "venv", "venv"],
      { cwd: semantic });

  // pip's temp and cache are forced inside semantic-index/. On a company-managed PC, unpacking
  // package DLLs into %TEMP% gets them scanned or quarantined by antivirus - the reason is recorded
  // at the top of semantic-index/requirements.txt, and it has bitten this repo before.
  const pipTemp = path.join(semantic, "pip-temp");
  const pipCache = path.join(semantic, "pip-cache");
  fs.mkdirSync(pipTemp, { recursive: true });
  fs.mkdirSync(pipCache, { recursive: true });
  run("Search dependencies (pip install)", venvPy,
      ["-m", "pip", "install", "-r", "requirements.txt"],
      { cwd: semantic, env: { ...process.env, TEMP: pipTemp, TMP: pipTemp,
                              TMPDIR: pipTemp, PIP_CACHE_DIR: pipCache } });
}

if (!fs.existsSync(chromaDir)) {
  console.log("");
  console.log("The next step downloads the embedding model on its first run (internet needed once)");
  console.log("and then embeds every file. Expect a few minutes.");
  run("Search index (full build)", venvPy, ["brain_index.py", "--full"], { cwd: semantic });
}

console.log("");
console.log("=== Setup complete ===");
console.log("Now built:");
console.log("  relay dependencies   mcp-server/node_modules");
console.log("  search environment   semantic-index/venv");
console.log("  search index         semantic-index/chroma-db");
console.log("");
console.log("Still to do, and neither is something a script can do:");
console.log("  1. The knowledge graph and the Obsidian vault - see");
console.log("     skills/brain-update-layers/SKILL.md (needs an AI session, not a script).");
console.log("  2. A Revit-side bridge listener must be running before anything can touch a model.");
console.log("     See SETUP.md section 3, and prove it with the ping in section 4.");
console.log("");
console.log("Check the result:  node tools/brain-status.mjs");
