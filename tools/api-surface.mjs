#!/usr/bin/env node
// Regenerate knowledge/revit-api-surface.md from the fragments themselves.
//
// WHY THIS IS GENERATED, NOT WRITTEN: the file answers "which Revit API does this Brain actually use",
// and the only honest source for that is scripts/. Hand-editing it would make it drift from the library
// the moment a fragment is added or retired — the exact failure tools/brain-status.mjs exists to stop.
//
// Reads only. Writes exactly one file.

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const SCRIPTS = path.join(ROOT, "scripts");
const OUT = path.join(ROOT, "knowledge", "revit-api-surface.md");

// Types that are .NET/BCL or noise words the regex picks up, not Revit API surface.
const NOISE = new Set(["String","Math","Convert","Console","Exception","StringBuilder","List","Dictionary",
  "HashSet","Tuple","Func","Action","Enumerable","Nullable","Object","Boolean","Double","Int32","Int64",
  "DateTime","Guid","System","Linq","Text","Generic","Collections","IEnumerable","Select","Where",
  "Autodesk","StringComparison","StringSplitOptions","OrdinalIgnoreCase","Comparison"]);

function walk(dir, out = []) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) walk(p, out);
    else if (e.name.endsWith(".cs")) out.push(p);
  }
  return out;
}

const files = walk(SCRIPTS).sort();
const types = new Map();          // type -> [fragment paths]
const bips = new Map(), bics = new Map();
const bump = (m, k) => m.set(k, (m.get(k) || 0) + 1);

for (const file of files) {
  const rel = path.relative(SCRIPTS, file).split(path.sep).join("/");
  const text = fs.readFileSync(file, "utf8").replace(/\/\/.*/g, "");   // drop line comments
  for (const m of text.matchAll(/\bBuiltInParameter\.([A-Z_0-9]+)/g)) bump(bips, m[1]);
  for (const m of text.matchAll(/\bBuiltInCategory\.(OST_[A-Za-z_0-9]+)/g)) bump(bics, m[1]);
  const seen = new Set();
  for (const m of text.matchAll(/(?:new |typeof\(|as | is |\(|<|,\s*)([A-Z][A-Za-z0-9]{3,})\b/g)) {
    const n = m[1];
    if (NOISE.has(n) || seen.has(n)) continue;
    seen.add(n);
    if (!types.has(n)) types.set(n, []);
    types.get(n).push(rel);
  }
}

const rows = [...types.entries()].sort((a, b) => b[1].length - a[1].length || a[0].localeCompare(b[0]));
const byCount = (m) => [...m.entries()].sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]));

let out = `# Which Revit API this Brain actually uses

Generated from the ${files.length} fragments on 2026-08-20 — **not** copied from documentation. Regenerate with the
command at the bottom; do not hand-edit.

## Why this file exists instead of a copy of the API docs

Ajmal asked (2026-08-20) whether the whole Revit API from \`revitapidocs.com\` should live in the Brain.
The instinct is right and the scale is the problem. Measured:

| | |
|---|---|
| Revit API | ~1,700 classes, 50 interfaces, 500 enumerations, **30,000+ documented members** |
| This Brain's whole index today | **3,786 chunks** from 341 files |
| Types the ${files.length} fragments actually use | **${types.size}** |

Indexing the API into the main search would make the Brain roughly **11% of its own index** — every
question would land on reference pages instead of on the skill or fragment that answers it. That is the
same failure already recorded here: 604 chunks of external standards were indexed on 2026-08-13 and
reverted the same hour for being a 20% increase. This would be eight times worse.

**And the API docs are not the gap.** A reference page tells you a method's signature. It does not tell
you that \`FilteredElementCollector.UnionWith()\` silently drops quick filters, or that
\`RBS_START_LEVEL_PARAM\` is the only level parameter an MEP curve has — both of which this Brain already
knows because it learned them the hard way. The fragments are **${files.length} proven working examples**; the value
is an index of our own usage, which is what this file is.

If the full API is genuinely wanted later, it belongs in a **separate index** that Brain searches never
touch — see [\`semantic-index/README.md\`](../semantic-index/README.md) for why the two must not share a collection.

## The types, by how much this library leans on them

\`used in N\` = number of fragments. Open the named fragment to see it used correctly in context.

| Type | used in | example fragments |
|---|---|---|
`;
for (const [name, fs_] of rows) {
  out += `| \`${name}\` | ${fs_.length} | ${fs_.slice(0, 3).map(f => "`" + f + "`").join(", ")} |\n`;
}
out += `\n## BuiltInParameter values in use (${bips.size})\n\n`;
out += byCount(bips).map(([k]) => "`" + k + "`").join(", ") + "\n";
out += `\n## BuiltInCategory values in use (${bics.size})\n\n`;
out += byCount(bics).map(([k]) => "`" + k + "`").join(", ") + "\n";
out += `
## Regenerate

Run after adding or retiring fragments — the counts go stale the moment \`scripts/\` changes:

\`\`\`
node tools/api-surface.mjs
\`\`\`
`;

fs.writeFileSync(OUT, out, "utf8");
console.log(`api-surface: ${files.length} fragments -> ${types.size} types, ${bips.size} parameters, ${bics.size} categories`);
