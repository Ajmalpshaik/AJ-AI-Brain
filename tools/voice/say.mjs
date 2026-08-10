#!/usr/bin/env node
// Puts one line into the voice queue and gets out of the way.
//
// WHY THIS IS SO SMALL: this runs on the hot path of every single tool call, so it must never make
// Ajmal wait. It writes a tiny file, makes sure the speaking process is alive, and exits - typically
// in a few milliseconds. All the slow work (synthesis, playback) happens in drainer.py, which is a
// separate process that nothing waits on. If speaking breaks, the Revit job still runs.
//
// THE QUEUE IS JUST A FOLDER OF FILES on purpose. Node writes to it from the Claude Code hooks and
// C# writes to it from the AJ Tools add-in (AiVoiceService.cs), and neither has to know the other
// exists or share a library. Files are named with a millisecond timestamp, so sorting by filename is
// sorting by time, which is what keeps the two voices from interleaving mid-sentence.
//
// Usage:
//   node tools/voice/say.mjs "Counting air terminals."            speaks as JARVIS (default)
//   node tools/voice/say.mjs "Forty two found." revit             speaks as the Revit voice

import fs from "node:fs";
import path from "node:path";
import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";

const voiceDir = path.dirname(fileURLToPath(import.meta.url));
const brainRoot = path.dirname(path.dirname(voiceDir));
const configPath = path.join(voiceDir, "voice-config.json");

// The queue and the audio cache live OUTSIDE the Brain, in the same machine-local folder the AJ Tools
// add-in already uses. Two reasons, both load-bearing:
//   1. The Brain is a portable knowledge package - "moving to another system means copying this
//      folder only" (START-HERE.md). Megabytes of generated MP3 and files that exist for 200ms are
//      not knowledge and have no business travelling with it.
//   2. It is the meeting point. The Revit add-in (AiVoiceService.cs) writes to this same folder
//      without needing to know where the Brain is checked out - which is what stops the two voices
//      from talking over each other.
export const runtimeDir = path.join(
  process.env.LOCALAPPDATA || process.env.HOME || voiceDir,
  "AJTools",
  "voice",
);
const queueDir = path.join(runtimeDir, "queue");
const lockPath = path.join(queueDir, ".drainer.lock");

export function readConfig() {
  try {
    return JSON.parse(fs.readFileSync(configPath, "utf8"));
  } catch {
    return {};
  }
}

/**
 * Prefer the Brain's own venv - it is where edge-tts gets installed.
 *
 * Deliberately python.exe and NOT pythonw.exe. pythonw looks like the obvious choice for a background
 * process, but the venv's pythonw stub would run `--version` and then silently refuse to execute an
 * actual script - no error, no exit code, nothing to find (2026-08-11). The console window it was
 * chosen to avoid is already suppressed by windowsHide below, so it bought nothing and cost a
 * silently dead voice.
 */
function findPython() {
  const venvPython = path.join(brainRoot, "semantic-index", "venv", "Scripts", "python.exe");
  if (fs.existsSync(venvPython)) return venvPython;
  // Not verified to exist - spawn failures fall through to the PowerShell path in the caller.
  return process.platform === "win32" ? "python" : "python3";
}

function drainerRunning() {
  try {
    const pid = Number(fs.readFileSync(lockPath, "utf8").trim());
    if (!Number.isInteger(pid) || pid <= 0) return false;
    process.kill(pid, 0); // Signal 0 tests for existence without touching the process.
    return true;
  } catch {
    return false;
  }
}

/** How many unspoken lines can pile up before we conclude nothing is draining them. */
const STUCK_QUEUE_THRESHOLD = 8;

function queueDepth() {
  try {
    return fs.readdirSync(queueDir).filter((name) => name.endsWith(".json")).length;
  } catch {
    return 0;
  }
}

function ensureDrainer(text, profileName) {
  if (drainerRunning()) return true;

  // A queue this deep with no drainer holding the lock is proof that previous spawns are failing -
  // no Python, a broken venv, a machine where it cannot start. Left alone this is SILENT: lines pile
  // up forever and the assistant simply never speaks again, with nothing on screen to explain why.
  // So stop trying and take the path that always works.
  if (queueDepth() >= STUCK_QUEUE_THRESHOLD) return false;

  try {
    const child = spawn(findPython(), [path.join(voiceDir, "drainer.py")], {
      detached: true,
      stdio: "ignore",
      windowsHide: true,
      cwd: voiceDir,
    });

    // A missing or unrunnable executable gives no pid and raises asynchronously rather than throwing,
    // so a plain try/catch around spawn() reports success for a process that never existed.
    if (!child.pid) return false;
    child.on("error", () => speakWithoutQueue(text, profileName));

    child.unref(); // Let this process exit immediately; the drainer outlives it.
    return true;
  } catch {
    return false;
  }
}

/**
 * Last-ditch path for a machine with no usable Python: talk through the Windows built-in voice
 * directly. There is no queue here so two lines can overlap, but a rare overlap beats silence.
 */
function speakWithoutQueue(text, profileName) {
  try {
    const config = readConfig();
    const profile = (config.profiles || {})[profileName] || {};
    const voice = (profile.fallback || "Microsoft David Desktop").replace(/'/g, "''");
    const rate = Number.isFinite(profile.fallbackRate) ? profile.fallbackRate : 2;
    const safe = String(text).replace(/'/g, "''");
    const script =
      `Add-Type -AssemblyName System.Speech; ` +
      `$s = New-Object System.Speech.Synthesis.SpeechSynthesizer; ` +
      `try { $s.SelectVoice('${voice}') } catch { }; ` +
      `$s.Rate = ${rate}; $s.Speak('${safe}'); $s.Dispose()`;
    const child = spawn("powershell", ["-NoProfile", "-NonInteractive", "-Command", script], {
      detached: true,
      stdio: "ignore",
      windowsHide: true,
    });
    child.unref();
  } catch {
    /* Speaking is a nice-to-have. It must never take the actual work down with it. */
  }
}

let sequence = 0;

/** Enqueue one spoken line. Returns quietly whether or not it worked - never throws. */
export function say(text, profileName = "jarvis") {
  const line = String(text || "").trim();
  if (!line) return;

  // Dry run prints the exact sentence instead of speaking it, so the wording can be checked - and
  // regression-tested by tools/voice/test-narration.mjs - without anyone having to sit and listen.
  if (process.env.AJ_VOICE_DRYRUN) {
    process.stdout.write(`[${profileName}] ${line}\n`);
    return;
  }

  const config = readConfig();
  if (config.enabled === false) return;

  try {
    fs.mkdirSync(queueDir, { recursive: true });

    // Millisecond stamp keeps filename order == time order. The counter and random suffix only break
    // ties between lines enqueued inside the same millisecond.
    const stamp = String(Date.now()).padStart(13, "0");
    const unique = `${stamp}-${String(sequence++).padStart(4, "0")}-${Math.random().toString(36).slice(2, 8)}`;
    const finalPath = path.join(queueDir, `${unique}.json`);

    // The temp file MUST live in the queue folder, not in %TEMP%. Renaming across volumes fails on
    // Windows (EXDEV), and %TEMP% is on C: while the Brain lives on D: - which silently sent every
    // single line down the offline-fallback path on 2026-08-11 until this was found. Atomic rename
    // only means anything within one filesystem anyway. The drainer reads *.json, so a .tmp file
    // sitting alongside is invisible to it.
    const tempPath = path.join(queueDir, `${unique}.tmp`);

    // Write then rename in: the drainer must never read a half-written line.
    fs.writeFileSync(tempPath, JSON.stringify({ text: line, profile: profileName }), "utf8");
    fs.renameSync(tempPath, finalPath);

    if (!ensureDrainer(line, profileName)) speakWithoutQueue(line, profileName);
  } catch {
    speakWithoutQueue(line, profileName);
  }
}

// CLI use: node say.mjs "text" [profile]
if (process.argv[1] && path.resolve(process.argv[1]) === path.resolve(fileURLToPath(import.meta.url))) {
  say(process.argv[2], process.argv[3] || "jarvis");
}
