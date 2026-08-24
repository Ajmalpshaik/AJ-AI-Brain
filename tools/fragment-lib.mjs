// The fragment PARSER, in one place — shared by tools/fragment-index.mjs (the lookup) and
// mcp-server/tools/run-fragment.js (the runner).
//
// WHY IT IS ITS OWN FILE: the runner has to understand a fragment exactly the way the index does. If
// it re-implemented the INPUTS parse, the two would drift, and the failure would be silent and awful —
// `--show` would print one form for Ajmal to fill in while `run_fragment` filled in a different one.
// Same reasoning as lib/prelude.cs: the point is not shorter code, it is ONE PLACE TO BE WRONG.
//
// Everything here is COMPUTED FROM DISK on every call and stored nowhere, which is the rule
// brain-status.mjs and fragment-index.mjs already follow — a checked-in index of a folder that changes
// is a lie waiting to happen.

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

export const brainRoot = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
export const scriptsDir = path.join(brainRoot, "scripts");

export function walk(dir, results = []) {
  if (!fs.existsSync(dir)) return results;
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, e.name);
    if (e.isDirectory()) walk(full, results);
    else if (e.name.endsWith(".cs")) results.push(full);
  }
  return results;
}

// --- header fields ---------------------------------------------------------------------------------
// A field runs from "// FIELD:" until the next "// FIELD:" or a ==== divider. The continuation lines are
// indented under it, which is why this cannot just take the single matching line.
export function headerField(lines, name) {
  const start = lines.findIndex((l) => new RegExp(`^//\\s*${name}:`).test(l));
  if (start === -1) return null;
  const out = [lines[start].replace(new RegExp(`^//\\s*${name}:\\s*`), "")];
  for (let i = start + 1; i < lines.length; i++) {
    const l = lines[i];
    if (!l.startsWith("//")) break;
    if (/^\/\/\s*={5,}/.test(l)) break;
    // Next field. The separator is not always a colon: "NOT STANDALONE — see scripts/README.md" uses an
    // em dash, and matching only ":" let it bleed into the end of PRODUCES on every filter fragment.
    // Continuation lines never match this, because they start with ordinary mixed-case prose.
    if (/^\/\/\s*[A-Z][A-Z0-9 _]{2,}\s*(?::|—|-)/.test(l)) break;
    out.push(l.replace(/^\/\/\s*/, ""));
  }
  return out.join(" ").replace(/\s+/g, " ").trim();
}

// --- INPUTS block ----------------------------------------------------------------------------------
// These are the "form fields": what a caller must supply to use the fragment. Parsing them is the whole
// point - it turns "read this file and work out what it needs" into a list.

// Split on commas that are not inside (), [], {} or a string. Needed because ONE declaration line can
// hold SEVERAL fields - `byte colorR = 255, colorG = 0, colorB = 0;` - and a value can itself contain
// commas, as `new string[] { "a", "b" }` does.
function splitTopLevel(text) {
  const parts = [];
  let depth = 0, start = 0, quote = null;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (quote) {
      if (c === "\\") { i++; continue; }
      if (c === quote) quote = null;
      continue;
    }
    if (c === '"' || c === "'") { quote = c; continue; }
    if (c === "(" || c === "[" || c === "{") depth++;
    else if (c === ")" || c === "]" || c === "}") depth--;
    else if (c === "," && depth === 0) { parts.push(text.slice(start, i)); start = i + 1; }
  }
  parts.push(text.slice(start));
  return parts;
}

// One declaration LINE -> its parts, or null if the line is not a declaration.
// 50 lines across 28 fragments declare more than one field at once (every colour input does), so a
// parser that reads only the first name reports a form with fields missing - and a WRITER built on it
// would replace all three values with one and silently drop the other two. That is why this is here
// once, rather than being re-derived by whoever writes the next tool.
export function parseDeclarationLine(rawLine) {
  const m = rawLine.match(/^(\s*)(.*?);(\s*(?:\/\/.*)?)$/);
  if (!m) return null;
  const [, indent, body, tail] = m;
  if (!body.trim() || body.trim().startsWith("//")) return null;

  // The type is everything before the first `identifier =`; that lookahead keeps a generic type's own
  // commas (Dictionary<string,int>) out of the declarator split below.
  const t = body.match(/^([A-Za-z_][\w<>,?\[\]. ]*?)\s+(?=[A-Za-z_]\w*\s*=)/);
  if (!t) return null;
  const type = t[1].trim();

  const declarators = [];
  for (const piece of splitTopLevel(body.slice(t[0].length))) {
    const d = piece.match(/^\s*([A-Za-z_]\w*)\s*=\s*([\s\S]+?)\s*$/);
    if (!d) return null;
    declarators.push({ name: d[1], value: d[2] });
  }
  if (!declarators.length) return null;
  return { indent, type, declarators, tail };
}

export function parseInputs(src) {
  const m = src.match(/----\s*INPUTS[\s\S]*?----\s*\n([\s\S]*?)\/\/\s*----\s*END INPUTS/);
  if (!m) return [];
  const out = [];
  for (const raw of m[1].split(/\r?\n/)) {
    const line = raw.trim();
    if (!line || line.startsWith("//")) continue;
    const parsed = parseDeclarationLine(raw);
    if (!parsed) continue;
    const note = (parsed.tail.match(/\/\/\s*(.*)$/) || [, ""])[1].trim();
    for (const d of parsed.declarators) {
      out.push({ type: parsed.type, name: d.name, default: d.value, note });
    }
  }
  return out;
}

// The same block, located by CHARACTER OFFSET rather than parsed into fields. The runner needs to
// rewrite declarations in place, which means knowing where each one physically sits; the index only
// needs to list them. Returns null when a fragment has no INPUTS block at all (43 of them).
export function inputsBlockRange(src) {
  const m = src.match(/----\s*INPUTS[\s\S]*?----\s*\n([\s\S]*?)(\/\/\s*----\s*END INPUTS)/);
  if (!m) return null;
  const bodyStart = m.index + m[0].length - m[1].length - m[2].length;
  return { start: bodyStart, end: bodyStart + m[1].length };
}

// --- verification status ---------------------------------------------------------------------------
// Read off scripts/README.md's own per-fragment markers - the convention the library already maintains
// by hand. A row with no marker either way is the honest "never run, never flagged" case.
export function readmeRows() {
  const p = path.join(scriptsDir, "README.md");
  const readme = fs.existsSync(p) ? fs.readFileSync(p, "utf8") : "";
  return readme.split(/\r?\n/).filter((l) => l.startsWith("| [`"));
}

export function makeStatusOf(rows = readmeRows()) {
  return function statusOf(rel) {
    // Match the row whose markdown LINK TARGET is this fragment - `](path)` - not merely any row that
    // mentions the path. 8 fragments are named inside a DIFFERENT fragment's row as prose ("feeds
    // creators/create-dimension.cs", "paired with action-import-parameters-from-csv.cs"), and a plain
    // .includes() picked up whichever row came first, so those 8 inherited a neighbour's verification
    // status. That is the exact failure this whole tool exists to prevent: confidently reported, wrong.
    const row = rows.find((l) => l.includes(`](${rel})`));
    if (!row) return "not-in-readme";
    if (/NOT yet live-verified/.test(row)) return "untested";
    // "verified" and the year are allowed a few words between them. The old test was the literal
    // `verified 2026`, so a row reading "✓ verified LIVE 2026-08-14" did not match and the fragment
    // reported as UNPROVEN even though it had been run against a real model. Found 2026-08-22: EIGHT
    // fragments were affected - the aligned-dimension, spot-elevation, sheet-set, insulation,
    // highlight-vs-rest, HVAC-zone, room-elevation and trunk-slicing ones - all genuinely proven, all
    // reported as unproven, and the session-start count was understating the library by 8. The natural
    // phrasing was right and the regex was wrong, so this is fixed here rather than by rewording eight
    // rows into an unnatural shape that would break again the next time someone wrote plainly.
    // The `NOT yet live-verified` test above still runs FIRST, so an explicit not-yet row is unaffected.
    if (/\bverified\b[^|]{0,24}?20\d\d/.test(row)) return "verified";
    // These two are plain substring tests, so a row that NARRATES an old status flips to it. Hit for
    // real 2026-08-24: create-ceiling.cs was corrected months earlier, its row was rewritten to say so,
    // and the sentence `This row said "CONFIRMED IMPOSSIBLE" until today` put the fragment straight back
    // into the impossible bucket. If a row needs to discuss a superseded verdict, paraphrase it -
    // "the old impossible verdict" - rather than quoting the literal phrase.
    if (/CONFIRMED IMPOSSIBLE/.test(row)) return "impossible";
    if (/BLOCKED/.test(row)) return "blocked";
    // LAST, and deliberately so. Everything above has already had its say, which makes this check purely
    // ADDITIVE: it can only ever move a fragment from "no status recorded" to "written, not yet run", and
    // can never reclassify one that is verified, blocked or impossible. That ordering matters, because a
    // proven fragment's row often notes that ONE path is still unexercised ("the <3-points guard is not
    // exercised"), and putting this test any earlier would flip those to untested.
    //
    // WHY IT EXISTS: measured 2026-08-24, ELEVEN of the 39 rows reporting "no status either way" in fact
    // said plainly that the fragment had not been run - "not yet run against a real model", "unproven",
    // "untested", "run it on ONE room first" - just not in the one literal phrase the check above looks
    // for. The session-start banner was calling them unflagged when their author had flagged them, which
    // is the same failure as the eight verified-but-reported-unproven fragments described above, and the
    // same conclusion applies: fix the regex, do not reword rows into an unnatural shape.
    if (/\b(?:not yet (?:run|been run|proven|tested|verified)|never (?:been )?run|not run against|unproven|untested|run (?:it|this) on (?:ONE|one|a )|run once on)\b/i.test(row))
      return "untested";
    return "no-status";
  };
}

export const STATUS_LABEL = {
  verified: "PROVEN", untested: "not run", blocked: "blocked",
  impossible: "impossible", "no-status": "unproven", "not-in-readme": "?",
};

// --- build -----------------------------------------------------------------------------------------
// One record per fragment. `src` is omitted by default because the index never needs 1.5 MB of C# in
// memory to answer a lookup; the runner asks for it.
export function loadFragments({ withSource = false } = {}) {
  const statusOf = makeStatusOf();
  return walk(scriptsDir).map((full) => {
    const rel = path.relative(scriptsDir, full).split(path.sep).join("/");
    const src = fs.readFileSync(full, "utf8");
    const lines = src.split(/\r?\n/);
    const parts = rel.split("/");
    const rec = {
      path: rel,
      kind: parts[0],
      group: parts.length > 2 ? parts[1] : null,
      name: path.basename(rel),
      purpose: headerField(lines, "PURPOSE") || "",
      assumes: headerField(lines, "ASSUMES") || "",
      produces: headerField(lines, "PRODUCES") || "",
      gotchas: lines.filter((l) => /^\/\/\s*GOTCHA/.test(l)).length,
      inputs: parseInputs(src),
      status: statusOf(rel),
    };
    if (withSource) {
      rec.src = src;
      rec.absPath = full;
      rec.standalone = !/NOT STANDALONE/.test(src);
    }
    return rec;
  }).sort((a, b) => a.path.localeCompare(b.path));
}

// --- name resolution -------------------------------------------------------------------------------
// Ajmal and the assistant both name fragments loosely - "filter-by-category", with or without the .cs,
// with or without the folder. An AMBIGUOUS name must never quietly pick one: `create-sheet.cs` and
// `create-sheets.cs` genuinely both exist, and picking the wrong one silently is the whole class of
// failure this Brain is built to stop. Returns {fragment} or {error, candidates}.
export function resolveFragment(query, fragments) {
  const q = String(query || "").trim().replace(/\\/g, "/").replace(/^scripts\//, "");
  if (!q) return { error: "Empty fragment name." };
  const withCs = q.endsWith(".cs") ? q : q + ".cs";

  const exactPath = fragments.filter((f) => f.path === withCs);
  if (exactPath.length === 1) return { fragment: exactPath[0] };

  const byName = fragments.filter((f) => f.name === withCs);
  if (byName.length === 1) return { fragment: byName[0] };

  const bySuffix = fragments.filter((f) => f.path.endsWith("/" + withCs));
  if (bySuffix.length === 1) return { fragment: bySuffix[0] };

  const candidates = [...new Set([...byName, ...bySuffix].map((f) => f.path))];
  if (candidates.length > 1) {
    return { error: `"${query}" matches ${candidates.length} fragments — name one exactly.`, candidates };
  }
  const near = fragments
    .filter((f) => f.name.includes(q.replace(/\.cs$/, "")) || f.path.includes(q.replace(/\.cs$/, "")))
    .map((f) => f.path)
    .slice(0, 8);
  return {
    error: `No fragment named "${query}".`,
    candidates: near,
  };
}
