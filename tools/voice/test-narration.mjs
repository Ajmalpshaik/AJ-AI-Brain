#!/usr/bin/env node
// Checks what the voice would SAY, without making a sound.
//
// WHY: the wording is the whole product here. A narrator that says "running command" for everything,
// or reads a Windows path out loud, gets muted on the first day - and you only find that out by
// listening to it, which is a slow and irritating way to test. This feeds realistic hook payloads
// straight through narrate-hook.mjs in dry-run mode and prints the exact sentence for each, so the
// wording can be reviewed and re-checked in a second after any change.
//
// Usage:  node tools/voice/test-narration.mjs

import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const voiceDir = path.dirname(fileURLToPath(import.meta.url));
const hook = path.join(voiceDir, "narrate-hook.mjs");

const cases = [
  ["count diffusers", { hook_event_name: "PreToolUse", tool_name: "mcp__aj-tools-aj-ai__count_elements", tool_input: { category: "Air Terminals" } }],
  ["count by OST name", { hook_event_name: "PreToolUse", tool_name: "mcp__aj-tools-aj-ai__count_elements", tool_input: { category: "OST_DuctCurves" } }],
  ["delete (must be clear)", { hook_event_name: "PreToolUse", tool_name: "mcp__aj-tools-aj-ai__delete_elements", tool_input: { elementIds: [1, 2, 3] } }],
  ["script with a comment", { hook_event_name: "PreToolUse", tool_name: "mcp__aj-tools-aj-ai__run_csharp", tool_input: { code: "// Place air terminals across the ceiling grid\nvar doc = ..." } }],
  ["script with no comment", { hook_event_name: "PreToolUse", tool_name: "mcp__aj-tools-aj-ai__run_csharp", tool_input: { code: "var doc = uidoc.Document;" } }],
  ["set a parameter", { hook_event_name: "PreToolUse", tool_name: "mcp__aj-tools-aj-ai__set_parameter_value", tool_input: { parameterName: "Flow" } }],
  ["isolate", { hook_event_name: "PreToolUse", tool_name: "mcp__aj-tools-aj-ai__isolate_elements", tool_input: { category: "Ducts" } }],
  ["ping", { hook_event_name: "PreToolUse", tool_name: "mcp__aj-tools-aj-ai__ping", tool_input: {} }],

  ["read a knowledge note", { hook_event_name: "PreToolUse", tool_name: "Read", tool_input: { file_path: "D:\\Ajmal\\AJ AI Brain\\knowledge\\live-model\\families.md" } }],
  ["edit a fragment", { hook_event_name: "PreToolUse", tool_name: "Edit", tool_input: { file_path: "scripts/recipes/create-floor.cs" } }],
  ["bash with description", { hook_event_name: "PreToolUse", tool_name: "Bash", tool_input: { command: "git status", description: "Show working tree status" } }],
  ["grep", { hook_event_name: "PreToolUse", tool_name: "Grep", tool_input: { pattern: "BeginTask" } }],
  ["agent", { hook_event_name: "PreToolUse", tool_name: "Agent", tool_input: { description: "find the duct sizing rules" } }],
  ["skill", { hook_event_name: "PreToolUse", tool_name: "Skill", tool_input: { skill: "ajtools-hvac-duct-routing" } }],
  ["ask the user", { hook_event_name: "PreToolUse", tool_name: "AskUserQuestion", tool_input: {} }],

  ["MUTED bookkeeping", { hook_event_name: "PreToolUse", tool_name: "TaskUpdate", tool_input: { taskId: "1" } }],
  ["MUTED self-reference", { hook_event_name: "PreToolUse", tool_name: "Bash", tool_input: { command: "node tools/voice/say.mjs hi", description: "Test the voice" } }],

  ["notification", { hook_event_name: "Notification", message: "Claude needs your permission to use Bash" }],
  ["session start", { hook_event_name: "SessionStart" }],
];

let spoken = 0;
let silent = 0;

for (const [label, payload] of cases) {
  const result = spawnSync(process.execPath, [hook], {
    input: JSON.stringify(payload),
    encoding: "utf8",
    env: { ...process.env, AJ_VOICE_DRYRUN: "1" },
  });

  const line = (result.stdout || "").trim();
  if (line) spoken++;
  else silent++;
  console.log(`  ${label.padEnd(26)} ${line || "(silent)"}`);
}

console.log(`\n  ${spoken} spoken, ${silent} silent.`);
