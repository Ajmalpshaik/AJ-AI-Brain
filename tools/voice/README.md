# AJ AI Voice — hearing what the AI is doing

A JARVIS-style spoken narration of live Revit work, in two voices, so you can keep your eyes on the
model instead of the chat window.

Built 2026-08-11 at Ajmal's request: *"i need also one jarvis or some voice mode also need to come
that what is the ai is doing in short reply in voice"*.

## The two voices

| Voice | Who is speaking | What it says | If offline |
|---|---|---|---|
| **Ryan** (British male) | the AI assistant, before it acts | the **intent** — "Counting air terminals." "Reading the families note." | Microsoft David |
| **Sonia** (British female) | the Revit add-in, after Revit answers | the **result** — "Forty two." "Done." "That failed." | Microsoft Zira |

They are split by *role*, not duplicated. If both narrated every action you would hear the same
sentence twice, half a second apart, in two voices. Ryan says what is about to happen; Sonia says
what came back.

## Controlling it

```bash
tools\voice\voice.cmd off
```

`on` · `off` · `test` · `status` · `doctor` · `list` · `setup` · `say "some words" [jarvis|revit]`

`doctor` is the one to run when it goes quiet — it checks every link in the chain and names the one
that is broken. **Run it in a real terminal window**, not through a tool that sandboxes the
filesystem, or it reports on a throwaway copy of the disk (see *Testing gotcha* below).

## How a sentence gets spoken

```
Claude Code hook ─┐
                  ├─→  queue folder  ─→  drainer.py  ─→  neural voice  ─→  speakers
Revit add-in    ──┘   (one file per     (one at a       (cached mp3)
                       line, named       time, warm)
                       by timestamp)
```

- **`narrate-hook.mjs`** turns a tool call into one short English sentence. This is the part that
  makes it worth listening to: it reads the category name off a count, the file name off a read, the
  plain-English `description` every shell command already carries. You hear *"Counting air
  terminals"*, not *"running command"*.
- **`say.mjs`** drops that sentence into the queue and exits in milliseconds. It never waits for
  speech, so narration cannot slow a Revit job down.
- **`drainer.py`** is the only thing that actually speaks. One process, one line at a time, in
  timestamp order — which is what stops the two voices overlapping.
- **`AiVoiceService.cs`** (in the AJ Tools add-in) writes into the same queue folder.

## Design decisions worth knowing

**Why a long-lived drainer instead of speaking on the spot.** Measured on this machine: importing the
neural voice library costs ~1.4 s, the synthesis itself only ~1.1 s. Paying that import per line puts
the narration ~3 s behind the work. The drainer imports once, stays warm while you are working, and
exits by itself after 45 s idle. Nothing to start, nothing to remember, nothing left running.

**Why the queue is a folder of files.** Node writes to it from the Claude Code hooks and C# writes to
it from Revit, and neither has to know the other exists or share a library. Filenames carry a
millisecond timestamp, so sorting by name is sorting by time.

**Why the runtime lives outside this repo.** The queue and audio cache sit in
`%LOCALAPPDATA%\AJTools\voice\`, not here. The Brain is a portable knowledge package — "moving to
another system means copying this folder only" — and megabytes of generated MP3, plus files that
exist for 200 ms, are not knowledge. It is also the one folder both the Brain and the add-in can find
without either knowing where the other is installed.

**Why it never goes silent.** The neural voice needs internet and a Python package in a gitignored
venv, so on a fresh copy of the Brain neither may exist. Both are treated as upgrades: without them
it speaks through the built-in Windows voice, which is on every Windows machine. Repeat lines
("Done.") are cached as audio the first time and play instantly and offline forever after.

**Why nothing here can break a Revit job.** Every failure in the whole chain is caught and swallowed,
and the add-in queues speech on a background thread. A model edit must never fail because the machine
could not say a word about it.

## Testing gotcha — read this before debugging

**Claude Code's Bash and PowerShell tools are sandboxed.** Processes they spawn write into a
throwaway overlay: Python reports the file written and `os.path.exists` says `True`, while the very
same shell session cannot see it. Hours went into chasing a "bug" on 2026-08-11 that was only ever
the sandbox showing a different disk.

If you are verifying voice behaviour, run `voice.cmd doctor` from a **real terminal window**. Treat
any filesystem result from a sandboxed tool as unproven.

## Known state

- Proven: neural synthesis, mp3 playback, the wording for every tool (`node
  tools/voice/test-narration.mjs`), hook wiring, the add-in build (0 warnings, 0 errors).
- Not yet confirmed on a real desktop: the drainer auto-starting from a hook and speaking end to end.
  `voice.cmd doctor` gives the answer in one line.
