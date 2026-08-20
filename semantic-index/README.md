# Plain-English search for the AJ AI Brain

Ask the Brain a question the way you would ask a person, and get back the skill,
knowledge note, or script fragment that answers it — without needing the exact
keyword.

This sits **alongside** the existing keyword search (`tools/fragment-index.mjs`),
it does not replace it, and it never modifies it. Three ways to look things up:

| Use | When |
|---|---|
| **`ask-brain-hybrid`** | **Almost always. Matches meaning AND exact words.** |
| `ask-brain` | Semantic only. Kept as the baseline, to compare against. |
| `node tools/fragment-index.mjs --find color` | C# fragments only, exact word, with PROVEN status |

Nothing here touches Revit, the AJ AI Bridge, or the compile checker. It only
reads `skills/`, `knowledge/`, `scripts/`, the five top-level guides
(`AGENT-SPEC.md`, `START-HERE.md`, `README.md`, `SETUP.md`, `CLAUDE.md`), and
one named file from `mcp-server/`: `tools/README.md`, the reference table of
the bridge's 17 native tools — the doc, never the bridge's JS code.

---

## The commands

### Ask a question — use this one

```
"D:\Ajmal\AJ AI Brain\semantic-index\ask-brain-hybrid.cmd" "how do I undo a mistake"
```

Options you can add:

- `--top 10` — show more results (default is 5)
- `--area fragment` — only C# script fragments
- `--area knowledge` — only knowledge notes
- `--area skill` — only skill workflows
- `--area guide` — only the top-level manuals
- `--explain` — show why each result ranked where it did
- `--no-fragment-tool` — skip `fragment-index.mjs` (works without Node)

Each result says **how it was found**:

```
found by: meaning #3 + words #1
```

*meaning* is its position by what you meant; *words* is its position by the
exact words you typed. **Appearing high in both is the strongest signal.** A
result found only by meaning may be a loose association; one found only by words
may just share vocabulary. Fragments also show `[PROVEN]` or `[unproven]`, read
live from `fragment-index.mjs`.

### Why hybrid exists — the problem it fixes

Semantic search alone confused two different jobs. Asked *"how many diffusers do
I need in this room"*, it ranked the **sprinkler** files first, because the shape
of the question — counting devices in a room — outweighed the single word saying
*which* device. Both jobs are device-counting; only one is about diffusers.

Hybrid fixes it by adding exact-word matching, weighted by **how rare each word
is**. "diffuser" appears in 12 files, "room" in 57 — so "diffuser" carries far
more weight, and the right skill wins.

Measured 2026-08-06, same index, same question:

| Rank | Semantic only | Hybrid |
|---|---|---|
| 1 | `nfpa13-sprinkler-spacing.md` ✗ | **`ajtools-hvac-terminal-layout`** ✓ |
| 2 | `ajtools-fire-sprinkler-layout` ✗ | `ajtools-fire-sprinkler-layout` |
| 3 | `ajtools-hvac-terminal-layout` ✓ | `live-model/hvac-terminals.md` ✓ |

### How reliable is it, really — measured, not claimed

The before/after above is the case hybrid was *built* for, so it proves little on its own. This is the
number that counts: **questions written by independent testers** in a modeller's own words, across
HVAC, fire, tagging, sheets/views, general Revit work and the Brain itself.

| Result | Count |
|---|---|
| #1 was the best answer | 13 |
| #1 was useful, though not the best file | 3 |
| **#1 was wrong** | **8** |

Three mechanical causes were found and fixed after that run (2 of the 8 now correct, 1 borderline
improved, 0 regressions). **The rest are vocabulary, and re-ranking cannot fix them:**

| You type | You get | You wanted |
|---|---|---|
| "add 4 more **floor levels**" | `create-floor.cs` — the slab creator | `create-levels.cs` |
| "how many **light fitting**" | matched "**light** hazard" | `action-count-by-group.cs` |
| "take my door schedule **out to excel**" | the glossary | `action-export-schedule-to-csv.cs` |

The site word simply is not in the file that answers you, so no amount of re-scoring reaches it.

**So: read the top 3–5, not just #1.** The right file was usually still in that window.

### Teaching it your words — `knowledge/site-vocabulary.md`

That file is a plain table of *what you say* → *what the files call it*, and the search rewrites your
question through it before looking:

```
"i want to take my door schedule out to excel"
   ->  out to excel  becomes  export csv schedule
   ->  action-export-schedule-to-csv.cs now ranks #1 (it was absent from the top 5)
```

**Add a row whenever a search misses because you used the site word.** It is read live, so a new row works
**immediately — no rebuild needed.** That is the one part of this system that genuinely does get better
every time it disappoints you.

Measured honestly on the four vocabulary failures: **1 fixed outright, 1 moved to #2, 1 had its actively
harmful top hit removed** ("add 4 more floor levels" no longer returns the *slab* creator), **1 unchanged**
— with zero regressions across six known-good questions. Useful, not magic.

Two rules if you add rows, both learned the hard way and written into the file itself:

- **Map the phrase, not the word.** `floor level` → `level` is right; `floor` → `level` is wrong, because
  a Floor is a real Revit category.
- **Narrow rows only.** `drawing` → `view sheet` seemed sensible and made things worse — it fires on
  nearly every question. Rejected rows are kept in the file with the reason, so nobody re-adds them.

### One deliberate quirk: `brain-log.md` is held back

The Brain's change log is the largest file here and it describes every problem ever solved — including,
word for word, the questions those problems were found with. Writing up the diffuser failure promptly made
the *log* the top answer to "how many diffusers do I need in this room", pushing out the skill that
actually answers it.

So its score is discounted to 0.85. Measured trade-off, not a tuned sweet spot — the scores sit within
thousandths of each other, so it falls off a cliff rather than sliding, and **no single value gets both
cases right**:

| Discount | "the dated log of changes" | "how many diffusers…" |
|---|---|---|
| none (1.0) | log ranks #1 ✓ | log ranks #1 ✗ |
| **0.85 (chosen)** | log misses the top 4 ✗ | the skill ranks #1 ✓ |

Real work questions get asked constantly; "show me the changelog" almost never. **If you want the change
log, just open `knowledge/brain-log.md`** — don't search for it.

### The same thing happened to `glossary.md` — caught 2026-08-13

A week later the diffuser question was back at #2, this time displaced by
[`knowledge/glossary.md`](../knowledge/glossary.md). Same structural cause: the glossary maps Ajmal's
words to Revit meanings, so it gains a row every time a term causes confusion, and **a file of
question-shaped phrases matches more questions the bigger it gets.** The table above still claimed the
skill ranked #1. It had not for days, and nobody noticed — `score-brain.cmd` found it on its first run.

**It could not take 0.85, and that difference is the interesting part.** Looking a word up *is* a real
question, and the glossary is the right answer to it, so the discount had to stop the displacement
without sinking the file's own job. Swept both ways:

| Weight | "how many diffusers" → skill | "what does duck mean" → glossary |
|---|---|---|
| 1.00 | #2 ✗ | #1 ✓ |
| 0.96 | #1 ✓ | #1 ✓ |
| **0.93 (chosen)** | **#1 ✓** | **#1 ✓** |
| 0.90 | #1 ✓ | #1 ✓ |
| 0.85 | #1 ✓ | #2 ✗ — `nfpa13-sprinkler-spacing.md` took #1 |

Both hold between 0.90 and 0.96, so **0.93 is the centre of that window** — chosen to sit as far from
both cliffs as possible, because these scores differ by thousandths and an edge value is one new file
away from flipping. Both cases are now guarded rows in `test-questions.md`.

**The general lesson: any file that grows by absorbing the phrasing of real questions will eventually
outrank the files that answer them.** Two have done it so far. Check the third the day you notice a
reference file ranking oddly high.

### Semantic only — the baseline

```
"D:\Ajmal\AJ AI Brain\semantic-index\ask-brain.cmd" "how do I undo a mistake"
```

Same options minus `--explain` and `--no-fragment-tool`. Kept deliberately
unchanged so the two can be compared on the same question.

### Rebuild the index

```
"D:\Ajmal\AJ AI Brain\semantic-index\index-brain.cmd"
```

**Normally 2–4 seconds.** It only re-reads the files that actually changed:

| What you did | Time |
|---|---|
| Nothing | **2.3 s** |
| Changed one file | **2.8 s** |
| Added one file | **3.9 s** |
| Deleted one file | **2.7 s** |
| Full rebuild (all 307 files) | ~79 s |

You can double-click the file. Add `--full` to force a complete rebuild.

**A full rebuild also happens on its own when it must** — if you change the
chunking settings or edit `brain_index.py`/`brain_common.py`, every stored chunk
is the wrong shape, but the *files* are untouched, so a file-by-file comparison
would happily skip them all. The build fingerprint catches exactly that and
starts over. Editing even a comment in those two files triggers it: a needless
80 seconds is a cheap price against a silently half-migrated index.

**Why you can trust the fast path.** The risk with only updating what changed is
*ghosts* — a file that used to make 12 chunks and now makes 8 leaving 4 orphans
behind, text that exists nowhere in the Brain but still answers questions. Every
chunk of a changed file is deleted before the new ones are written. Verified by
shrinking a 30-chunk file to 2 and confirming the deleted text was gone, and by
checking that an incrementally-updated index reports **exactly the same chunk
count as a full rebuild** — 2,540 either way.

---

## When to rebuild — this is the important bit

**Since 2026-08-13 you usually don't have to.** `tools/reindex-mark.mjs` (a PostToolUse
hook) flags any file edit made inside a session, and `tools/reindex-run.mjs` (a Stop
hook) does **one** rebuild at the end of that turn — not one per edited file. Editing
the Brain through an AI session now refreshes the index by itself.

**What still goes unnoticed** is any edit the session never saw:

> **A git checkout or branch switch, a file changed in an editor, a folder copied in —
> none of those fire a hook. After any of those, run `index-brain.cmd` yourself.**

**You will be told when you forget.** Every search compares the Brain on disk
against what the index was built from, and shouts if they differ:

```
!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
STALE INDEX - these results are from an OLDER copy of the Brain.
  1 changed, 2 new since the last rebuild.
    changed  knowledge/live-model/hvac-ducts.md
    added    scripts/actions/action-new-thing.cs
  FIX: edits made inside a session re-index themselves when the turn ends.
       If this keeps showing, run  semantic-index\index-brain.cmd  (2-4 s).
!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
```

It compares file **contents**, not modified-dates — copying the folder or
checking out a git branch changes dates without changing a word, and a warning
that cries wolf is one you learn to ignore. The results still print underneath;
you are being warned, not blocked.

You do not need to remember what changed — it works that out itself by comparing
file contents, and re-reads only what actually moved. Ghost entries are prevented
not by rebuilding everything but by deleting *every* chunk of a changed file before
writing its new ones; that was verified by shrinking a 30-chunk file to 2 and
confirming the removed text was gone, and by checking an incrementally-updated index
reports the same chunk count as a full rebuild.

Running it more often than needed costs nothing — a rebuild with nothing changed is
about 2 seconds.

*(This paragraph used to claim every run threw the whole index away and cost 80
seconds. That stopped being true when the incremental path landed on 2026-08-06 and
was corrected on 2026-08-13 — it had been contradicting the timing table further up
this same file.)*

---

## What is in this folder

| Item | What it is |
|---|---|
| `ask-brain-hybrid.cmd` | Shortcut — ask a question (meaning + words). **Use this one.** |
| `ask-brain.cmd` | Shortcut — ask a question (meaning only, the baseline) |
| `index-brain.cmd` | Shortcut — rebuild the index |
| `brain_common.py` | Shared settings: which folders, where data goes |
| `brain_index.py` | Reads the Brain and builds the index |
| `brain_search_hybrid.py` | Answers a question using both signals |
| `brain_search.py` | Answers a question using meaning only |
| `embed_bge.py` | The embedding model — turns text into the numbers the search compares |
| `rerank.py` | Optional second-pass scorer, off by default |
| `score_brain.py` | Runs `test-questions.md` and prints the score |
| `requirements.txt` | The one Python package needed |
| `venv/` | The private Python installation |
| `model-cache/` | The downloaded language models (~250 MB with the re-ranker) |
| `chroma-db/` | The index itself |
| `pip-temp/`, `pip-cache/`, `run-temp/` | Scratch space, kept off the system temp folder |

Only the `.py`, `.cmd`, `.md` and `.txt` files are saved to git. Everything else
is rebuildable and is ignored, so the repo stays small.

---

## Two things worth knowing

**It works offline.** The model lives in `model-cache/`, downloaded once. Searching
and rebuilding both work with no internet, and your Brain's text never leaves this
machine — there is no API key anywhere in this layer because there is no service to
call.

**The model is `bge-small-en-v1.5`** (changed from `all-MiniLM-L6-v2` on 2026-08-20).
The old one was a 2021 model that read only 256 word-pieces at a time; this one reads
512 and retrieves measurably better. It is the same size class and still runs on
`onnxruntime`, so nothing new was installed. Changing the model changes the
fingerprint in `brain_common.py`, which makes the next `index-brain.cmd` rebuild
everything by itself — vectors from two different models cannot be compared, so a
part-migrated index would answer every question, just quietly worse.

**Nothing is ever written to the system temp folder.** This is a hard rule, set
after `tools/verify-fragments-compile.ps1` wrote ~267 unsigned DLLs into `%TEMP%`
and Sophos flagged it as `ML/PE-A`. Every path used here — the database, the
model, pip's unpacking folder, and Python's own temp — is forced inside this one
folder by `brain_common.py`. Verified clean on 2026-08-06.

---

## If something breaks

**"collection does not exist"** — run `index-brain.cmd` once.

**"The embedding model has not been downloaded yet."** — run it once, then rebuild:

```
venv\Scripts\python.exe embed_bge.py --download
index-brain.cmd --full
```

It refuses to fall back to the old model on purpose. Half an index in one vector
space and half in another still answers every question, just worse, and nothing
would ever tell you.

**Moved the Brain to another folder or another PC?** Nothing to do — since
2026-08-20 `BRAIN_ROOT` is worked out from where `brain_common.py` actually sits,
so the folder simply works wherever it is copied. (It used to be a hardcoded
`D:\...` path, which meant the search ran on exactly one PC.) Set the
`AJ_BRAIN_ROOT` environment variable only for the odd case of running these scripts
from a copy that lives outside the Brain they index.

**Starting fresh on a new machine?** See `requirements.txt` — it has the setup
commands, including how to keep pip off `%TEMP%`.
