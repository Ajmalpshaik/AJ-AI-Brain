# Why a black window opens over Revit — and the one rule that stops it

**Symptom.** A black, empty terminal window appears on top of whatever Ajmal is working on. His
words, 2026-08-21: *"evry time i chat what is this coming ??"* Closing it makes it come straight
back on the next message, which is what makes it feel endless.

**It is never cosmetic.** It sits over the model, and closing the window **kills the process inside
it** — so closing the search server's window is what makes the next message start a new one.

## What actually causes it

This Brain's Python lives in a venv built on the **Microsoft Store** Python
(`semantic-index/venv/pyvenv.cfg` → `WindowsApps\PythonSoftwareFoundation.Python.3.11`). That makes
`venv\Scripts\python.exe` a **launcher shim, not an interpreter**: it re-executes the base
interpreter as a **second process**.

That second process never saw the first one's creation flags. So:

- `DETACHED_PROCESS` given to the shim is honoured by the shim — and **lost** by the real
  interpreter, which then allocates a console of its own.
- On Windows 11, Windows Terminal is the default console host, so that console is a **visible
  window**, not the old invisible conhost.

Proof, measured 2026-08-21: `conhost.exe` parented directly to the server's `python3.11.exe`, with a
`WindowsTerminal.exe` created in the same second.

## The two rules

1. **Any background/detached Python on Windows starts `pythonw.exe`, never `python.exe`.**
   `pythonw` is a GUI-subsystem binary — Windows allocates no console for it at all, and the shim
   re-executes the base `pythonw`, so nothing has to survive a flag handoff. Keep
   `DETACHED_PROCESS | CREATE_NEW_PROCESS_GROUP` as well; it is right for other reasons (Ctrl-C
   isolation), it just is not what keeps the window away.
2. **Any Node → Python (or Node → Node) spawn inside a hook passes `windowsHide: true`.**
   Hooks run with no console of their own, so a console child gets a **new** console — a Terminal
   window that flashes on every message. Node's default is `windowsHide: false`.

Give a detached child **real** stdio handles (a log file or `DEVNULL`), never nothing at all: a shim
handed no handles has reported a pid and then done nothing.

## Where these rules are applied

| File | What it starts |
|---|---|
| [`semantic-index/brain_client.py`](../semantic-index/brain_client.py) | the warm search server — `windowless_python()` |
| [`tools/voice/say.mjs`](../tools/voice/say.mjs) | the speech drainer — `findPython()` prefers `pythonw` |
| [`tools/auto-search-hook.mjs`](../tools/auto-search-hook.mjs) | cold-path search (every message) |
| [`tools/reindex-run.mjs`](../tools/reindex-run.mjs) | index rebuild at end of turn |
| [`tools/graph-rebuild.mjs`](../tools/graph-rebuild.mjs) · [`tools/score-check.mjs`](../tools/score-check.mjs) | graph rebuild, score check |

## How to check it is really fixed

A process with no window has **no `conhost.exe` child**. Run this after starting anything in the
background — it prints nothing when the fix holds:

```powershell
Get-CimInstance Win32_Process -Filter "Name='conhost.exe'" |
  Where-Object { $_.ParentProcessId -eq <pid> }
```

`Get-CimInstance Win32_Process -Filter "Name='WindowsTerminal.exe'"` returning nothing is the other
half: no terminal host means no window was handed out.

## It has bitten twice

- **2026-08-11** — the voice drainer, launched with `python.exe`. Fixed by switching to `pythonw`.
- **2026-08-21** — the warm search server, launched with `sys.executable` (the shim) and detached.
  The 2026-08-11 lesson lived only as a comment in the voice code, so the new code did not inherit
  it. That is why this note exists instead of a third comment.
