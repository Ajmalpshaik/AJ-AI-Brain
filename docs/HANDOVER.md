# Handover — pick this up in a new chat

Paste the block below as your first message in a fresh session. Everything above the line is context;
everything under **"What I want you to do"** is the actual work.

Last updated: 2026-08-14, end of the second verification session.

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

## 2. State as of 2026-08-14 (end of session 2)

| | |
|---|---|
| Skills / fragments / native tools | **11** · 270 · 17 (`ajtools-visual-report` added 2026-08-14) |
| Proven against a real model | **234 (87%)** · 12 flagged untested · 12 blocked · 12 no status |
| Retrieval score | 4/14 at #1, 7/14 in top 5 — honest, not flattering |
| Git | `main`, one branch |

Verified count moved 223 → 234 this session. Nine of those eleven were **already verified and being
miscounted** — see §4.

## 3. THE THING THAT WILL BITE YOU FIRST

**The Revit model `Project1` is STILL UNSAVED.** Everything lives only in memory:

```
17 ducts · 7 duct fittings · 1 accessory · 4 walls · 1 room · 1 MEP space · 2 HVAC zones
1 floor · 3 sheets · 3 air terminals · 1 dimension · 1 spot elevation
2 insulated ducts · 2 electrical equipment · 2 electrical fixtures · 1 power circuit
```

**Ask Ajmal to save it before doing anything else.** Everything above is rebuildable by API (all proven
live — `Duct.Create`, `Wall.Create`, `NewRoom`, `NewSpace`, `NewFloor`, `ViewSheet.Create`,
`NewFamilyInstance`, `DuctInsulation.Create`, `ElectricalSystem.Create`), but rebuilding costs a session.

The insulation and electrical fixtures were **deliberately left in place** so the fragments that depend on
them stay re-runnable.

## 4. What the last session did

**Closed 8 open items.** The three annotation/sheet-set fragments (aligned dimensions, spot elevations,
sheet sets), the insulation action, both insulation filters, `load-family.cs`, and
`filter-by-electrical-system.cs`.

**Found a fourth silent-success bug** — `action-add-spot-elevations.cs` annotated the soffit at −300 mm on
a 300 mm floor while reporting "1 placed, 0 skipped". Fixed.

**The headline lesson, and it cost real bugs to learn: a CHECK written by reading code is a hypothesis,
not a finding.** Of five predictions tested against the live model this session, **three were wrong**:

- The spot-elevation guard `Math.Abs(FaceNormal.Z) > 0.9` would have passed the very face that was
  wrong (−1.00). The real test is `> 0.9` **positive**.
- `action-manage-sheet-sets.cs`'s header claimed selecting an existing set as `CurrentViewSheetSet` was
  not viable on 2020. Reflection: it is plainly settable. The workaround it justified actually failed.
- `load-family.cs`'s "KNOWN BUG" does not occur — already-loaded returns `ok=False` **and** `fam=NULL`.

**"Fixture-blocked" was wrong three more times.** Insulation (the fragment *creates* insulation — it only
needed a TYPE, and the template ships six), and electrical (the stock library ships **166 electrical
.rfa**, which also cleared `load-family.cs`). **Ask "can this build its own fixture, or can the API build
one" before accepting any blocked item.**

**`tools/brain-status.mjs` was undercounting.** It matched `/verified 2026/`, so nine rows written
"verified **live** 2026-08-14" were reported as unproven — the drift-detector had drifted. Widened.
Real figure moved 223 → 232 before any new work.

## 5. What I want you to do

### A. Ajmal's own job — do not do this for him

`semantic-index/test-questions.md` has **14 rows and needs 20+**. They must be *his* words. The nudge tool
(`node tools/test-row-nudge.mjs --all`) lists 32 captured messages, but most are session chatter —
**only three are real Revit questions not yet rows**, and they are waiting for him to confirm:

| His words | Proposed expected file (HIS call, not yours) |
|---|---|
| `no am talking bout the biggest space how mey airterminal is there` | `skills/ajtools-live-model/SKILL.md` |
| `not all the assessory isulate only vcd` | `skills/ajtools-live-model/SKILL.md` |
| `can you tell me how meny vcd is there in the model` | `scripts/actions/reporting/action-count-and-report.cs` |

At 20+, the **embedding-model swap** unlocks — the largest accuracy gain still available.

**The nudge tool needs tuning**: it captures every message, so real questions are buried in chatter.
Worth a filter.

### B. Still open, and genuinely so

- `action-place-accessory-on-run.cs` — METHOD proven, but the file as one uninterrupted run threw an
  unisolated null reference. Read its STATUS block. Suspect: an element handle reused across a
  transaction boundary after `BreakCurve`.
- `recipes/ray-trace-to-ceiling.cs` — **an ASK, not a wait.** `Ceiling.Create` has 0 overloads before
  2022 (re-confirmed by reflection). Ask Ajmal to draw ONE ceiling by hand; ten seconds, then it runs.
- A nested shared family, and a sleeve family — the last two genuine fixture blocks. Both could be
  authored via `Application.NewFamilyDocument` (proven to work), which is the next thing to challenge.
- Graph markdown half needs a subagent pass or `GEMINI_API_KEY`.

### C. Tomorrow's build — the Desktop → Claude Code handoff (agreed 2026-08-14)

Ajmal wants to work in **Claude Desktop** for normal Revit jobs and be told to switch to **Claude Code**
whenever the job is Brain maintenance — new skill, new script, updating, checking, anything that edits
this repo. Desktop cannot do that work: no file access of its own, no hooks, no shell, so no
session-status, no consistency check, no re-index.

**Build it in `mcp-server/`, not as a written rule** — a rule depends on the agent remembering; the relay
cannot forget. `@modelcontextprotocol/sdk` 1.29.0 is installed and exposes `getClientVersion()`
(verified 2026-08-14 in `server/index.d.ts`), so the relay can tell which app connected.

Shape:
1. Detect the client at startup. Claude Desktop → maintenance work is refused with an explanation, not
   attempted.
2. **ASK FIRST, then write** — his explicit instruction: *"first it will ask that can i give the
   handover.md like that and after that it will give."* Never write the handover silently.
3. On a yes, append the job to `docs/HANDOVER.md`, which every Claude Code session already reads first
   (`CLAUDE.md` §"Where the open work is"). So he opens Claude Code and it already knows — no pasting.

Independent of the MCP Apps question below; this works whether or not clickable UI ever renders.

### D. Also open — the clickable report (MCP Apps), untested

an external tool shipped an interactive Revit report in Claude: click a bar → Revit selects, edit a cell →
parameter changes, **no tokens**, because the widget calls tools directly instead of going through the
model. Mechanism, verified 2026-08-14: a `ui://` resource + `_meta.ui.resourceUri`, host renders it in a
sandboxed iframe, iframe calls tools back over postMessage.

**The Revit half already exists here** — `select_elements`, `set_parameter_value`, `report_parameters`.
Missing: the UI layer. `mcp-server/` registers tools only, no resources.

**Do not promise this works — test it.** MCP Apps hosts confirmed: Claude Desktop, VS Code Copilot, M365
Copilot, Goose, Postman, MCPJam, Archestra. **Claude Code is not on the list**, there is an open spec
issue about how CLI hosts should support it at all (ext-apps #689), and an open bug that widgets are not
rendering in Claude's own surfaces (ext-apps #671). Build one small proof — a duct report whose bars
select in Revit — and try it in both apps. Full write-up: `knowledge/tool-landscape-removed.md`.

## 6. How to work here — the rules that actually bite

- **Read back after every change, from a SEPARATE bridge call.** Four "silent success" bugs in three
  days. The script's own report is not evidence.
- **Add a negative control.** A filter that returns "all" looks correct until it matters — prove it
  *excludes* something too.
- **One step per script.** Break, place, connect as one script hung Revit behind a modal dialog.
- **A modal dialog HANGS the bridge, it does not fail.** If a call never returns, ask Ajmal to look at
  Revit and press Cancel.
- **One connection at a time — parallel bridge calls are unreliable.** And if Ajmal says "don't go to
  Revit", make **no** call at all, not even a ping: it preempts his other session rather than queueing.
- **Never `Document.Regenerate()` after `Commit()`** — illegal, surfaces as a hang.
- **Every number is a per-request input.** State the ones you choose.
- **He speaks mm. The API is feet.** Convert both ways, explicitly.
- **`npm test` in `mcp-server/` is gated** — it invokes `delete_elements` with `confirm:true`. Do not
  remove that gate.
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

## 8. Daily check, 2026-08-18 — the graphify split-brain has reopened

Found by the scheduled daily tool check, which runs in a **Linux container holding a fresh git clone and
nothing else**. Read §8.1 before trusting any of this: that container can see the *source* of all four
tools and the *runtime state* of none of them.

### The finding

Since the last full markdown extraction (2026-08-13, brain-log), **13 document files have changed or been
added**, including two that did not exist at extraction time:

- `skills/ajtools-visual-report/` — **an entire new skill** (`SKILL.md` + `dashboard-template.html`)
- `knowledge/tool-landscape-removed.md` — a new knowledge note
- modified: `CLAUDE.md`, `README.md`, `SETUP.md`, `START-HERE.md`, `knowledge/INDEX.md`,
  `knowledge/glossary.md`, `knowledge/live-model/core.md`, `knowledge/reply-style.md`,
  `knowledge/brain-log.md`, `scripts/README.md`

Twelve `.cs` fragments also changed, but those are the **AST half — it rebuilds itself** on the Stop hook
(`tools/graph-rebuild.mjs`). The markdown half cannot: it needs graphify's semantic pass, which is
subagents or a `GEMINI_API_KEY`. So the graph's document side is **as of 2026-08-13 and is missing a whole
skill**, while its code side is current — the exact split-brain closed on 2026-08-13 and reopened by five
days of normal work. The **Obsidian vault is derived from that graph, so it is stale the same way** (last
regenerated 2026-08-13: 1,503 notes, 325 stale pruned).

This is the failure mode the 2026-08-13 entry predicted in writing: *"graphify's semantic pass over the
markdown needs either a `GEMINI_API_KEY` or subagents"* — §5 B has carried it as an open item since, and it
has now grown by 13 files.

### Do this on the Windows machine — it cannot be done from the container

```bash
python tools/graph-rebuild.py --check    # authoritative staleness count, writes nothing
```

**That number, not the 13 above, is the real one.** The 13 is derived from git history; `--check` compares
content hashes against the actual extraction cache. Two things will skew a naive reading: `score-history.md`
is excluded as always-changing, and **`brain-log.md` is not excluded, so it will report stale on nearly
every run** — it is written every session. Judge the count by the *other* files in the list.

Then, for the markdown side, repeat the 2026-08-13 method (subagents, on Ajmal's explicit go-ahead) and
keep its two lessons: **chunk by size, not by count** — the batch that died held all five big root docs —
and **expect the shrink guard to fire**; overriding it needs a human reason, not a flag. `graphify-repair.py`
must run on the extraction *before* the build or the 203→0 dangling-edge fix is silently undone. Regenerate
the vault afterwards.

### 8.1 The daily check cannot verify what it was asked to verify

Worth fixing, because a check that always answers "cannot tell" is a check nobody reads. Of the four tools
it is asked about, **three have no runtime state in git by deliberate design** — `.gitignore` excludes
`graphify-out/` and `semantic-index/*` (venv, embedding model, Chroma DB) precisely because a stale derived
index travelling with the repo is worse than none, and the vault travels with the folder, not the repo. The
AJ AI bridge is a fourth case: `.mcp.json` points at `C:\Users\AjmalAlavudheen\...` and
`D:\Ajmal\AJ AI Brain\mcp-server\index.js`, and it needs a live Revit session, so it is unreachable from a
Linux container and was not connected to that session.

From the clone, these are genuinely checkable and were all **clean on 2026-08-18**: 11 skills · 270
fragments · 17 native tools, `verify-consistency.mjs` 8/8 with no drift, all 23 `mcp-server/` JS files
parse, the `delete_elements` safety gate still present in `test/smoke.test.js`, working tree clean and
level with `main`.

**So the daily check belongs on the Windows machine**, where `graph-rebuild.py --check`,
`index-brain.cmd`, a vault listing and a bridge `ping` all actually resolve. Run from anywhere else it can
only ever report on source.
