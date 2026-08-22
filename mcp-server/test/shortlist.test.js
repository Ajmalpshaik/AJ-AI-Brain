// The usage shortlist — the rules that make it "as per my usage" rather than "as per all history".
//
// It runs on EVERY message, so two things are tested that would not normally be: that it never
// throws on a broken or missing log (a hook that throws blocks what Ajmal typed), and that a
// half-written last line loses one record instead of the whole file.
//
// The most important test here is the LAST one. The shortlist is only as good as the log feeding
// it, and run_fragment names its fragments instead of pasting their code — so the logger had to
// learn to read them from a different field. Without that, adopting run_fragment would have quietly
// turned the usage record into a record of the OLD way only, and this feature would rank on data
// that was slowly going blind.

import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

import { rankFragments, shortlistBlock } from "../../tools/shortlist.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const brainRoot = path.join(here, "..", "..");
const TODAY = "2026-08-22";

function tmpLog(records) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "shortlist-"));
  const file = path.join(dir, "runs.jsonl");
  fs.writeFileSync(file, records.map((r) => (typeof r === "string" ? r : JSON.stringify(r))).join("\n") + "\n");
  return file;
}
const run = (date, ...fragments) => ({ date, tool: "run_fragment", fragments, codeLines: 0 });

test("ranks by RECENT use, so a fragment he has stopped using drops off", () => {
  const file = tmpLog([
    // Used constantly three months ago, not once since.
    ...Array.from({ length: 40 }, () => run("2026-05-20", "old-favourite.cs")),
    // Used a handful of times this month.
    ...Array.from({ length: 5 }, () => run("2026-08-18", "current-tool.cs")),
  ]);

  const { ranked, window } = rankFragments({ file, today: TODAY, days: 30 });
  assert.equal(window, "recent");
  assert.equal(ranked[0].name, "current-tool.cs", "the one he actually uses now must lead");
  assert.ok(!ranked.some((r) => r.name === "old-favourite.cs"), "the abandoned one must not appear");

  // Widen the window and the old one comes back — proving the window is what excluded it, not a bug.
  const wide = rankFragments({ file, today: TODAY, days: 200 });
  assert.equal(wide.ranked[0].name, "old-favourite.cs");
  assert.equal(wide.ranked[0].count, 40);
});

test("falls back to all-time when the recent window is empty, and says which it used", () => {
  const file = tmpLog([run("2026-01-10", "a.cs"), run("2026-01-11", "a.cs"), run("2026-01-11", "b.cs")]);
  const { ranked, window } = rankFragments({ file, today: TODAY, days: 30 });
  assert.equal(window, "all-time", "an empty recent window must not read as 'no data'");
  assert.equal(ranked[0].name, "a.cs");
  assert.match(shortlistBlock({ file, today: TODAY }), /all time/);
});

test("ties break by all-time use, so the ordering is stable and meaningful", () => {
  const file = tmpLog([
    ...Array.from({ length: 10 }, () => run("2026-02-01", "workhorse.cs")),
    run("2026-08-20", "workhorse.cs"),
    run("2026-08-20", "newcomer.cs"),
  ]);
  const { ranked } = rankFragments({ file, today: TODAY, days: 30 });
  assert.equal(ranked[0].name, "workhorse.cs", "same recent count — the one carrying more work overall wins");
  assert.equal(ranked[0].allTime, 11);
});

test("no log at all is silence, not an error", () => {
  const missing = path.join(os.tmpdir(), "definitely-not-here-" + Math.random().toString(36).slice(2), "x.jsonl");
  const { ranked } = rankFragments({ file: missing });
  assert.deepEqual(ranked, []);
  assert.equal(shortlistBlock({ file: missing }), "", "a fresh checkout must inject nothing at all");
});

test("a half-written last line loses that record, not the whole log", () => {
  const file = tmpLog([run("2026-08-20", "good.cs"), run("2026-08-20", "good.cs"), '{"date":"2026-08-2']);
  const { ranked } = rankFragments({ file, today: TODAY, days: 30 });
  assert.equal(ranked[0].name, "good.cs");
  assert.equal(ranked[0].count, 2);
});

test("the injected block stays small and carries the decision order", () => {
  const file = tmpLog(Array.from({ length: 40 }, (_, i) => run("2026-08-20", `frag-${i}.cs`)));
  const block = shortlistBlock({ file, today: TODAY });
  assert.ok(Buffer.byteLength(block) < 1024, `block is ${Buffer.byteLength(block)} bytes — it is paid for on every message`);
  assert.equal(block.split("\n").length, 3);
  assert.match(block, /run_fragment/);
  assert.match(block, /search/i);
  assert.match(block, /new C#/);
});

test("the logger records run_fragment's NAMED fragments, not just pasted code", () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "joblog-"));
  const logFile = path.join(dir, "job-log", "revit-runs.jsonl");

  // The hook writes to job-log/ relative to the Brain root, so run it in a copy of that shape.
  const script = path.join(brainRoot, "tools", "job-log-revit.mjs");
  const payload = JSON.stringify({
    tool_name: "mcp__aj-tools-aj-ai__run_fragment",
    tool_input: {
      describe: "test",
      // Bare name, nested path, and a name already carrying .cs — all must normalise the same way.
      fragments: ["filter-by-category", "actions/color-graphics/action-set-color-uniform.cs", "create-sheet.cs"],
    },
  });

  fs.mkdirSync(path.join(dir, "tools"), { recursive: true });
  fs.copyFileSync(script, path.join(dir, "tools", "job-log-revit.mjs"));
  const res = spawnSync(process.execPath, [path.join(dir, "tools", "job-log-revit.mjs")], {
    input: payload, encoding: "utf8",
  });
  assert.equal(res.status, 0, "the logger must always exit 0");

  const written = JSON.parse(fs.readFileSync(logFile, "utf8").trim().split("\n").pop());
  assert.deepEqual(
    written.fragments.sort(),
    ["action-set-color-uniform.cs", "create-sheet.cs", "filter-by-category.cs"],
    "all three name shapes must normalise to the bare name.cs the log already uses"
  );
  assert.equal(written.tool, "run_fragment");
});
