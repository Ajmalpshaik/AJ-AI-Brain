#!/usr/bin/env node
// Puts one line into the voice queue and gets out of the way.
//
// WHY THIS IS SO SMALL: this runs on the hot path of every single tool call, so it must never make
// Ajmal wait. It writes a tiny file, makes sure the speaking process is alive, and exits - typically
// in a few milliseconds. All the slow work (synthesis, playback) happens in drainer.py, which is a
// separate process that nothing waits on. If speaking breaks, the Revit job still runs.
//
// THE QUEUE IS JUST A FOLDER OF FILES on purpose: files named with a millisecond timestamp, so
// sorting by filename is sorting by time. That ordering is what stops parallel tool calls from
// talking over each other, and it needs no index, no database and no shared library - anything that
// can write a file can enqueue.
//
// Usage:
//   node tools/voice/say.mjs "Counting air terminals."

import fs from "node:fs";
import path from "node:path";
import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";

const voiceDir = path.dirname(fileURLToPath(import.meta.url));
const brainRoot = path.dirname(path.dirname(voiceDir));
const configPath = path.join(voiceDir, "voice-config.json");

// THE QUEUE LIVES INSIDE THE BRAIN, AND IT HAS TO. This is not a preference - it is the difference
// between this voice working and not existing at all.
//
// It used to live in %LOCALAPPDATA%\AJTools\voice\, which reads better and was wrong. Claude Code's
// hooks and shell tools write into a THROWAWAY OVERLAY for any path outside the project folder: the
// writing process is told the file exists, and the real disk never receives it. Proven 2026-08-11
// with one probe written to both locations at once -
//
//     %LOCALAPPDATA%\AJTools\voice\  ->  writer saw it, real disk had nothing
//     D:\Ajmal\AJ AI Brain\          ->  survived
//
// - so every line this file has ever queued went into a folder that does not exist, and Ajmal never
// once heard it speak. A whole day was spent debugging a drainer that was fine; the queue it was
// draining was the fiction.
//
// The portability argument that put it outside still stands, so `.voice-runtime/` is gitignored: it
// stays out of the copied package without having to be off the project drive.
export const runtimeDir = path.join(brainRoot, ".voice-runtime");
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
 * Prefer the Brain's own venv - it is where edge-tts gets installed - and inside it prefer
 * pythonw.exe, the interpreter that has no console.
 *
 * WHY pythonw AFTER ALL. An earlier version chose python.exe on purpose, believing the venv's pythonw
 * "would run --version and then silently refuse to execute an actual script". That was a
 * misdiagnosis. Retested 2026-08-11: the venv's pythonw runs `-c` code, runs drainer.py, and writes
 * files - it works. What python.exe actually cost was the very thing pythonw exists to prevent:
 * Ajmal got a black console window sitting on top of his Revit model. A `detached` child on Windows
 * is handed its own console, and windowsHide does not reliably suppress it for a venv launcher shim
 * (which re-executes the base interpreter as a second process that never saw the flag).
 *
 * That window is not a cosmetic problem. The entire point of a spoken narrator is to let him keep his
 * eyes on the model instead of on a screen - so covering the model with a console defeats the layer.
 */
function findPython() {
  const scripts = path.join(brainRoot, "semantic-index", "venv", "Scripts");
  const windowless = path.join(scripts, "pythonw.exe");
  if (fs.existsSync(windowless)) return windowless;

  const withConsole = path.join(scripts, "python.exe");
  if (fs.existsSync(withConsole)) return withConsole;

  // Not verified to exist - spawn failures fall through to the PowerShell path in the caller.
  return process.platform === "win32" ? "pythonw" : "python3";
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

/**
 * Longest a failing spawn is left alone before trying again. This - NOT the queue depth - is what
 * stops a machine that genuinely cannot start Python from retrying on every single line.
 */
const SPAWN_RETRY_COOLDOWN_MS = 30_000;
const cooldownPath = path.join(runtimeDir, ".spawn-cooldown");

function queueDepth() {
  try {
    return fs.readdirSync(queueDir).filter((name) => name.endsWith(".json")).length;
  } catch {
    return 0;
  }
}

function spawnOnCooldown() {
  try {
    const until = Number(fs.readFileSync(cooldownPath, "utf8").trim());
    return Number.isFinite(until) && Date.now() < until;
  } catch {
    return false;
  }
}

function startSpawnCooldown() {
  try {
    fs.mkdirSync(runtimeDir, { recursive: true });
    fs.writeFileSync(cooldownPath, String(Date.now() + SPAWN_RETRY_COOLDOWN_MS), "utf8");
  } catch {
    /* A cooldown we cannot record just means we retry sooner. Harmless. */
  }
}

/**
 * Throw away a backlog nobody is ever going to want to hear, keeping only the line being said now.
 *
 * Same principle drainer.py already applies when muted: a queue that went unspoken is stale, and
 * playing fifteen hours of it back in one burst is worse than losing it.
 */
function dropStaleQueue(keepPath) {
  try {
    for (const name of fs.readdirSync(queueDir)) {
      if (!name.endsWith(".json")) continue;
      const full = path.join(queueDir, name);
      if (full === keepPath) continue;
      try {
        fs.unlinkSync(full);
      } catch {
        /* Another process may have taken it. Fine either way. */
      }
    }
  } catch {
    /* Nothing to clear. */
  }
}

function ensureDrainer(text, profileName, currentPath) {
  if (drainerRunning()) return true;

  // A deep queue with NO drainer holding the lock does NOT mean spawning is futile - it means the
  // speaker is DEAD. The first version read it the other way round and simply gave up, which
  // deadlocked the whole voice on 2026-08-11: the queue only empties if a drainer runs, a drainer was
  // only started if the queue was shallow, so once it passed eight lines nothing could ever start
  // again. It stayed stuck for 15 hours and 139 lines, survived every restart (the jam is a folder on
  // disk, not a process), and left no error anywhere because failing to speak looks exactly like
  // having nothing to say. So: clear the dead backlog and START, and let the cooldown below - not the
  // depth - be the thing that stops a genuinely impossible spawn from retrying forever.
  if (queueDepth() >= STUCK_QUEUE_THRESHOLD) dropStaleQueue(currentPath);

  if (spawnOnCooldown()) return false;
  startSpawnCooldown();

  try {
    // Give the child REAL output handles pointed at a log file, rather than stdio:"ignore".
    // Two reasons, both learned the hard way on 2026-08-11:
    //   1. A venv python.exe is a launcher shim that re-executes the base interpreter, and handing it
    //      no std handles is exactly the shape of launch that failed here - it would report a pid and
    //      then do nothing at all.
    //   2. With "ignore", a Python-level crash goes to a null device and the only symptom is a voice
    //      that never speaks again. Pointed at a file, every future failure explains itself.
    let childStdio = "ignore";
    try {
      fs.mkdirSync(runtimeDir, { recursive: true });
      const logFd = fs.openSync(path.join(runtimeDir, "drainer-startup.log"), "a");
      childStdio = ["ignore", logFd, logFd];
    } catch {
      /* Fall back to no handles rather than not starting at all. */
    }

    const child = spawn(findPython(), [path.join(voiceDir, "drainer.py")], {
      detached: true,
      stdio: childStdio,
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

/**
 * Enqueue one spoken line. Returns quietly whether or not it worked - never throws.
 *
 * maxWords overrides the global cap for THIS line only. It exists for the closing summary: the
 * per-action cap is deliberately tight (eight words, caveman), and applying it to the one line that
 * carries the actual answer produced "Found forty two air terminals across whole of." - cut
 * mid-phrase, with the result thrown away. A short narrator is the point; a truncated answer is not.
 */
export function say(text, profileName = "jarvis", maxWords) {
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
    const payload = { text: line, profile: profileName };
    if (Number.isFinite(maxWords)) payload.maxWords = maxWords;
    fs.writeFileSync(tempPath, JSON.stringify(payload), "utf8");
    fs.renameSync(tempPath, finalPath);

    if (!ensureDrainer(line, profileName, finalPath)) speakWithoutQueue(line, profileName);
  } catch {
    speakWithoutQueue(line, profileName);
  }
}

// CLI use: node say.mjs "text" [profile]
if (process.argv[1] && path.resolve(process.argv[1]) === path.resolve(fileURLToPath(import.meta.url))) {
  say(process.argv[2], process.argv[3] || "jarvis");
}
