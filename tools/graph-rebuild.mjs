#!/usr/bin/env node
// Stop hook - rebuild the knowledge graph's code side when any code file has changed.
//
// WHY: the semantic index has rebuilt itself since Phase 1, but the knowledge graph never
// did. So `search_graph` could answer confidently from a graph built days earlier and nothing
// anywhere said so - the exact silent-staleness problem that was fixed for the vector index
// and left live for the graph. This closes it.
//
// WHY IT DOES NOT JUST RUN `graphify update .`: that command goes extract -> build -> write in
// one step, with nowhere to insert tools/graphify-repair.py, so it would silently undo the fix
// that took 203 dangling edges to 0. tools/graph-rebuild.py runs the same pipeline with the
// repair in the middle.
//
// WHY IT IS GATED ON CODE, NOT ON ANY EDIT: a full rebuild is ~12s, against ~0.3s for the
// index. Running that after every turn would tax editing a knowledge note with the cost of
// re-parsing 328 source files. Only a code change can alter the AST half, so only a code
// change triggers it. Markdown changes need graphify's LLM pass, which cannot be automatic at
// all - graph-rebuild.py REPORTS that staleness instead, which is better than the nothing
// there was before.
//
// Always exits 0. A stale graph is worth a warning, never a blocked turn.

import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.join(here, "..");
const semanticRoot = path.join(root, "semantic-index");
const fingerprintFile = path.join(semanticRoot, "run-temp", ".graph-fingerprint");

const CODE_EXT = new Set([".cs", ".js", ".mjs", ".py"]);
const SKIP = new Set([".git", "graphify-out", "node_modules", "venv", "model-cache",
                      "chroma-db", "pip-cache", "pip-temp", "run-temp", ".voice-runtime"]);

function walk(dir, out = []) {
  let entries;
  try {
    entries = fs.readdirSync(dir, { withFileTypes: true });
  } catch {
    return out;
  }
  for (const e of entries) {
    if (SKIP.has(e.name)) continue;
    const p = path.join(dir, e.name);
    if (e.isDirectory()) walk(p, out);
    else if (CODE_EXT.has(path.extname(e.name))) out.push(p);
  }
  return out;
}

function fingerprint() {
  const h = crypto.createHash("sha256");
  for (const f of walk(root).sort()) {
    h.update(path.relative(root, f));
    try {
      // Normalise line endings: git rewrites LF to CRLF on checkout here, and a fingerprint
      // that changes on checkout would rebuild the graph for no reason on every branch switch.
      h.update(fs.readFileSync(f, "utf8").replace(/\r\n/g, "\n"));
    } catch {
      h.update("UNREADABLE");
    }
  }
  return h.digest("hex");
}

// The graphify library is NOT in semantic-index/venv - it is installed in whatever interpreter
// `graphify` was installed into (uv tool, pipx, or system), and graphify itself records that
// path in .graphify_python. Reaching for the venv first was the first version of this hook and
// it failed with ModuleNotFoundError: No module named 'graphify'. The venv stays as a fallback
// only because a machine that installed graphify INTO it would otherwise be unreachable.
function graphifyPython() {
  const recorded = path.join(root, "graphify-out", ".graphify_python");
  try {
    const p = fs.readFileSync(recorded, "utf8").trim();
    if (p && fs.existsSync(p)) return p;
  } catch {
    /* fall through */
  }
  return [
    path.join(semanticRoot, "venv", "Scripts", "python.exe"),
    path.join(semanticRoot, "venv", "bin", "python"),
  ].find((p) => fs.existsSync(p));
}

try {
  if (!fs.existsSync(path.join(root, "graphify-out", "graph.json"))) process.exit(0);

  const force = process.argv.includes("--force");
  const current = fingerprint();
  let stored = "";
  try {
    stored = fs.readFileSync(fingerprintFile, "utf8").trim();
  } catch {
    /* first run */
  }
  if (!force && stored === current) process.exit(0);

  const python = graphifyPython();
  if (!python) process.exit(0);

  const result = spawnSync(python, [path.join(root, "tools", "graph-rebuild.py")], {
    encoding: "utf8",
    cwd: root,
    timeout: 300000,
  });

  // Record the fingerprint either way, so a broken rebuild does not retry every single turn.
  try {
    fs.mkdirSync(path.dirname(fingerprintFile), { recursive: true });
    fs.writeFileSync(fingerprintFile, current, "utf8");
  } catch {
    /* not worth reporting */
  }

  const out = `${result.stdout || ""}${result.stderr || ""}`;
  if (result.error || result.status !== 0) {
    console.error(`Knowledge graph rebuild failed:\n${out.trim().slice(0, 600)}`);
    process.exit(0);
  }

  // Surface only the two lines that carry information: what the graph now is, and whether the
  // markdown half has fallen behind. The rest is progress noise.
  for (const line of out.split("\n")) {
    if (/graph rebuilt:|MARKDOWN STALE/.test(line)) console.error(line.trim());
  }
} catch {
  // A maintenance hook must never disturb the work it maintains.
}

process.exit(0);
