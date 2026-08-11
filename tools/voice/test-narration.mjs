#!/usr/bin/env node
// Checks what the voice would SAY, without making a sound.
//
// WHY: the wording is the whole product here. A narrator that says "running command" for everything,
// or reads a Windows path out loud, gets muted on the first day - and you only find that out by
// listening to it, which is a slow and irritating way to test. This feeds realistic hook payloads
// straight through narrate-hook.mjs in dry-run mode and prints the exact sentence for each, so the
// wording can be reviewed and re-checked in a second after any change.
//
// TWO STAGES, because the spoken line is built in two places and testing only the first one lies:
//   1. narrate-hook.mjs  decides WHETHER to speak and picks the sentence      (Node, this repo)
//   2. drainer.py        strips filler and trims to maxWords before speaking  (Python)
// A test that stops at stage 1 shows a sentence Ajmal never actually hears.
//
// Usage:  node tools/voice/test-narration.mjs

import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const voiceDir = path.dirname(fileURLToPath(import.meta.url));
const brainRoot = path.dirname(path.dirname(voiceDir));
const hook = path.join(voiceDir, "narrate-hook.mjs");

// Expectation is written next to each case on purpose. "17 spoken, 2 silent" tells you nothing about
// whether the RIGHT ones were silent, and the quiet rule (speakOnly: "revit") means most of this list
// is now supposed to say nothing at all.
const cases = [
  // --- Reaches the live model: these are the ones worth hearing -------------------------------
  ["count diffusers", "speak", { hook_event_name: "PreToolUse", tool_name: "mcp__aj-tools-aj-ai__count_elements", tool_input: { category: "Air Terminals" } }],
  ["count by OST name", "speak", { hook_event_name: "PreToolUse", tool_name: "mcp__aj-tools-aj-ai__count_elements", tool_input: { category: "OST_DuctCurves" } }],
  ["delete by id (needs count)", "speak", { hook_event_name: "PreToolUse", tool_name: "mcp__aj-tools-aj-ai__delete_elements", tool_input: { elementIds: [1, 2, 3], category: "Ducts" } }],
  ["delete by filter (no count)", "speak", { hook_event_name: "PreToolUse", tool_name: "mcp__aj-tools-aj-ai__delete_elements", tool_input: { category: "Ducts" } }],
  ["move by id", "speak", { hook_event_name: "PreToolUse", tool_name: "mcp__aj-tools-aj-ai__move_elements", tool_input: { elementIds: [7, 8], category: "Air Terminals" } }],
  ["script with a comment", "speak", { hook_event_name: "PreToolUse", tool_name: "mcp__aj-tools-aj-ai__run_csharp", tool_input: { code: "// Place air terminals across the ceiling grid\nvar doc = ..." } }],
  // No comment on top - the wording has to come out of the code, or Ajmal hears "Running script" and
  // learns nothing. This is the case he reported on 2026-08-11 asking for a colour change.
  ["colour, no comment", "speak", { hook_event_name: "PreToolUse", tool_name: "mcp__aj-tools-aj-ai__run_csharp", tool_input: { code: "var ogs = new OverrideGraphicSettings();\nogs.SetProjectionLineColor(new Color(255,0,0));\nview.SetElementOverrides(id, ogs);" } }],
  ["delete, no comment", "speak", { hook_event_name: "PreToolUse", tool_name: "mcp__aj-tools-aj-ai__run_csharp", tool_input: { code: "using (var t = new Transaction(Document, \"x\")) { t.Start(); Document.Delete(ids); t.Commit(); }" } }],
  ["draw duct, no comment", "speak", { hook_event_name: "PreToolUse", tool_name: "mcp__aj-tools-aj-ai__run_csharp", tool_input: { code: "var d = Duct.Create(Document, systemTypeId, ductTypeId, levelId, p1, p2);" } }],
  ["read-only, no comment", "speak", { hook_event_name: "PreToolUse", tool_name: "mcp__aj-tools-aj-ai__run_csharp", tool_input: { code: "var n = new FilteredElementCollector(Document).OfClass(typeof(Wall)).GetElementCount();" } }],
  // A separator line on top must not be read out as if it were a sentence.
  ["script with a separator", "speak", { hook_event_name: "PreToolUse", tool_name: "mcp__aj-tools-aj-ai__run_csharp", tool_input: { code: "// ============================\nvar ogs = new OverrideGraphicSettings();" } }],
  ["script with no comment", "speak", { hook_event_name: "PreToolUse", tool_name: "mcp__aj-tools-aj-ai__run_csharp", tool_input: { code: "var doc = uidoc.Document;" } }],
  ["set a parameter", "speak", { hook_event_name: "PreToolUse", tool_name: "mcp__aj-tools-aj-ai__set_parameter_value", tool_input: { parameterName: "Flow" } }],
  ["isolate", "speak", { hook_event_name: "PreToolUse", tool_name: "mcp__aj-tools-aj-ai__isolate_elements", tool_input: { category: "Ducts" } }],
  ["reset the view", "speak", { hook_event_name: "PreToolUse", tool_name: "mcp__aj-tools-aj-ai__reset_isolation", tool_input: {} }],

  // --- Touches Revit but says nothing about the model ------------------------------------------
  ["ping (health check)", "silent", { hook_event_name: "PreToolUse", tool_name: "mcp__aj-tools-aj-ai__ping", tool_input: {} }],

  // --- The assistant THINKING: all of this is meant to be silent now ---------------------------
  ["read a knowledge note", "silent", { hook_event_name: "PreToolUse", tool_name: "Read", tool_input: { file_path: "D:\\Ajmal\\AJ AI Brain\\knowledge\\live-model\\families.md" } }],
  ["edit a fragment", "silent", { hook_event_name: "PreToolUse", tool_name: "Edit", tool_input: { file_path: "scripts/recipes/create-floor.cs" } }],
  ["bash with description", "silent", { hook_event_name: "PreToolUse", tool_name: "Bash", tool_input: { command: "git status", description: "Show working tree status" } }],
  ["grep", "silent", { hook_event_name: "PreToolUse", tool_name: "Grep", tool_input: { pattern: "BeginTask" } }],
  ["agent", "silent", { hook_event_name: "PreToolUse", tool_name: "Agent", tool_input: { description: "find the duct sizing rules" } }],
  ["skill", "silent", { hook_event_name: "PreToolUse", tool_name: "Skill", tool_input: { skill: "ajtools-hvac-duct-routing" } }],
  ["ask the user", "silent", { hook_event_name: "PreToolUse", tool_name: "AskUserQuestion", tool_input: {} }],
  ["MUTED bookkeeping", "silent", { hook_event_name: "PreToolUse", tool_name: "TaskUpdate", tool_input: { taskId: "1" } }],
  ["MUTED self-reference", "silent", { hook_event_name: "PreToolUse", tool_name: "Bash", tool_input: { command: "node tools/voice/say.mjs hi", description: "Test the voice" } }],
  ["session start greeting", "silent", { hook_event_name: "SessionStart" }],

  // --- Still spoken: waiting on Ajmal, and the closing answer -----------------------------------
  ["notification", "speak", { hook_event_name: "Notification", message: "Claude needs your permission to use Bash" }],
];

console.log("\n  STAGE 1 - what narrate-hook.mjs decides to say\n");

let failures = 0;

for (const [label, expectation, payload] of cases) {
  const result = spawnSync(process.execPath, [hook], {
    input: JSON.stringify(payload),
    encoding: "utf8",
    env: { ...process.env, AJ_VOICE_DRYRUN: "1" },
  });

  const line = (result.stdout || "").trim();
  const actual = line ? "speak" : "silent";
  const ok = actual === expectation;
  if (!ok) failures++;

  console.log(`  ${ok ? "ok  " : "FAIL"}  ${label.padEnd(28)} ${line || "(silent)"}`);
}

// --------------------------------------------------------------------------------------------------
// Stage 2: the filler-stripping and length trim every line goes through before it is spoken.
// --------------------------------------------------------------------------------------------------

// [profile, line, maxWords] - maxWords null means "use the global cap".
const drainerCases = [
  ["jarvis", "Counting air terminals.", null],
  ["jarvis", "Deleting 3 ducts.", null],
  ["jarvis", "Reset view.", null],
  ["jarvis", "Clearing the graphic overrides.", null],
  ["jarvis", "Found forty two air terminals across the whole of level two and isolated them.", null],
  // Same sentence as the closing summary, which is allowed past the tight cap so the answer survives.
  ["jarvis", "Found forty two air terminals across the whole of level two and isolated them.", 16],
];

const venvPython = path.join(brainRoot, "semantic-index", "venv", "Scripts", "python.exe");
const python = fs.existsSync(venvPython) ? venvPython : process.platform === "win32" ? "python" : "python3";

const probe = [
  "import json, sys",
  `sys.path.insert(0, r"${voiceDir}")`,
  "import drainer",
  "config = drainer.load_config()",
  "for profile, line, cap in json.load(sys.stdin):",
  "    out = drainer.caveman(line) if config.get('caveman', True) else line",
  "    print(profile + '|' + drainer.trim(out, cap if cap else config.get('maxWords', 14)))",
].join("\n");

const drained = spawnSync(python, ["-c", probe], {
  input: JSON.stringify(drainerCases),
  encoding: "utf8",
});

console.log("\n  STAGE 2 - what drainer.py finally speaks\n");

if (drained.status !== 0) {
  console.log(`  skipped - could not run ${python}: ${(drained.stderr || "").trim().split("\n").pop()}`);
} else {
  const outputs = (drained.stdout || "").trim().split("\n").filter(Boolean);
  for (let index = 0; index < outputs.length; index++) {
    const [profile, ...rest] = outputs[index].split("|");
    const before = drainerCases[index]?.[1] ?? "";
    const after = rest.join("|");
    const changed = before !== after;
    console.log(`  [${profile.padEnd(6)}] ${changed ? `${before}  ->  ${after}` : after}`);
  }
}

console.log(failures ? `\n  ${failures} case(s) spoke when they should not, or stayed silent when they should not.` : "\n  All cases behaved as expected.");
