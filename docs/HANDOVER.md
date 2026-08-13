# Handover — pick this up in a new chat

Paste the block below as your first message in a fresh session. Everything above the line is context;
everything under **"What I want you to do"** is the actual work.

Last updated: 2026-08-14, end of the fixture-and-verification session.

---

## The prompt

```text
Read D:\Ajmal\AJ AI Brain\docs\HANDOVER.md and CLAUDE.md first, then continue the work listed there.
```

That is all you need to type. The rest of this file is what that session will read.

---

## 1. What this repo is

`D:\Ajmal\AJ AI Brain` — a portable knowledge package for doing real Revit work through an AI agent
connected to a live Revit session over an MCP bridge. Private GitHub repo `Ajmalpshaik/AJ-AI-Brain`.

It is **not** the AJ Tools plugin source. It is skills + knowledge + 270 proven C# fragments, plus the
retrieval and self-maintenance machinery around them.

A SessionStart hook prints the true state on every start. **Trust that, not this file** — this file is a
snapshot and snapshots go stale, which is the failure mode this whole repo is built against.

## 2. State as of 2026-08-14

| | |
|---|---|
| Skills / fragments / native tools | 10 · 270 · 17 |
| Proven against a real model | 223 (83%) · 16 flagged untested · 13 blocked · 18 no status |
| Retrieval score | **4/14 at #1, 7/14 in top 5** — honest, not flattering |
| Knowledge graph | 1,192 nodes · 0 dangling · 328 named communities |
| Git | `main` in sync, one branch, tag `v2026.08.14` |

## 3. What the last session did — the short version

Turned the Brain from "a RAG you had to remember to use" into one that maintains itself, then started
proving the unproven fragment library against a live model.

**Six reflexes now run on hooks, so nothing depends on anyone remembering:**
semantic index rebuild · retrieval re-score · knowledge-graph rebuild · uncaptured-test-question nudge ·
un-composed-C# nudge · voice.

**Built:** score card (`score-brain.cmd`), `search_brain` and `search_graph` MCP tools, `job-log/`,
four agent definitions, a cross-encoder re-ranker (measured neutral, **shipped OFF**).

**Five times, measuring overturned reasoning** — including Anthropic's own published Contextual
Retrieval, which measured *worse* here (7/14 → 5/14) and was reverted. All five are in
`knowledge/brain-log.md` so nobody re-derives them.

## 4. THE THING THAT WILL BITE YOU FIRST

**The Revit model `Project1` is UNSAVED.** Every fixture built for verification lives only in memory:

```
17 ducts  ·  1 accessory-in-run  ·  7 duct fittings  ·  4 walls
1 room (43.2 m²)  ·  1 MEP space  ·  2 HVAC zones  ·  1 floor  ·  3 sheets
```

Including a **12 m trunk in 6 pieces with 4 takeoffs and 3 union fittings** that took real work to build.

**Ask Ajmal to save it before doing anything else.** If it is gone, rebuild with `Duct.Create`,
`Document.Create.NewTakeoffFitting`, `Wall.Create`, `NewRoom`, `NewSpace`, `NewFloor`,
`ViewSheet.Create` — all proven live on 2026-08-14, see `knowledge/brain-log.md`.

## 5. What I want you to do

### A. Finish the last 3 of the 9 live-bridge fragments — fixtures already exist

1. `actions/annotation/action-add-aligned-dimensions.cs` — dimension the 4 walls
2. `actions/annotation/action-add-spot-elevations.cs` — the floor gives the planar face
3. `actions/sheet-dates-revisions/action-manage-sheet-sets.cs` — sheets M-901/902/903 exist

For each: read the fragment, run it verbatim with real inputs, then **verify from a SEPARATE bridge
call** — never from the script's own report. Update its row in `scripts/README.md` and add a dated
entry to `knowledge/brain-log.md`. Two silent-success bugs were found exactly this way.

### B. Re-examine the 13 "blocked / fixture-blocked" items

**Twice in two days, "fixture-blocked" turned out to mean "nobody tried to create it."** The insulation
fragment had been blocked since July; one `Duct.Create` call cleared it. The slice-trunk recipe said
"no matching multi-branch trunk fixture available"; two API calls built one.

**Challenge every blocked item with "can this be created by API?" before accepting it.**

### C. Ajmal's own job — do not do this for him

`semantic-index/test-questions.md` has **14 questions and needs 20+**. They must be *his* words, not
yours: questions written by whoever is tuning the search prove nothing. They capture themselves as he
works — `tools/test-row-nudge.mjs` lists any he asked that are not yet rows. **Surface them; let him
confirm the expected answer.**

At 20+, the **embedding-model swap** unlocks — the largest accuracy gain still available, deliberately
not attempted because with 14 questions there is no honest way to tell improvement from damage.

### D. Open, lower priority

- `action-place-accessory-on-run.cs` — rewritten, its METHOD proven, but the file as one uninterrupted
  run threw an unisolated null reference. Read its STATUS block. Suspect: an element handle reused
  across a transaction boundary after `BreakCurve`.
- Graph markdown half needs a subagent pass or `GEMINI_API_KEY` (code half is automatic).
- 328 communities are named; new ones fall back to their hub name.

## 6. How to work here — the rules that actually bite

- **Read back after every change.** Three "silent success" bugs in two days: scripts that reported
  success while doing nothing. The script's own report is not evidence.
- **One step per script.** Break, place, connect as one script hung Revit behind a modal dialog. Split
  and commit between steps.
- **A modal dialog HANGS the bridge, it does not fail.** If a call never returns, ask Ajmal to look at
  Revit — there is probably a dialog waiting. Ask him to press Cancel, then re-read state.
- **Never `Document.Regenerate()` after `Commit()`** — illegal, and surfaces as a hang.
- **Every number is a per-request input.** State the ones you choose.
- **He speaks mm. The API is feet.** Convert both ways, explicitly.
- **`npm test` in `mcp-server/` is gated now** — it invokes `delete_elements` with `confirm:true`. If
  the bridge is live it skips. Do not remove that gate.
- Fix any consistency drift **in the same turn** the hook reports it.

## 7. Where to look things up

```bash
semantic-index\ask-brain-hybrid.cmd "the question in plain English"   # meaning + words, 316 files
node tools/fragment-index.mjs --find <word>                            # the 270 fragments, by purpose
node tools/brain-status.mjs --capabilities                             # what this Brain can actually do
node tools/job-report.mjs --unused                                     # which fragments have never run
semantic-index\score-brain.cmd                                         # retrieval score, before/after
```

Read the **top 3–5** search hits, never just #1 — measured ~3 in 4 right at #1, and the misses are site
vocabulary. `knowledge/glossary.md` is the site-word → Revit-word map.
