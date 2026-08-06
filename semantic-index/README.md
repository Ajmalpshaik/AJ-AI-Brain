# Semantic search for the AJ AI Brain

Ask the Brain a question in plain English and get back the skill, knowledge note,
or script fragment that best answers it — without needing the exact keyword.

This sits **alongside** the existing keyword search (`tools/fragment-index.mjs`),
it does not replace it. They are good at different things:

| Use | When |
|---|---|
| `node tools/fragment-index.mjs --find color` | You know the word that will be in the file |
| `ask-brain "make ducts a different colour"` | You know what you want to *do*, not what it's called |

Nothing here touches Revit, the AJ AI Bridge, `mcp-server/`, or the compile
checker. It only reads `skills/`, `knowledge/`, `scripts/`, and the four
top-level guides (`AGENT-SPEC.md`, `START-HERE.md`, `README.md`, `SETUP.md`).

---

## The two commands

### Ask a question

```
"D:\Ajmal\AJ AI Brain\semantic-index\ask-brain.cmd" "how do I undo a mistake"
```

Options you can add:

- `--top 10` — show more results (default is 5)
- `--area fragment` — only C# script fragments
- `--area knowledge` — only knowledge notes
- `--area skill` — only skill workflows
- `--area guide` — only the top-level manuals

Use `--area fragment` when you specifically want code. Without it, the longer
prose files often rank above the fragments, because they contain more words
about the same subject.

### Where it is weakest — worth knowing before it surprises you

Questions shaped like **"how many X do I need in this room"** match poorly,
because the shape of the question ("counting devices in a room") carries more
weight than the one word that says *which* device. Tested 2026-08-06:
*"how many diffusers do I need in this room"* ranks the sprinkler files above
`ajtools-hvac-terminal-layout`, even though the sprinkler skill is the wrong
answer. The two score within half a point of each other.

Say the Revit word rather than the site word and it resolves — "air terminal"
instead of "diffuser", "sprinkler head" instead of "sprinkler". Or use the
keyword search, which is better at exactly this:

```
node tools/fragment-index.mjs --find diffuser
```

This is the honest boundary of a small offline model. Fixing it properly means
combining keyword and semantic scoring, which is a Phase 2 job, not a tweak.

### Rebuild the index

```
"D:\Ajmal\AJ AI Brain\semantic-index\index-brain.cmd"
```

Takes about **80 seconds**. You can also just double-click the file.

---

## When to rebuild — this is the important bit

The index is a **snapshot**. It does not notice when you edit the Brain.

Add a new fragment, write a new knowledge note, or change a skill, and the
search will keep returning the *old* text until you rebuild. So:

> **After you add or change anything in `skills/`, `knowledge/`, `scripts/`, or
> the four top-level guides, run `index-brain.cmd`.**

You do not need to remember what changed. Every run throws the whole index away
and builds it fresh from whatever is on disk right now. That is deliberate — a
partial update would leave ghost entries behind for files you deleted or
renamed, and a stale index is worse than no index. (The same reasoning the
repo's `.gitignore` already applies to `graphify-out/`.)

Running it more often than needed costs nothing but the 80 seconds.

---

## What is in this folder

| Item | What it is |
|---|---|
| `ask-brain.cmd` | Shortcut — ask a question |
| `index-brain.cmd` | Shortcut — rebuild the index |
| `brain_common.py` | Shared settings: which folders, where data goes |
| `brain_index.py` | Reads the Brain and builds the index |
| `brain_search.py` | Answers a question |
| `requirements.txt` | The one Python package needed |
| `venv/` | The private Python installation |
| `model-cache/` | The downloaded language model (~166 MB) |
| `chroma-db/` | The index itself |
| `pip-temp/`, `pip-cache/`, `run-temp/` | Scratch space, kept off the system temp folder |

Only the `.py`, `.cmd`, `.md` and `.txt` files are saved to git. Everything else
is rebuildable and is ignored, so the repo stays small.

---

## Two things worth knowing

**It works offline.** The model was downloaded once, on 2026-08-06, and lives in
`model-cache/`. Searching and rebuilding both work with no internet. Your Brain's
text never leaves this machine.

**Nothing is ever written to the system temp folder.** This is a hard rule, set
after `tools/verify-fragments-compile.ps1` wrote ~267 unsigned DLLs into `%TEMP%`
and Sophos flagged it as `ML/PE-A`. Every path used here — the database, the
model, pip's unpacking folder, and Python's own temp — is forced inside this one
folder by `brain_common.py`. Verified clean on 2026-08-06.

---

## If something breaks

**"collection does not exist"** — run `index-brain.cmd` once.

**Moved the Brain to another folder or another PC?** Open `brain_common.py` and
change the `BRAIN_ROOT` line at the top to the new path, then rebuild. If the
whole `semantic-index` folder came along, nothing else needs doing — including
the model, so it still works offline.

**Starting fresh on a new machine?** See `requirements.txt` — it has the setup
commands, including how to keep pip off `%TEMP%`.
