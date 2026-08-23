// Re-true every live count claim from tools/verify-consistency.mjs's own drift report.
//
// WHY THIS EXISTS: hand-patching the ~18 lines that state a fragment/skill/file count loses to a
// second Claude session adding a fragment mid-edit — measured 2026-08-23, when the count moved
// 314 -> 315 -> 316 -> 317 while one turn was fixing it. The checker already knows the true number
// and which line is wrong; this just applies what it says, so it can be re-run until it converges.
//
// It changes ONLY the number, ONLY on the line the checker named, so a dated historical figure
// elsewhere in the same file is never touched.
//
// TWO TRAPS BUILT INTO THIS FILE, both of which produced a silent no-op the first time:
//   1. `\b` inside a JS TEMPLATE LITERAL is the BACKSPACE character, not a word boundary. Building
//      a RegExp from `` `\b${n}\b` `` compiles happily and matches nothing. Use '\\b' in a normal
//      string, which is what this file does now.
//   2. A /g RegExp carries `lastIndex` BETWEEN CALLS, so `re.test(s)` followed by `s.replace(re,…)`
//      skips every other match. Replace first, then compare, and never share a /g regex across the two.
//
// Usage:  node tools/sync-counts.mjs        (safe to run repeatedly; says so when there is nothing to do)

import { execFileSync } from 'node:child_process';
import fs from 'node:fs';

let out = '';
try { out = execFileSync('node', ['tools/verify-consistency.mjs'], { encoding: 'utf8' }); }
catch (e) { out = (e.stdout || '') + (e.stderr || ''); }   // it exits non-zero when it finds drift

const edits = [];
for (const raw of out.split('\n')) {
  let m = raw.match(/LIVE COUNT DRIFT in (.+?):(\d+): says "[^"]*?(\d+)[^"]*?" but disk has (\d+)/);
  if (m) { edits.push({ file: m[1], line: +m[2], from: m[3], to: m[4] }); continue; }
  m = raw.match(/INDEX COUNT DRIFT in (.+?): says "searches all (\d+) files" but the indexable set on disk holds (\d+)/);
  if (m) { edits.push({ file: m[1], from: m[2], to: m[3], ctx: 'searches all ' }); continue; }
  m = raw.match(/AGENT-SPEC COUNT DRIFT \(§3\.5\): claims (\d+) (\w+), disk has (\d+)/);
  if (m) { edits.push({ file: 'AGENT-SPEC.md', from: m[1], to: m[3], word: m[2] }); }
}
if (!edits.length) { console.log('nothing to sync — counts already true'); process.exit(0); }

const byFile = new Map();
for (const e of edits) { if (!byFile.has(e.file)) byFile.set(e.file, []); byFile.get(e.file).push(e); }

let changed = 0;
for (const [file, list] of byFile) {
  const buf = fs.readFileSync(file);
  // BOM preserved byte-for-byte: stripping it from a .ps1 makes PowerShell 5.1 read the file as ANSI,
  // and one em dash then opens an unterminated string. That has broken two scripts in this repo.
  const bom = buf.length >= 3 && buf[0] === 0xef && buf[1] === 0xbb && buf[2] === 0xbf;
  const lines = (bom ? buf.subarray(3) : buf).toString('utf8').split('\n');

  for (const e of list) {
    if (e.line) {                                     // a numbered line the checker pointed at
      const i = e.line - 1;
      if (i >= lines.length) { console.log(`  skip ${file}:${e.line} — past end of file`); continue; }
      const after = lines[i].replace(new RegExp('\\b' + e.from + '\\b', 'g'), e.to);
      if (after !== lines[i]) { lines[i] = after; changed++; }
    } else if (e.ctx) {                               // "searches all N files"
      const needle = e.ctx + e.from + ' files';
      for (let i = 0; i < lines.length; i++)
        if (lines[i].includes(needle)) { lines[i] = lines[i].split(needle).join(e.ctx + e.to + ' files'); changed++; }
    } else {                                          // AGENT-SPEC §3.5 breakdown, e.g. "166 actions"
      // The §3.5 sentence WRAPS, so "167 actions" is routinely split as "…, 167\nactions, 34 …".
      // A per-line includes() can never match it — that bug made this branch a silent no-op.
      // Work on the joined text and allow any whitespace, newline included, between number and word.
      const joined = lines.join('\n');
      const re = e.word === 'total'                   // "total" is the sum, never written as that word
        ? new RegExp('\\b' + e.from + '(\\s+real C# fragments)')
        : new RegExp('\\b' + e.from + '(\\s+' + e.word + '\\b)');
      const after = joined.replace(re, e.to + '$1');
      if (after !== joined) { lines.length = 0; lines.push(...after.split('\n')); changed++; }
    }
  }

  const body = Buffer.from(lines.join('\n'), 'utf8');
  fs.writeFileSync(file, bom ? Buffer.concat([Buffer.from([0xef, 0xbb, 0xbf]), body]) : body);
}
console.log(`synced ${changed} count claim(s) across ${byFile.size} file(s)`);
