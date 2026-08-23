#!/usr/bin/env node
// One Stop hook that runs the other six, so the turn ends faster.
//
// WHY: five separate entries in .claude/settings.json meant five Node processes started one after
// another at the end of EVERY turn. Measured 2026-08-21:
//
//   reindex-run     335 ms      score-check     379 ms      test-row-nudge  375 ms
//   fragment-nudge  373 ms      graph-rebuild   643 ms      -> 2,105 ms, sequential
//
// Most of that is Node starting up, five times, to do a few milliseconds of real work each. They
// cannot simply be imported into one process - all five call process.exit(), so the first one to
// finish would take the rest down with it - but they CAN run at the same time.
//
// ORDER IS NOT FREE, and one dependency is real: score-check.mjs re-scores retrieval, which reads
// the vector index, and reindex-run.mjs is what rebuilds that index. Scoring a half-rebuilt index
// produces a number that means nothing and would trip the regression alarm for no reason. So
// reindex runs first, alone, and the other four run together once it is done.
//
//   335 + max(379, 375, 373, 643)  =  ~978 ms   instead of 2,105 ms
//
// SAFETY. Replacing five hook entries with one means a bug here silently disables all five - and
// silently-not-running is this repo's worst failure mode, the reason score-check.mjs exists at all.
// So: every child is isolated, a child that crashes or times out is REPORTED rather than swallowed,
// and this always exits 0, because a Stop hook must never block the turn from ending.
//
//   node tools/stop-hooks.mjs            run them as the hook does
//   node tools/stop-hooks.mjs --timing   also print what each one cost

import path from "node:path";
import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const showTiming = process.argv.includes("--timing");
const CHILD_TIMEOUT = 120000;

// Read once: a Stop hook is handed the same JSON on stdin that the individual hooks expect, and
// several of them read it. Collect it here and pass it on to each child unchanged.
async function readStdin() {
  try {
    const chunks = [];
    for await (const chunk of process.stdin) chunks.push(chunk);
    return Buffer.concat(chunks).toString("utf8");
  } catch {
    return "";
  }
}

function run(script, stdinText) {
  return new Promise((resolve) => {
    const started = Date.now();
    const child = spawn(process.execPath, [path.join(here, script)], {
      cwd: path.dirname(here),
      stdio: ["pipe", "pipe", "pipe"],
    });

    let out = "", err = "", settled = false;
    const finish = (status) => {
      if (settled) return;
      settled = true;
      resolve({ script, status, out, err, ms: Date.now() - started });
    };

    const timer = setTimeout(() => { child.kill(); finish("timeout"); }, CHILD_TIMEOUT);
    child.stdout.on("data", (d) => { out += d; });
    child.stderr.on("data", (d) => { err += d; });
    child.on("error", () => { clearTimeout(timer); finish("spawn-failed"); });
    child.on("close", (code) => { clearTimeout(timer); finish(code); });

    try {
      child.stdin.end(stdinText);
    } catch {
      // a child that closed stdin early is not a failure
    }
  });
}

function report(results) {
  for (const r of results) {
    // Children speak to the session through stdout; pass it straight through so combining them
    // changes nothing about what anyone sees.
    if (r.out && r.out.trim()) process.stdout.write(r.out.endsWith("\n") ? r.out : r.out + "\n");
    if (r.status !== 0 && r.status !== null) {
      process.stderr.write(`[stop-hooks] ${r.script} exited ${r.status}` +
        (r.err.trim() ? `: ${r.err.trim().split("\n")[0]}` : "") + "\n");
    }
  }
  if (showTiming) {
    for (const r of results) process.stderr.write(`  ${String(r.ms).padStart(5)} ms  ${r.script}\n`);
  }
}

const stdinText = await readStdin();

// Phase 1, alone: rebuilding the index must finish before anything scores it.
const first = await run("reindex-run.mjs", stdinText);

// Phase 2, together: none of these four writes anything the others read.
const rest = await Promise.all(
  ["score-check.mjs", "test-row-nudge.mjs", "fragment-nudge.mjs", "graph-rebuild.mjs"]
    .map((s) => run(s, stdinText)),
);

// Phase 3, alone: the Obsidian vault is rendered FROM graph.json, which graph-rebuild.mjs above is what
// writes - so this cannot join phase 2 or it would export a half-written graph. Added 2026-08-22, when
// the vault was found 2 days and 89 files stale while the other two derived layers were current: it was
// the only one whose documented rebuild was a slash command, so it needed a human to remember.
// It is stamp-guarded and costs a stat call when the graph has not moved, so this phase is usually free.
const vault = await run("obsidian-export.mjs", stdinText);

report([first, ...rest, vault]);
process.exit(0);
