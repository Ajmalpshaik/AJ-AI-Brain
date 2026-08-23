#!/usr/bin/env node
// Bump the plugin version so installed copies actually receive an update.
//
// WHY THIS EXISTS: Claude Code pins a plugin to the `version` string in .claude-plugin/plugin.json.
// The documentation is blunt about the consequence - "users only receive updates when you change
// this field". So pushing new fragments to GitHub does NOT reach anyone who installed the plugin
// unless this one number moves. Forget it, and the work is published and invisible at the same
// time: no error, no warning, the colleague simply never gets it.
//
// Leaving the field out is not the fix. Omitting it falls back to a version source the public docs
// do not spell out, and "it probably updates" is not something to build a handover on.
//
// So: one command, run before pushing anything a user should receive.
//
//   node tools/plugin-release.mjs           bump the patch number (1.1.0 -> 1.1.1)
//   node tools/plugin-release.mjs --minor   bump the minor       (1.1.3 -> 1.2.0)
//   node tools/plugin-release.mjs --check   print the version, change nothing

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
const manifestPath = path.join(root, ".claude-plugin", "plugin.json");

let raw;
try {
  raw = fs.readFileSync(manifestPath, "utf8");
} catch {
  console.error("No .claude-plugin/plugin.json found at " + manifestPath);
  process.exit(1);
}

const manifest = JSON.parse(raw);
const current = manifest.version;
const m = /^(\d+)\.(\d+)\.(\d+)$/.exec(current || "");
if (!m) {
  console.error('plugin.json version is "' + current + '", which is not major.minor.patch.');
  console.error("Set it to something like 1.0.0 by hand once, then this can take over.");
  process.exit(1);
}

if (process.argv.includes("--check")) {
  console.log("plugin version: " + current);
  process.exit(0);
}

const [, maj, min, pat] = m.map(Number);
const next = process.argv.includes("--major") ? `${maj + 1}.0.0`
           : process.argv.includes("--minor") ? `${maj}.${min + 1}.0`
           : `${maj}.${min}.${pat + 1}`;

// Rewrite only the version line rather than re-serialising the whole file. JSON.stringify would
// reorder nothing but WOULD reflow every line, turning a one-number release into a diff nobody can
// read - and this file is small enough that a surgical edit is also the honest one.
const updated = raw.replace(
  new RegExp('("version"\\s*:\\s*)"' + current.replace(/\./g, "\\.") + '"'),
  '$1"' + next + '"',
);
if (updated === raw) {
  console.error("Could not find the version line to rewrite. Left the file untouched.");
  process.exit(1);
}
fs.writeFileSync(manifestPath, updated);

console.log("plugin version " + current + " -> " + next);
console.log("");
console.log("Commit this with the rest of the change and push. Installed copies pick it up on");
console.log("their next auto-update check, and the search index rebuilds itself on their side.");
