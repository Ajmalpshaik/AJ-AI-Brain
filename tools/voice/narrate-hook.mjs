#!/usr/bin/env node
// Turns one Claude Code hook event into one short spoken English sentence.
//
// WHY THIS FILE IS THE WHOLE POINT: the banner already in Revit
// (src/AiShell/Services/AiTaskWarningBarService.cs) says "AJ AI is working / Processing your Revit
// task..." for every job, whether it is counting diffusers or deleting walls. It is an on/off light,
// not information. What makes a voice worth listening to is that it says the actual thing.
//
// So this reads what each tool call genuinely carries and speaks that: the category name on a count,
// the file name on a read, the plain-English `description` field that every Bash call already
// includes. You hear "Counting air terminals", not "running command".
//
// WHAT IT DELIBERATELY WILL NOT DO: read out paths, code, IDs or anything else that is unpleasant to
// listen to. Everything is reduced to words a person would actually say out loud, then cut short -
// Ajmal asked for a SHORT reply in voice, and a narrator that reads file paths aloud gets muted on
// day one.
//
// Wired to four events in .claude/settings.json:
//   PreToolUse   - narrate each action as it starts   (this is the "every single action" setting)
//   Notification - say when Claude is waiting on Ajmal, so he knows to look at the screen
//   Stop         - speak the one-line summary of what just got done
//   SessionStart - a short greeting so he knows the voice is alive
//
// Reads the hook payload as JSON on stdin. Always exits 0: a hook that fails must never interrupt
// the work it is only describing.

import fs from "node:fs";
import { say } from "./say.mjs";

const VOICE_PROFILE = "jarvis"; // The only voice. See voice-config.json.

/** Tool-name prefix for everything that reaches the live model through the AJ AI bridge. */
const REVIT_TOOL_PREFIX = "mcp__aj-tools-aj-ai__";

/** Word budget for the closing summary only - the per-action lines stay on the tight global cap. */
const SUMMARY_MAX_WORDS = 16;

function readConfig() {
  try {
    return JSON.parse(fs.readFileSync(new URL("./voice-config.json", import.meta.url), "utf8"));
  } catch {
    return {}; // Defaults are fine - a missing config must not silence the voice.
  }
}

// --------------------------------------------------------------------------------------------------
// Making text sayable
// --------------------------------------------------------------------------------------------------

/** Strip anything that sounds bad read aloud, and collapse to a single clean line. */
function sayable(value, maxWords = 12) {
  let text = String(value ?? "")
    .replace(/```[\s\S]*?```/g, " ")           // fenced code
    .replace(/`[^`]*`/g, " ")                  // inline code
    .replace(/\[([^\]]+)\]\([^)]*\)/g, "$1")   // markdown links -> just the words
    .replace(/[*_#>|]/g, " ")                  // markdown furniture
    .replace(/https?:\/\/\S+/g, " ")           // URLs
    .replace(/[A-Za-z]:\\\S+|\/\S+\//g, " ")   // file paths
    .replace(/\s+/g, " ")
    .trim();

  const words = text.split(" ").filter(Boolean);
  if (words.length > maxWords) text = words.slice(0, maxWords).join(" ").replace(/[,;:]$/, "");
  return text;
}

/** "create-floor.cs" -> "create floor";  "families.md" -> "families". A name, not a path. */
function prettyFileName(filePath) {
  const base = String(filePath ?? "").split(/[\\/]/).pop() ?? "";
  return base.replace(/\.[^.]+$/, "").replace(/[-_]+/g, " ").trim();
}

/** "OST_DuctCurves" -> "duct curves";  "Air Terminals" -> "air terminals". */
function prettyCategory(category) {
  return String(category ?? "")
    .replace(/^OST_/, "")
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .replace(/[-_]+/g, " ")
    .toLowerCase()
    .trim();
}

/**
 * A run_csharp payload should open with a comment saying what it does. Prefer that over guessing.
 *
 * Only the opening few lines are considered. The first version searched the WHOLE script for any line
 * starting with "//", which happily read out an explanatory note from the middle of a fifty-line
 * script - a sentence about one detail, announced as if it were the job.
 */
function intentFromCode(code) {
  const header = String(code ?? "").split("\n").slice(0, 4);
  for (const line of header) {
    const trimmed = line.trim();
    if (!trimmed.startsWith("//")) continue;
    // Separator rules ("// =====", "// -----") are decoration, not a sentence.
    const body = trimmed.replace(/^\/+/, "").replace(/^[\s=\-*]+|[\s=\-*]+$/g, "");
    const text = sayable(body, 10);
    if (text.length > 3) return text;
  }
  return "";
}

/**
 * What the script DOES, worked out from the Revit API calls in it.
 *
 * Ajmal, 2026-08-11: asked to change a colour, and all the voice said was "Running script" - because
 * the work went through run_csharp with no comment on top. A narrator that says "running script" for
 * every scripted job is the banner problem again: an on/off light, not information. Writing the
 * comment is the real fix (see scripts/README.md), but a narrator must not depend on the caller
 * remembering something, so this reads the code instead.
 *
 * ORDER MATTERS - most specific first. Nearly every script contains a FilteredElementCollector, so
 * "Reading the model" has to be last or it would win every time and describe nothing.
 */
const CODE_INTENT_HINTS = [
  [/SetSurfaceTransparency/, "Setting transparency"],
  [/SetProjectionLineColor|SetSurfaceForegroundPatternColor|SetCutLineColor|OverrideGraphicSettings/, "Changing colour"],
  [/\bDuct\.Create/, "Drawing duct"],
  [/\bPipe\.Create/, "Drawing pipe"],
  [/\bWall\.Create/, "Drawing walls"],
  [/\bFloor\.Create/, "Creating floor"],
  [/NewRoom|\bSpace\b.*Create|NewSpace/, "Creating rooms"],
  [/ViewSchedule\.Create/, "Creating a schedule"],
  [/NewFamilyInstance/, "Placing families"],
  [/ElementTransformUtils\.CopyElement/, "Copying elements"],
  [/ElementTransformUtils\.RotateElement/, "Rotating elements"],
  [/ElementTransformUtils\.MoveElement/, "Moving elements"],
  [/Document\.Delete|doc\.Delete/, "Deleting elements"],
  [/IsolateElementsTemporary|IsolateElementTemporary/, "Isolating elements"],
  [/\.HideElements|\.UnhideElements/, "Changing what is visible"],
  [/LookupParameter\([^)]*\)\s*\.\s*Set|\.Set\(/, "Setting parameters"],
  [/new Transaction/, "Changing the model"],
  [/FilteredElementCollector/, "Reading the model"],
];

function guessFromCode(code) {
  const text = String(code ?? "");
  for (const [pattern, phrase] of CODE_INTENT_HINTS) {
    if (pattern.test(text)) return phrase;
  }
  return "";
}

// --------------------------------------------------------------------------------------------------
// Tool -> sentence
// --------------------------------------------------------------------------------------------------

/**
 * The live-Revit tools. These are the ones worth hearing, so they get the most specific wording.
 *
 * CAVEMAN WORDING, per Ajmal 2026-08-11: "no too much reply, main things only like caveman". Articles
 * and filler are dropped ("Resetting the view" -> "Reset view", "Running a script in Revit" ->
 * "Running script"), because a narrator you listen to all day is judged on how little it makes you
 * sit through. What is never dropped: the category name and the count - those are the information.
 */
function narrateRevitTool(shortName, input) {
  const category = prettyCategory(input.category ?? input.categoryName ?? "");
  const noun = category || "elements";
  const withCategory = (verb) => `${verb} ${noun}.`;

  // Every element-targeting tool accepts EITHER exact Element Ids OR a category filter
  // (mcp-server/shared/element-filter.js). When the Ids are given the count is known BEFORE Revit
  // runs - which is the only chance to hear "twelve" ahead of a delete instead of after it.
  const idCount = Array.isArray(input.elementIds) ? input.elementIds.length : 0;
  const withCount = (verb) => (idCount ? `${verb} ${idCount} ${noun}.` : `${verb} ${noun}.`);

  switch (shortName) {
    case "ping":                     return ""; // A bridge health check says nothing about the model.
    case "model_summary":            return "Model summary.";
    case "count_elements":           return withCategory("Counting");
    case "list_elements":            return withCategory("Listing");
    case "report_parameters":        return withCategory("Parameters on");
    case "select_elements":          return withCategory("Selecting");
    case "isolate_elements":         return withCategory("Isolating");
    case "reset_isolation":          return "Reset view.";
    case "hide_elements":            return withCategory("Hiding");
    case "unhide_elements":          return withCategory("Unhiding");
    case "set_color":                return withCategory("Colouring");
    case "set_transparency":         return withCategory("Transparency on");
    case "reset_graphic_overrides":  return "Clearing overrides.";
    // The two that change the model get the count spoken. Deliberately louder, not quieter: this is
    // the one line Ajmal most needs to catch by ear, and "Deleting elements" with no number is a
    // warning that arrives too late to be worth anything.
    case "move_elements":            return withCount("Moving");
    case "delete_elements":          return withCount("Deleting");
    case "set_parameter_value": {
      const parameter = sayable(input.parameterName ?? input.parameter ?? input.name ?? "", 5);
      return parameter ? `Setting ${parameter}.` : "Setting parameter.";
    }
    case "run_csharp": {
      // Comment first (the author said what they meant), code second (worked out), "Running script"
      // only when neither can tell you anything - which should now be almost never.
      const code = input.code ?? input.script ?? "";
      const intent = intentFromCode(code) || guessFromCode(code);
      return intent ? `${intent}.` : "Running script.";
    }
    default:
      return `Revit: ${shortName.replace(/_/g, " ")}.`;
  }
}

/** Everything else - the Brain's own files, searches, agents, the web. */
function narrateTool(toolName, input) {
  if (toolName.startsWith("mcp__aj-tools-aj-ai__")) {
    return narrateRevitTool(toolName.replace("mcp__aj-tools-aj-ai__", ""), input);
  }

  switch (toolName) {
    case "Read": {
      const name = prettyFileName(input.file_path);
      return name ? `Reading ${name}.` : "Reading a file.";
    }
    case "Write": {
      const name = prettyFileName(input.file_path);
      return name ? `Writing ${name}.` : "Writing a file.";
    }
    case "Edit":
    case "NotebookEdit": {
      const name = prettyFileName(input.file_path ?? input.notebook_path);
      return name ? `Editing ${name}.` : "Editing a file.";
    }
    case "Bash":
    case "PowerShell": {
      // Every Bash call already carries a plain-English description written for a human. Use it.
      const described = sayable(input.description, 12);
      return described ? `${described}.` : "Running a command.";
    }
    case "Grep": {
      const pattern = sayable(input.pattern, 5);
      return pattern ? `Searching for ${pattern}.` : "Searching the files.";
    }
    case "Glob":
      return "Looking for files.";
    case "Skill": {
      // Skill ids are hyphenated slugs ("ajtools-hvac-duct-routing"). Spoken with the hyphens intact
      // they come out as one unreadable run-on word, so they get spaced back into English first.
      const skill = sayable(String(input.skill ?? "").replace(/^ajtools-/, "").replace(/[-:_]+/g, " "), 5);
      return skill ? `Loading the ${skill} skill.` : "Loading a skill.";
    }
    case "Agent":
    case "Task": {
      const described = sayable(input.description, 8);
      return described ? `Sending an agent to ${described}.` : "Sending out an agent.";
    }
    case "WebSearch": {
      const query = sayable(input.query, 8);
      return query ? `Searching the web for ${query}.` : "Searching the web.";
    }
    case "WebFetch":
      return "Reading a web page.";
    case "AskUserQuestion":
      return "I need your answer.";
    case "Artifact":
      return "Publishing a page.";
    case "Workflow":
      return "Starting a workflow.";
    default:
      return `${toolName.replace(/^mcp__[^_]+__/, "").replace(/[-_]/g, " ")}.`;
  }
}

// --------------------------------------------------------------------------------------------------
// The final summary, read back out of the transcript
// --------------------------------------------------------------------------------------------------

/**
 * On Stop, speak the first sentence of what was just said on screen. This is the line that actually
 * answers "what did the AI do" - and it costs nothing extra, because the model already wrote it.
 * The transcript is JSONL, one message per line, so the last assistant text is the last match.
 */
function finalSummary(transcriptPath) {
  try {
    if (!transcriptPath || !fs.existsSync(transcriptPath)) return "";
    const lines = fs.readFileSync(transcriptPath, "utf8").split("\n").filter(Boolean);

    for (let index = lines.length - 1; index >= 0; index--) {
      let entry;
      try {
        entry = JSON.parse(lines[index]);
      } catch {
        continue;
      }
      if (entry?.type !== "assistant") continue;

      const content = entry?.message?.content;
      const blocks = Array.isArray(content) ? content : [];
      const text = blocks.filter((b) => b?.type === "text").map((b) => b.text).join(" ").trim();
      if (!text) continue;

      // First real sentence only. A spoken paragraph is a paragraph nobody listens to.
      const cleaned = sayable(text, 200);
      const sentence = cleaned.split(/(?<=[.!?])\s/)[0] ?? cleaned;
      return sayable(sentence, 16);
    }
  } catch {
    /* No summary is fine - the per-action narration already covered the work. */
  }
  return "";
}

// --------------------------------------------------------------------------------------------------

function readStdin() {
  try {
    return fs.readFileSync(0, "utf8");
  } catch {
    return "";
  }
}

function main() {
  let payload;
  try {
    payload = JSON.parse(readStdin() || "{}");
  } catch {
    return;
  }

  const event = payload.hook_event_name ?? "";

  if (event === "SessionStart") {
    // Off by default: the greeting is the assistant talking about itself, which is exactly the
    // category Ajmal asked to lose. Set greetOnStart in voice-config.json to bring it back.
    if (readConfig().greetOnStart) say("A J A I Brain online.", VOICE_PROFILE);
    return;
  }

  if (event === "Notification") {
    const message = sayable(payload.message, 12);
    say(message ? `${message.replace(/[.!?]$/, "")}.` : "Waiting for you.", VOICE_PROFILE);
    return;
  }

  if (event === "Stop") {
    // The one line allowed to run past the caveman cap. Everything else is a label for an action
    // Ajmal can see happening in Revit; this is the answer, and half an answer is worse than none.
    const summary = finalSummary(payload.transcript_path);
    if (summary) say(summary, VOICE_PROFILE, SUMMARY_MAX_WORDS);
    return;
  }

  if (event === "PreToolUse") {
    const toolName = payload.tool_name ?? "";
    const input = payload.tool_input ?? {};

    const config = readConfig();
    if ((config.mute ?? []).includes(toolName)) return;

    // THE QUIET RULE - hear the DOING, not the THINKING.
    //
    // The first version narrated every tool call, which sounds thorough and is not: counted on one
    // real session, 22 lines were spoken and NOT ONE of them was about the model. Ajmal heard
    // "Reading glossary", "Searching for class void Task<", "Looking for files" - the assistant
    // reading its own notes - while the only calls that touched Revit were health checks. Worse, it
    // narrated its own narration ("Searching for glossary Bridge Service Editing Searching").
    //
    // So the line is drawn at the bridge: if it reaches the live model you hear it, otherwise it is
    // the assistant thinking and it stays silent. Set speakOnly to "all" to restore the old
    // everything-out-loud behaviour.
    if ((config.speakOnly ?? "revit") === "revit" && !toolName.startsWith(REVIT_TOOL_PREFIX)) return;

    // Never narrate the voice system operating on itself - it is noise, and on a Bash call that
    // touches these files it would describe its own plumbing instead of Ajmal's work.
    const raw = JSON.stringify(input);
    if (/voice[\\/](say|drainer|narrate-hook|voice-config)/i.test(raw)) return;

    const line = narrateTool(toolName, input);
    if (line) say(line, VOICE_PROFILE);
  }
}

main();
