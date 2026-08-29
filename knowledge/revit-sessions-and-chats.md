# Running more than one Revit, and more than one chat

Plain-English note for Ajmal, written 2026-08-27 from his own questions. The agent-facing rules live in
[`live-model/core.md`](live-model/core.md) under "Bridge basics" — this file is the human version.

## The short answer

- **Open as many Revits as the PC can handle.** The bridge sets no limit.
- **One chat for one Revit.** This is the only rule that really matters.
- **Mixed versions are fine** — Revit 2020 and Revit 2025 open together, each with its own chat. The
  version makes no difference to the AI.

## Setting it up

1. Open the Revits you want.
2. **In each one, click Connect AJ AI Bridge** in the AJ AI pane. A Revit that was never connected does
   not show up at all — it is invisible to every chat.
3. Start a chat for each Revit. When more than one is connected the chat will stop and ask which one to
   use; answer once and it stays on that Revit.

## What happens in each case

| Situation | What happens |
|---|---|
| One Revit connected | Taken automatically. Never asks. Exactly as it always worked. |
| Two or more connected | **Nothing is sent to Revit at all** until you say which one. The AI is not allowed to guess. |
| You have chosen one | Every command in that chat goes there. Opening a third Revit later does **not** ask again. |
| The Revit you chose closes | Everything stops and says so. It never slides onto a different project. |
| Chat A → Revit 1, chat B → Revit 2 | **Works perfectly.** The two never disturb each other. |
| Chat A **and** chat B → the **same** Revit | **They fight.** Whichever speaks last takes the Revit over and cuts the other one off. |

That last row is the reason for the standing rule *"don't go to Revit, another session is running."* It
is not a queue — a second chat does not wait its turn, it pushes the first one out. Nothing is
corrupted, because each chat reconnects on its next call, but a job running mid-way gets chopped.

## Can I keep modelling while the AI works?

**Not in the same Revit — you take turns.** Revit does one thing at a time, and the AI's script runs on
the same thread that draws the screen. While it runs, Revit is genuinely frozen. No add-in can change
that; it is how Revit itself is built.

In practice it barely shows:

- Most jobs finish in **one or two seconds**, with a banner across the top while they run.
- If you are **mid-command** — a wall half-drawn, a dialog open — the AI **cannot interrupt you**. It
  waits until you finish.

**The real danger is not the freeze, it is the stale read:** the AI reads the model, you change
something, and a later step acts on the old picture. That is why every step re-reads instead of
trusting what it saw a minute ago.

**If you want to genuinely work while the AI works, open a second Revit** — your model in one, the AI's
job in the other. Real side-by-side work only exists across two Revits, never inside one.

## Two traps worth knowing

- **The session list shows a stale file name.** When the chat lists the connected Revits, the file name
  shown is whatever was in front when that Revit first connected — it does not update when you switch
  projects. Proven live 2026-08-20. **Identify a session by its number (pid), not by the name shown.**
- **One Revit can hold several projects open.** Picking the Revit is only half of it — commands land on
  whichever project window is in front, and that changes when you click. For anything that *writes*,
  say which project and it gets pinned by name.

## How many is sensible

There is no technical limit, only memory. On this machine (64 GB) **three or four Revits at once is
comfortable**; five works if the models are small. Watch the PC, not a rule.

## Why it works this way

Each Revit hosts its own private line named after its process number, and advertises itself in
`%APPDATA%\AJTools\bridges\<pid>.json`. Before 2026-08-20 every Revit tried to use one shared line and
the second one simply refused to start. Inside one Revit that line has two slots — one serving the
current chat, one already listening — which is what makes a new chat take over instantly instead of
waiting.
