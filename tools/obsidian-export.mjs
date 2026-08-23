#!/usr/bin/env node
// Stop-hook step: keep the Obsidian vault in step with the knowledge graph, automatically.
//
// WHY THIS EXISTS. Of the Brain's three derived layers, two already rebuilt themselves at the end of
// every turn - the vector index (reindex-run.mjs) and the knowledge graph (graph-rebuild.mjs) - and the
// Obsidian vault did not. Its documented rebuild was `/graphify . --update`, a SLASH COMMAND, so it
// needed a human to remember and an AI session to run. Predictably it drifted: measured 2026-08-22 it
// was 2 days and 89 source files behind while the other two were current, and Ajmal's question that day
// was exactly "is that automatic or another session we need to do that?".
//
// WHAT MADE IT AUTOMATABLE. `graphify export obsidian` is a plain CLI command - it re-renders the vault
// from `graphify-out/graph.json` and needs no model call at all. The slash command was only ever the
// wrapper. Timed on this Brain: 5 s for ~2,000 notes.
//
// WHY IT IS GUARDED BY A STAMP. 5 s on EVERY turn would be paid by every reply, including the many that
// change nothing a reader would see. The vault is a pure function of graph.json, so this exports only
// when graph.json has actually moved since the last export. Unchanged, this costs a stat call.
//
// ORDER MATTERS: it reads graph.json, and graph-rebuild.mjs is what writes it. So this runs in its own
// phase AFTER that one, never beside it - see the phase comment in stop-hooks.mjs.
//
// SAFETY: a Stop hook must never block a turn from ending, and silently-not-running is this repo's worst
// failure mode. So this always exits 0, reports trouble on stderr rather than swallowing it, and treats
// a missing `graphify` binary as "not set up on this machine" rather than an error.
//
//   node tools/obsidian-export.mjs           export only if the graph moved
//   node tools/obsidian-export.mjs --force   export regardless
//   node tools/obsidian-export.mjs --check   say what it WOULD do, change nothing

import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, "..");
const graphJson = path.join(root, "graphify-out", "graph.json");
const stampFile = path.join(root, "graphify-out", ".obsidian-export-stamp");
const vaultDir = path.join(root, "graphify-out", "obsidian");

const force = process.argv.includes("--force");
const checkOnly = process.argv.includes("--check");

function note(msg) { process.stderr.write(`[obsidian-export] ${msg}\n`); }

// The graph is the input; if it has not moved, the vault cannot be out of date.
// mtime+size rather than a hash: this runs on every turn and a 1.6 MB hash is not worth the read.
function graphSignature() {
  try {
    const s = fs.statSync(graphJson);
    return `${Math.floor(s.mtimeMs)}:${s.size}`;
  } catch {
    return null;
  }
}

const sig = graphSignature();
if (sig === null) {
  // No graph in this checkout - graphify-out/ is gitignored, so this is normal on a fresh clone.
  if (checkOnly) note("no graphify-out/graph.json in this checkout - nothing to export");
  process.exit(0);
}

let previous = null;
try { previous = fs.readFileSync(stampFile, "utf8").trim(); } catch { }

if (!force && previous === sig) {
  if (checkOnly) note("vault is already in step with the graph - nothing to do");
  process.exit(0);
}

if (checkOnly) {
  note(previous === null
    ? "would export: no stamp yet, so the vault has never been exported by this hook"
    : "would export: graph.json has changed since the last export");
  process.exit(0);
}

// `graphify` is installed per-user, not part of this repo. Its absence means this machine has not been
// set up for the graph layer at all - which is a fine state to be in, and not this hook's problem.
const probe = spawnSync(process.platform === "win32" ? "where" : "which", ["graphify"],
  { encoding: "utf8", windowsHide: true });
if (probe.status !== 0) {
  note("graphify is not on PATH - skipping (the vault is optional; see semantic-index/README.md)");
  process.exit(0);
}

const started = Date.now();
const result = spawnSync("graphify", ["export", "obsidian"], {
  cwd: root,
  encoding: "utf8",
  timeout: 180000,
  windowsHide: true,
  shell: process.platform === "win32",   // graphify is a .exe shim on Windows
});
const ms = Date.now() - started;

if (result.error || result.status !== 0) {
  // Report and move on. A failed vault export must not cost the user their turn, and the vault is the
  // one layer nothing else depends on.
  note(`export failed after ${ms} ms` +
    (result.error ? `: ${result.error.message}` : ` (exit ${result.status})`));
  const tail = (result.stderr || result.stdout || "").trim().split("\n").pop();
  if (tail) note(`  ${tail}`);
  process.exit(0);
}

// Stamp only on success, so a failed export retries next turn instead of being marked done.
try { fs.writeFileSync(stampFile, sig, "utf8"); } catch { }

let count = 0;
try {
  const walk = (d) => {
    for (const e of fs.readdirSync(d, { withFileTypes: true })) {
      if (e.isDirectory()) walk(path.join(d, e.name));
      else count++;
    }
  };
  walk(vaultDir);
} catch { }

note(`vault re-exported in ${ms} ms` + (count ? ` - ${count} notes` : ""));

// graphify refuses to overwrite files it did not create. That is the right call, but it means a stray
// file in the vault folder silently shrinks the vault, so say it rather than let it pass.
const skipped = (result.stdout || "").match(/skipped (\d+) pre-existing file/);
if (skipped) note(`  note: ${skipped[1]} pre-existing file(s) were left alone, so those notes are not regenerated`);

process.exit(0);
