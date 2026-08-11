#!/usr/bin/env node
// The switch. Everything a person needs to do to the voice, without opening a config file.
//
// WHY IT EXISTS: a narrator you cannot silence in one second is a narrator you uninstall. `voice off`
// has to work while it is mid-sentence, from any folder, with nothing to remember - so it writes one
// flag that drainer.py re-reads before every single line, and the queued backlog is dropped rather
// than played back at you all at once when you turn it on again.
//
// Usage (from the Brain folder):
//   tools\voice\voice.cmd off              silence it
//   tools\voice\voice.cmd on               bring it back
//   tools\voice\voice.cmd test             hear one line, check your speakers
//   tools\voice\voice.cmd status           is it on, is it warm, is the good voice available
//   tools\voice\voice.cmd list             every neural voice you could switch to
//   tools\voice\voice.cmd setup            install the neural voice into the Brain's venv
//   tools\voice\voice.cmd say "some words"

import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { say, runtimeDir } from "./say.mjs";

const voiceDir = path.dirname(fileURLToPath(import.meta.url));
const brainRoot = path.dirname(path.dirname(voiceDir));
const configPath = path.join(voiceDir, "voice-config.json");
const lockPath = path.join(runtimeDir, "queue", ".drainer.lock");
const cacheDir = path.join(runtimeDir, "cache");

const venvPython = path.join(brainRoot, "semantic-index", "venv", "Scripts", "python.exe");
const python = fs.existsSync(venvPython) ? venvPython : "python";

const command = (process.argv[2] || "status").toLowerCase();

function readConfig() {
  return JSON.parse(fs.readFileSync(configPath, "utf8"));
}

/** Rewrite only the enabled flag, so Ajmal's comments and voice choices survive untouched. */
function setEnabled(enabled) {
  const raw = fs.readFileSync(configPath, "utf8");
  const updated = raw.replace(/"enabled"\s*:\s*(true|false)/, `"enabled": ${enabled}`);
  fs.writeFileSync(configPath, updated, "utf8");
}

function drainerRunning() {
  try {
    process.kill(Number(fs.readFileSync(lockPath, "utf8").trim()), 0);
    return true;
  } catch {
    return false;
  }
}

function neuralInstalled() {
  const probe = spawnSync(python, ["-c", "import edge_tts"], { encoding: "utf8" });
  return probe.status === 0;
}

switch (command) {
  case "off": {
    setEnabled(false);
    console.log("Voice off. Nothing will be spoken until you run: tools\\voice\\voice.cmd on");
    break;
  }

  case "on": {
    setEnabled(true);
    console.log("Voice on.");
    say("Voice on.", "jarvis");
    break;
  }

  case "test": {
    if (!readConfig().enabled) {
      console.log("Voice is currently OFF - turning it on for this test would be surprising, so it stayed off.");
      console.log("Run: tools\\voice\\voice.cmd on");
      break;
    }
    console.log("Speaking one line. You should hear ONE voice - there is only one.");
    say("Voice test. If you can hear this, the voice is working.", "jarvis");
    console.log("If you heard nothing, check your speakers, then run: tools\\voice\\voice.cmd status");
    break;
  }

  case "status": {
    const config = readConfig();
    const cached = fs.existsSync(cacheDir) ? fs.readdirSync(cacheDir).filter((f) => f.endsWith(".mp3")).length : 0;
    const neural = neuralInstalled();

    console.log(`  Voice          ${config.enabled ? "ON" : "OFF"}`);
    console.log(`  Speaker        ${drainerRunning() ? "warm (fast)" : "cold - next line costs about 1 extra second"}`);
    console.log(`  Good voice     ${neural ? "available" : "NOT installed - run: tools\\voice\\voice.cmd setup"}`);
    console.log(`  Voice used     ${config.profiles?.jarvis?.neural} (offline: ${config.profiles?.jarvis?.fallback})`);
    console.log(`  Cached lines   ${cached} (these play instantly and work with no internet)`);
    break;
  }

  case "list": {
    if (!neuralInstalled()) {
      console.log("The neural voice is not installed yet. Run: tools\\voice\\voice.cmd setup");
      break;
    }
    console.log("English neural voices you can put in voice-config.json:\n");
    const result = spawnSync(python, ["-m", "edge_tts", "--list-voices"], { encoding: "utf8" });
    const rows = (result.stdout || "")
      .split("\n")
      .filter((line) => /^en-(GB|US|AU|IE)-/.test(line.trim()))
      .map((line) => "  " + line.trim());
    console.log(rows.join("\n") || "  (could not reach the voice list - check your internet)");
    break;
  }

  case "setup": {
    console.log("Installing the neural voice into the Brain's Python venv...");
    const result = spawnSync(python, ["-m", "pip", "install", "edge-tts", "--disable-pip-version-check"], {
      encoding: "utf8",
      stdio: "inherit",
    });
    console.log(result.status === 0 ? "\nDone. Run: tools\\voice\\voice.cmd test" : "\nInstall failed - the built-in Windows voice still works.");
    break;
  }

  // Checks every link in the chain and names the one that is broken. Worth having because the voice
  // fails SILENTLY by nature - there is no error message for "did not speak", and every stage looks
  // identical from the outside. Run this in a real terminal window rather than through any tool that
  // sandboxes the filesystem, or it will report on a throwaway copy of the disk instead of the disk.
  case "doctor": {
    const results = [];
    const check = (name, ok, detail) => results.push({ name, ok, detail });

    const config = readConfig();
    check("Config readable", true, `voice is ${config.enabled ? "ON" : "OFF"}`);

    const version = spawnSync(python, ["--version"], { encoding: "utf8" });
    check("Python runs", version.status === 0, version.status === 0 ? (version.stdout || version.stderr).trim() : `cannot run ${python}`);

    check("Neural voice installed", neuralInstalled(), neuralInstalled() ? "edge-tts present" : "run: voice.cmd setup");

    const queueDir = path.join(runtimeDir, "queue");
    let nodeCanWrite = false;
    try {
      fs.mkdirSync(queueDir, { recursive: true });
      const probe = path.join(runtimeDir, "doctor-probe.tmp");
      fs.writeFileSync(probe, "ok");
      fs.unlinkSync(probe);
      nodeCanWrite = true;
    } catch { /* reported below */ }
    check("Node can write runtime folder", nodeCanWrite, runtimeDir);

    // Node writing here proves nothing about Python: they are different executables and antivirus,
    // policy and folder permissions all discriminate by executable. Python is the one that has to
    // write the lock, the cache and the log, so Python is the one that gets tested.
    const pyProbe = "import os,sys\np=os.path.join(sys.argv[1],'doctor-py.tmp')\nopen(p,'w').write('ok')\nos.remove(p)\nprint('ok')";
    const pyWrite = spawnSync(python, ["-c", pyProbe, runtimeDir], { encoding: "utf8" });
    const pyWriteOk = pyWrite.status === 0 && /ok/.test(pyWrite.stdout || "");
    check("Python can write runtime folder", pyWriteOk, pyWriteOk ? "" : ((pyWrite.stderr || "").trim().split("\n").pop() || "blocked"));

    const depth = fs.existsSync(queueDir) ? fs.readdirSync(queueDir).filter((f) => f.endsWith(".json")).length : 0;
    check("Queue is draining", depth < 8, depth === 0 ? "empty" : `${depth} line(s) waiting - a big backlog means nothing is speaking them`);

    const countCache = () => (fs.existsSync(cacheDir) ? fs.readdirSync(cacheDir).filter((f) => f.endsWith(".mp3")).length : 0);
    const countQueue = () => (fs.existsSync(queueDir) ? fs.readdirSync(queueDir).filter((f) => f.endsWith(".json")).length : 0);
    const cacheBefore = countCache();
    const queueBefore = countQueue();
    say("Voice check. If you can hear this, the voice is working.", "jarvis");

    const sleep = (ms) => Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, ms);
    let synthesised = false;
    let drained = false;
    for (let waited = 0; waited < 20000 && !synthesised; waited += 400) {
      sleep(400);
      synthesised = countCache() > cacheBefore;
      // Nothing left in the queue means something consumed the line, even if it left no audio file
      // behind - which is exactly what the offline Windows-voice fallback does.
      drained = countQueue() <= queueBefore;
    }

    if (synthesised) check("Speaker spoke a line", true, "with the good neural voice - did you hear it?");
    else if (drained) check("Speaker spoke a line", true, "with the offline Windows voice (the neural one did not run)");
    else check("Speaker spoke a line", false, "the line is still sitting in the queue - nothing picked it up");

    console.log("");
    for (const r of results) {
      console.log(`  ${r.ok ? "PASS" : "FAIL"}  ${r.name.padEnd(32)} ${r.detail}`);
    }
    const failed = results.filter((r) => !r.ok);
    console.log(failed.length ? `\n  ${failed.length} problem(s). The first FAIL above is the one to fix.` : "\n  All good - the voice works end to end.");
    break;
  }

  case "say": {
    say(process.argv[3] || "Nothing to say.", process.argv[4] || "jarvis");
    break;
  }

  default:
    console.log("Usage: voice.cmd  on | off | test | status | doctor | list | setup | say \"text\"");
}
