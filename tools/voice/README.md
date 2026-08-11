# AJ AI Voice — hearing what the AI is doing

A JARVIS-style spoken narration of live Revit work, in two voices, so you can keep your eyes on the
model instead of the chat window.

Built 2026-08-11 at Ajmal's request: *"i need also one jarvis or some voice mode also need to come
that what is the ai is doing in short reply in voice"*.

## One voice

| Voice | What it says | If offline |
|---|---|---|
| **Ryan** (British male) | the **intent** before each action — "Counting air terminals." — and the **answer** at the end | Microsoft David |

**There used to be a second voice, and it is gone — code and all.** Sonia (British female) came from
the AJ Tools add-in reading back what Revit returned. Built and deleted on 2026-08-11, the same day
Ajmal first heard it work:

> *"totally remove that female voice feature, only men voice … remove everything, even the code also
> related to this"*

He is right. The design assumed the two voices carried different news — intent versus result — but
Ryan already announces the job and then reads the answer at the end, so the second voice was another
person confirming something you had just been told. **A second voice earns its place only when it says
something the first one cannot.**

An off-by-default toggle was built first and he asked for removal instead, which was the better call:
**a feature nobody wants is not improved by making it optional** — it just leaves dead code and a
switch to explain. `AiVoiceService.cs` is deleted and `McpBridgeService` (v1.10.0) no longer calls it.
Nothing in AJ Tools speaks any more.

**What you give up:** the per-action result mid-job. Ryan says what is about to happen and what
happened overall; he does not read out each number as it comes back. On a long batch you hear the plan
and the total, not a running commentary.

> **One rebuild of the add-in is needed** before Revit stops speaking — the deleted code is still
> inside the DLL currently loaded. Nothing else about this layer depends on it.

### The lesson worth keeping from the attempt

The first mute was Brain-side: drop the add-in's lines out of the shared queue. **It passed its own
unit test and silenced nothing.** The add-in only queues when it can see a live speaker and otherwise
speaks straight through Windows — and that fallback path was never covered by the test. *Verify a
cross-component switch against the fallback path, not the happy path; the fallback is where an "off"
switch goes to die.*

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

**Why the runtime lives INSIDE this repo — and why the opposite was fatal.** The queue, audio cache
and log sit in `.voice-runtime/` (gitignored), not in `%LOCALAPPDATA%`.

They started in `%LOCALAPPDATA%\AJTools\voice\` for good-sounding reasons: keep megabytes of MP3 out
of a folder meant to be copied, and give both sides one meeting point. It made the assistant's voice
**structurally impossible**. Claude Code writes any path outside the project folder into a throwaway
overlay — the writing process is told the file exists and the real disk never receives it. Proven
2026-08-11 by writing one probe to both places at once:

| Written by a Claude Code shell to | Writer saw | Real disk |
|---|---|---|
| `%LOCALAPPDATA%\AJTools\voice\` | exists | **nothing** |
| `D:\Ajmal\AJ AI Brain\` | exists | exists |

So every line the hooks ever queued went into a folder that does not exist. Ajmal heard only the Revit
add-in — a real process on the real disk — and never once heard the assistant. A full day went into
debugging a drainer that was working; **the queue it was draining was the fiction.**

**The add-in's queue stays in `%LOCALAPPDATA%`** — it is real for a real Revit process, and moving it
would mean recompiling the add-in and closing the model. So `drainer.py` reads **both** folders and
merges them. Ordering survives the split because filenames carry a millisecond timestamp, which sorts
correctly regardless of which folder a line came from. The drainer also publishes its lock into the
add-in's folder, so `AiVoiceService` can see a live speaker and queue through it instead of falling
back to Windows' own voice — which is what made Sonia sound robotic and talk over things.

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

## How much it talks — the quiet rule

Ajmal, 2026-08-11: *"no too much reply, main things only like caveman"*.

**It speaks only what reaches the live model.** Counting, listing, isolating, moving, deleting,
running a script — plus Revit's answer, the closing summary, and any point where it is stuck waiting
on you. Reading files, searching, loading skills, editing the Brain's own notes: silent.

The first version narrated every tool call, which sounds thorough and is not. Counted on one real
session: **22 spoken lines, none of them about the model** — "Reading glossary", "Searching for class
void Task<", and at one point narrating its own narration. The setting is `speakOnly` in
`voice-config.json`; set it to `"all"` to get the old behaviour back.

Wording is caveman too — filler stripped (`the`, `a`, `just`, `really`), capped at `maxWords`.
**Negatives, numbers and units are never stripped**: flipping the meaning of a spoken warning costs
far more than a saved word. Two deliberate exceptions to the cap:

- **Delete and move say the count** — "Deleting 12 ducts", not "Deleting elements" — but only when
  the call names exact Element Ids. A category-filtered delete has no count until Revit runs it.
- **The closing summary gets 16 words**, not 8. It carries the answer, and an answer cut in half is
  worse than no answer.

The filler-stripping lives in `drainer.py`, which is the one process **both** voices pass through.
That is why the Revit add-in's voice got shorter with no C# change and no Revit restart.

## Known state

- Proven: neural synthesis, mp3 playback, the wording and the speak/stay-silent decision for every
  tool (`node tools/voice/test-narration.mjs` — 22 cases, both stages), hook wiring, the add-in build.
- **Confirmed working end to end on a real desktop, 2026-08-11.** Ajmal heard both voices on a live
  two-count job (505 ducts, 121 air terminals): the drainer starts from a hook, synthesises through
  the neural voice, and plays. Nothing about this layer is unproven any more.
- The thing that hid it for a day was never the code — it was believing a directory listing. When
  something here goes quiet, get the filesystem answer from Python or from Ajmal's own terminal, and
  suspect the queue LOCATION before the queue logic.

## The deadlock that hid all of this (2026-08-11)

`say.mjs` treated *"queue deeper than 8 with no drainer"* as proof that spawning was futile, and gave
up. It is the opposite — it means the speaker is **dead**. The queue only empties if a drainer runs,
and a drainer was only started if the queue was shallow, so once it passed eight lines **nothing could
ever start again**. It stayed stuck for 15 hours and 139 lines.

It survived every restart, because the jam is a folder on disk and not a process, and it produced no
error anywhere — a voice that fails to speak looks exactly like a voice with nothing to say. Now a
deep queue with no lock clears the dead backlog and starts the speaker; a **time cooldown**, not the
queue depth, is what stops a genuinely impossible spawn from retrying on every line.
