# AJ AI Brain — RAG and Agents

**Design spec — 2026-08-13**

What this is: the plan to finish turning the AJ AI Brain into a working RAG system, and to add a small
set of agents that keep it growing while Ajmal works in Revit.

Read this before building anything. Nothing in here is built yet.

---

## 1. The question this answers

> *"Can we make this AJ AI Brain into a RAG? Is it possible, and what do we need to do more?"*

**It already is one.** The retrieval half was built on 2026-08-06 and works. What is missing is not a
vector database — it is that the retrieval step depends on a person remembering to run a command, and
the index goes stale the moment anyone forgets to rebuild it.

So this is not a build-from-scratch project. It is a **finishing** project, plus an **agent layer** on
top.

---

## 2. Verified starting state

Every number below was read from disk on 2026-08-13, not from documentation.

### What exists and works

| Piece | Detail |
|---|---|
| Skills | 10 |
| Script fragments | 269 — 223 verified, 20 flagged untested, 12 blocked, 14 no status |
| Native bridge tools | 17 |
| Knowledge files | 27 |
| Files in the search index | 314 |
| Vector store | ChromaDB, in `semantic-index/chroma-db/` |
| Embedding model | `ONNXMiniLM_L6_V2` (all-MiniLM-L6-v2), 166 MB, fully offline |
| Chunking | 900 char target, 1,100 max, 150 overlap |
| File types indexed | `.md` and `.cs` **only** |
| Query rewriting | `knowledge/site-vocabulary.md`, 62 lines, read live — no rebuild needed |
| Freshness check | Compares file **contents**, not dates; prints `STALE INDEX` |
| Backup | GitHub remote, in sync, 173 commits since 2026-07-22 |

### The gaps, stated plainly

| Gap | Evidence |
|---|---|
| **Retrieval is optional** | No `UserPromptSubmit` hook exists in `.claude/settings.json`. Search only happens if someone types the command. |
| **The index goes stale silently** | It is a snapshot. Nothing rebuilds it automatically. |
| **Search does not work everywhere** | `ask-brain-hybrid.cmd` is a Windows batch file. It cannot run on Claude Code for web. |
| **No agents exist** | There is no `.claude/agents/` folder at all. |
| **The measurement cannot be repeated** | The 24 test questions from 2026-08-06 were never saved. Only the score survives. ~7 questions are recoverable from `knowledge/brain-log.md`; ~17 are lost. |
| **Two knowledge files are oversized** | `knowledge/live-model/core.md` (327 lines) and `knowledge/live-model/families.md` (456 lines), both past the 300-line rule, both flagged every session, neither reviewed. |
| **34 fragments are unproven** | 20 flagged untested + 14 with no status = 13% of the library. |
| **Nothing records what real work happened** | No log of which fragments actually get used, or which fail on real models. |

---

## 3. The core idea — three layers, built in order

The mistake to avoid is using an agent to do a job a script should do. These are three different
kinds of thing and they stack:

| Layer | What it is | Rule of thumb |
|---|---|---|
| **1. Memory** | The Brain can find what it already knows | Plumbing. Built once. |
| **2. Reflex** | Things that must never be forgotten happen with nobody deciding | **No judgment in the job → it is a hook, never an agent.** |
| **3. Hands** | Jobs with real judgment, run in parallel, report back | Agents. |

**Build bottom-up.** Agents sitting on top of a stale index just means several workers confidently
reading the same out-of-date page.

---

## 4. Layer 1 — Memory (the RAG plumbing)

### 4.1 `search_brain` as a native tool

Add one tool to the MCP server so Brain search is a real tool call instead of a Windows batch file.

- New file `mcp-server/tools/search-brain.js`, following the existing `register(server)` pattern
- Two lines added to `mcp-server/index.js`
- Does **not** touch the Revit bridge — it shells out to the existing Python search
- Updates `mcp-server/tools/README.md` (17 tools → 18)

**Why it matters:** on Claude Code for web, `.cmd` files silently do nothing. That exact trap already
cost this repo a whole session of unchecked work on 2026-08-04, recorded in `CLAUDE.md`. This is the
same trap, second appearance.

### 4.2 Auto-search on every question

A `UserPromptSubmit` hook runs the search on Ajmal's question before the assistant sees it, and puts
the top results into context automatically.

**Decision made: gate it, do not speed it up.** A search cold-starts Python and loads a 166 MB model —
roughly 2–4 seconds. Running that on "ok" and "yes go ahead" is a tax on nothing. So:

- Fire only on substantive questions (length threshold + a Revit/MEP vocabulary check)
- Skip short confirmations entirely
- **The `STALE INDEX` warning must be carried into the injection, loudly.** Silent auto-injection of
  stale text on every single message is worse than no search at all.

If the delay ever genuinely annoys, *then* build a warm search service. Not before. Building the
complicated version before feeling the problem is how projects die.

### 4.3 A better embedding model

`all-MiniLM-L6-v2` is the quality ceiling. It is small, general-English, from 2021, and has never seen
"FCU", "diffuser" or "Ashghal" used the way this trade uses them. Its input limit is also shorter than
the 900-character chunks being fed to it.

Swapping it is roughly a day and lifts every other item at once — **but only attempt it after the test
set exists (§6.1), or there is no way to know whether it helped.**

---

## 5. Layer 2 — Reflex (hooks, no agents)

### 5.1 Auto re-index

`.claude/settings.json` already has:

```
PostToolUse → matcher "Edit|Write|NotebookEdit" → tools/verify-consistency-hook.mjs
```

That hook fires on every file edit in this repo already. Re-indexing belongs on the same trigger.

- Runs `index-brain.cmd` — 2.8 seconds for a one-file change
- **Filtered:** only fires when the edited file is inside `skills/`, `knowledge/`, `scripts/`, one of
  the six root docs, or `mcp-server/tools/README.md`. Editing anything else must not trigger it.
- Never blocks the edit; failure is reported, not fatal

**This permanently removes the staleness problem, costs nothing, and needs no agent.** Never hire a
worker to press a button.

---

## 6. Layer 3 — Hands (the agents)

Three agents. Two ideas from the first draft were deliberately deleted:

- **A "capture" agent** — writing one line to a holding file is a single tool call. An agent for that
  is overhead with no gain.
- **A "health check" agent** — `tools/brain-status.mjs` and the consistency hook already do this,
  never forget, and cost nothing.

### 6.1 The Librarian — *"did we save what we learned?"*

| | |
|---|---|
| **Job** | At session end, file what was learned: check it is not already written down, route it per `skills/brain-self-maintain/SKILL.md`, update `knowledge/INDEX.md` / `scripts/README.md` / `knowledge/brain-log.md`, rebuild the index, run the consistency check |
| **Runs** | On request ("save it"), or at end of session |
| **Revit access** | **None.** No bridge tools at all. |
| **Why an agent** | Slow, read-heavy, fixed checklist, and it happens exactly when everyone has stopped paying attention |

**Build this one first.** It is the entire reason the Brain gets stronger rather than staying still.
A lesson not written down is gone forever; a script not written can be written tomorrow.

### 6.2 The Script Writer — new C# fragments

| | |
|---|---|
| **Job** | Search all 269 existing fragments first, compose from proven pieces where possible, compile-check, document in `scripts/README.md`, add `// SOURCE:` cross-references, rebuild the index |
| **Runs** | On request |
| **Revit access** | **None** until Ajmal tests the result himself on one element |
| **Why an agent** | The mandatory first step — *search the 269 first* — is the Brain's own rule and the one most likely to be skipped because it feels expensive. An agent that is told it must do it first will do it every time. |

### 6.3 The Investigator — read-only model questions

| | |
|---|---|
| **Job** | Questions needing heavy model reading: trace what connects to what, audit a parameter across hundreds of elements |
| **Runs** | On request |
| **Revit access** | **Read-only.** Restricted to running fragments already proven read-only — it does not author fresh C#. |
| **Why last** | This is the only agent with real risk. Build it after the safe ones have earned trust. |

### 6.4 Hard rules for all agents

**One bridge, one Revit session.** If a background agent runs a script while Ajmal is working in the
same model, that is two transactions fighting over one model — changes nobody asked for, in a file
being actively worked on.

| Rule | Enforcement |
|---|---|
| Background agents never touch Revit | They are not given the bridge tools |
| Only read-only Revit access for the Investigator | Restricted to proven read-only fragments |
| Anything that **changes** the model stays in the main conversation with Ajmal | Design, not preference — his own rules 2, 3 and 5 in `START-HERE.md` require him in the loop |

**Shared-file collisions.** `scripts/README.md`, `knowledge/INDEX.md` and `knowledge/brain-log.md` are
written by everything. Two writers at once means one write silently erases the other — no error, no
warning, a lesson simply gone.

Mitigation: during work, captures go to **one append-only inbox file** that nothing else touches. The
Librarian does the real filing later, when nothing else is running.

### 6.5 One shared rules file — not three copies

Every agent starts with an empty head. It does not know the mm→feet rule, does not know *verify, don't
trust the API*, and does not know this, from `CLAUDE.md`:

> Never bulk-edit files here with PowerShell `Get-Content`/`Set-Content` — it corrupted **41 files** on
> 2026-07-26.

**Decision: one rules file that every agent references, not the rules copied into three agent
definitions.** Copies drift. When a rule is learned, it must be learned once, in one place.

Without this, every agent will repeat every mistake already paid for.

---

## 7. The six improvements

### 7.1 A test set that runs — the highest priority item in this document

Retrieval was measured once, on 2026-08-06: 24 questions, 13 right at #1. That number is the most
honest thing in the repo.

**The questions were never saved.** Only the score was kept. About 7 are recoverable from
`knowledge/brain-log.md`; the rest are gone.

So every change proposed above — a new model, split files, different chunking — would be made blind.
Worse, a change that quietly makes retrieval *worse* would go unnoticed for months.

**Build:** a file of *question → the file that should come back*, and a runner that prints a score.

```
18 / 24 correct   (was 16 / 24)   improved
```

**Honesty requirement:** questions written by the person tuning the tool prove nothing. The 2026-08-06
run was valid precisely because independent testers wrote them. Any new questions must come from
Ajmal or someone else — not from the assistant building the improvements.

This does not improve the Brain by itself. It makes every other improvement **provable instead of
hopeful**, which is why it goes first.

### 7.2 Let the vocabulary file write itself

The measured weakness is not the search engine — it is site words that appear in no file: "floor
levels", "light fitting", "out to excel".

The moment a gap reveals itself is obvious and detectable: **a search misses, then a second search
with a different word succeeds.** Log that pair as a candidate row for `knowledge/site-vocabulary.md`.

Two rules already learned the hard way, and already written in that file, must be kept:

- Map the **phrase**, not the word — `floor level` → `level` is right; `floor` → `level` is wrong,
  because Floor is a real Revit category
- **Narrow rows only** — `drawing` → `view sheet` fires on nearly every question and made things worse

This is the one part of the system that improves every time it disappoints. Automating it compounds.

### 7.3 A job log — is 269 fragments a library or a hoard?

Nothing records what real work happened. Every session there are real elements, real numbers, real
failures — and it all evaporates when the session ends.

**Build:** one appended line per real task — what was asked, what was used, did it work, how many
elements.

After a month this answers a question that is currently unanswerable:

- Which fragments do the actual work (estimate: **40 of the 269 do 90% of it**)
- Which have never run on a real job
- Which fail repeatedly

**And the sharp part: an unused fragment is not free.** It competes in every search. More files means
more things for the right answer to lose against. The job log is what makes it safe to delete.

When a script fails on a real model, that is the single most valuable signal in the whole system, and
today it disappears.

### 7.4 Split the two oversized files

`knowledge/live-model/core.md` (327 lines) and `knowledge/live-model/families.md` (456 lines).

This is **not housekeeping — it is retrieval accuracy.** A 456-line file becomes roughly 15 chunks.
Each competes separately, and the chunk that comes back may be missing the context sitting just above
it. Big files retrieve worse than small focused ones.

The 300-line split rule was always a RAG rule; nobody had connected it yet. With §7.1 in place, the
improvement can be measured.

### 7.5 Close out the 34 unproven fragments

20 flagged untested + 14 no status = 13% of the library. The session banner reports it every time and
nothing closes the loop.

No code needed. Next session with Revit open and a test model, work through them in batches.
**83% → 95%+ proven.** Cheapest quality win available.

### 7.6 A file per project

The Brain knows *how* to do things but nothing about *which job is open* — Qatar naming, which
standards apply, which family library, which units.

**Narrow version only.** Not model state — `START-HERE.md` rule 2 rightly forbids trusting cached
model data. Only stable facts: standards, naming conventions, paths, units.

Lowest priority of the six.

---

## 8. One practical catch to test before agents go live

`.claude/settings.json` narrates on `PreToolUse` with matcher `*` — **every tool call speaks aloud.**

If background agents fire tool calls while Ajmal is modelling, he may get continuous narration about
work he did not ask for, mid-duct. Worth testing before turning agents loose, and easy to fix once
known — but exactly the kind of irritation that makes someone switch the whole system off.

---

## 9. Build order

| Phase | Items | Done means |
|---|---|---|
| **1 — Measure and stop the bleeding** | §7.1 test set · §5.1 auto re-index · §4.1 `search_brain` tool | A score can be printed on demand; the index can no longer go stale; search works on web |
| **2 — Close the real gap** | §4.2 auto-search (gated) · §6.1 Librarian agent · §6.5 shared rules file | Nothing already written down gets missed; what is learned gets filed |
| **3 — Quality, now measurable** | §4.3 better model · §7.4 split the two files · §7.2 self-writing vocabulary · §7.5 prove the 34 | Every change scored against §7.1 with no regressions |
| **4 — Growth** | §6.2 Script Writer · §7.3 job log · §6.3 Investigator · §7.6 project file | The Brain reports which parts of itself actually get used |
| **5 — Documents** | PDFs and standards (QCS, Ashghal, NFPA, submittals) | Only after citation design is settled — see §11 |

**Phase 1 is a few hours and it is the phase that matters.** Everything after is improvement on
something already working.

---

## 10. How we will know it worked

| Question | Measured by |
|---|---|
| Is retrieval getting better? | §7.1 score, run before and after every change |
| Has the index gone stale? | It cannot — §5.1 |
| Does search work everywhere? | It runs on Claude Code for web — §4.1 |
| Are lessons being kept? | Files created per session, by the Librarian |
| Which fragments matter? | §7.3 job log |
| Is the library proven? | `brain-status.mjs` — target 95%+ verified |

**No item ships on "it felt better."** This repo's own standard, from `semantic-index/README.md`, is
*"measured, not claimed."*

---

## 11. Deliberately not doing

| Not doing | Why |
|---|---|
| **A standalone chat app** | Every other item reuses the assistant as the generation half, free. A standalone app needs its own model — either a weak local one, or a paid API with a key and internet. Only worth it if the rest earns its keep first. |
| **A warm/fast search service** | Gating (§4.2) is far simpler and may make the speed problem disappear. Build it only if the delay is actually felt. |
| **A capture agent** | One tool call. Overhead with no gain. |
| **A health-check agent** | Already covered by scripts that never forget. |
| **An agent that changes the Revit model** | Breaks `START-HERE.md` rules 2, 3 and 5 by design — it cannot ask Ajmal anything. |
| **PDFs before Phase 5** | Needs the citation design settled first: an uncited standards quote breaks the Brain's core rule harder than anything else here. Document, version and page are mandatory. |
| **Editing the Revit add-in source** | Out of scope for this Brain, per `START-HERE.md`. |

---

## 12. Open questions for Ajmal

1. **Which goal comes first** — the assistant never missing what he has already written, or the Brain
   answering from documents he never wrote (QCS, Ashghal, NFPA, submittals)? The second pulls Phase 5
   to the front and changes the order.
2. **The 17 lost test questions** — reconstruct them himself, or start a fresh set? They must not be
   written by the assistant tuning the tool.
3. **Is 269 fragments intended to grow, or be pruned?** §7.3 makes pruning safe, but the intent
   changes whether growth or curation is the goal.

---

## 13. Notes for whoever picks this up

- Nothing in this spec was built. It is the design only.
- Every number in §2 was read from disk on 2026-08-13, not copied from documentation. This repo's
  recurring failure mode is documentation quietly getting ahead of reality — verify before trusting
  anything here too.
- After anything in `skills/`, `knowledge/`, `scripts/` or the root docs changes, run
  `semantic-index\index-brain.cmd` — until §5.1 makes that automatic.
