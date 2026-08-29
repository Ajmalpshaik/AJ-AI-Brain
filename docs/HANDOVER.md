# Handover — pick this up on the Windows PC

Last updated: **2026-08-29.** Read top-down. The newest session is first.

## 2026-08-29 — the daily four-layer health check is pointed at a machine that cannot see three of them

A scheduled daily task now fires in the **cloud container** and asks for the status of four things: AJ
Tool, the vector-based search, Graphify, and Obsidian. **It can answer one of the four, and that is
structural, not a bad day.** Writing it down here so the next run — and the next reader — does not
re-derive it.

**Why three are unanswerable from a container.** The vector index, the knowledge graph and the Obsidian
vault are all **gitignored derived state** (`.gitignore` says so in its own words, and
[`skills/brain-update-layers/SKILL.md`](../skills/brain-update-layers/SKILL.md) opens by saying they
"live on this machine and never travel in git"). A cloud session is a fresh clone: it gets their *code*
and none of their *state*. `node tools/brain-status.mjs` says it out loud —

```
vector index · knowledge graph · Obsidian vault — NONE PRESENT IN THIS CHECKOUT
```

and `node tools/brain-setup.mjs --check` agrees: *"NOT SET UP on this machine yet — missing: relay
dependencies, search environment, search index."* `graphify` is not on PATH, `graphify-out/` does not
exist, and the vault is a pure function of `graphify-out/graph.json`, so no graph means no vault to
check. **AJ Tool is the same story from the other side**: the bridge needs Revit open on the Windows PC,
and no `mcp__aj-tools-aj-ai__*` tool is connected in a cloud session at all.

Building any of them here would not help — the container is thrown away when the session ends, and the
result is gitignored, so nothing would reach the PC.

**Two of the four were also deliberately retired from routine maintenance**, which a daily check should
not quietly reverse. Ajmal's decision, 2026-08-23, on measured evidence recorded in that same skill:
the vector index answered **247 of 247** questions and runs on every message, while the graph's
`search_graph` had been called **zero** times ever and the Obsidian vault **had never been opened** —
the app on the PC has no vault registered. So the graph and the vault became FULL mode, asked for by
name ("update the **all** brain"), at ~1 hour and ~800k tokens a run. A daily check that reports them
as "stale" is reporting a design decision, not a fault.

**What a cloud run can genuinely check, and did, on 2026-08-29 — all green:** both repos clean and level
with `origin/main`; **all 13** consistency checks pass with no drift; **12 skills · 398 fragments · 26
native tools**; 248 fragments (62%) proven against a real model, 25 with no status either way; the
retrieval-score claims in the docs match the last recorded run (2026-08-25). AEB-Tools sits at v1.1.3,
working tree clean.

**So the check is worth keeping — it just needs its scope named.** Either move it to a session on the
Windows PC, where all four are real and `brain-update-layers` FAST mode answers it in about 30 seconds,
or leave it in the cloud and narrow it to the half that lives in git: repo sync, consistency, counts,
proven percentage. What it must not do is keep reporting three layers as "cannot verify" every morning
as though that were news.

## 2026-08-28 — two knowledge corrections from a no-Revit session. ONE LINE TO VERIFY ON THE PC.

Nothing structural. Two findings from building a write path on a machine with no Revit and no compiler,
both filed where they belong and logged in [`knowledge/brain-log.md`](../knowledge/brain-log.md).

**The one thing to actually check, and it takes a second:** `live-model/core.md` used to tell every
session to branch on the Revit version for a mm-to-feet conversion. It no longer does — for a **length**
that is plain arithmetic, `mm / 304.8`, exact in every release from 2020 to 2027, with no units API in
it at all. That kills the whole `DisplayUnitType`-fails-on-2024 failure class this Brain has already been
bitten by twice. **It is reasoned, not run.** Convert `304.8` and expect exactly `1.0`. If it does, the
rule is right and there is nothing else to do.

The other is a trap, not a claim: an unguarded `RollBack()` in a `catch` can throw a second time and bury
the error that caused it — and a `RefreshActiveView()` after a *successful* commit can report a committed
change as a failure. Both in
[`knowledge/live-model/failure-handling-without-a-class.md`](../knowledge/live-model/failure-handling-without-a-class.md).

Plugin bumped to **1.1.44** so installed copies receive both.


## 2026-08-26 — THE DAILY TOOL CHECK IS POINTED AT THE WRONG MACHINE, AND HAS BEEN SINCE IT WAS CREATED

**Nothing is broken. The routine that is supposed to notice if something breaks is the problem.**

A scheduled routine — *"Daily tool status check"*, `trig_01MjHqahHCrpcfn3xnPkFU2z`, created **2026-08-17**,
fires **15:00 UTC daily**, push notifications on — asks for four things to be checked every day:

1. AJ Tool running and accessible
2. the vector index current
3. Graphify configuration and data integrity
4. Obsidian sync and vault status

**It runs in a cloud container, and three of those four cannot be answered from a cloud container at
all.** This is not a fault that appeared today — it is the shape of the routine, and it has fired that
way roughly nine times.

| Asked for | What a container run can actually say |
|---|---|
| AJ Tool running | **No.** `.mcp.json` hard-codes `D:\Ajmal\AJ AI Brain\mcp-server\index.js` and the bridge needs Revit open. No `aj-tools-aj-ai` server exists here to ping |
| Vector index current | **No.** `semantic-index/chroma-db` is gitignored — absent from every checkout |
| Graphify integrity | **No.** `graphify-out/` is gitignored. `python tools/graph-rebuild.py --check` says *"No graph yet"* — that is the container, not the PC |
| Obsidian vault | **No.** The vault is gitignored and absent |

`brain-status.mjs` already prints this honestly rather than passing silently — *"vector index · knowledge
graph · Obsidian vault — NONE PRESENT IN THIS CHECKOUT"* — and the 2026-08-21 daily-check section further
down this file recorded the same limitation. **What nobody did was fix the routine**, so it has kept
reporting into a session with nobody reading it, and the daily push has been carrying assurance about
three layers that were never examined.

### What the run CAN check, and it was all green today (2026-08-26)

Everything that travels in git, which is the repo's own configuration and integrity:

| Checked | Result |
|---|---|
| `node tools/verify-consistency.mjs` | **All 13 checks pass, no drift** |
| `node tools/brain-status.mjs` | 12 skills · 398 fragments · 26 native tools · consistency clean; 247 proven (62%) |
| Branch vs `origin/main` | in sync, 0 ahead / 0 behind |
| MCP server code (`mcp-server`, v1.7.0) | **43 of 45 tests pass** — both failures are container artifacts, see below |
| AEB-Tools repo | in sync with `origin/main`, v1.1.3, all 14 extension `.py` files compile |

**The two MCP-server test failures are the environment, not the code, and both were read rather than
assumed:**

- `document-targeting.test.js` — `listen EACCES \\.\pipe/AJTools.AjAi.TEST.2292`. A **Windows named
  pipe**; Linux has no such thing. It cannot pass here and says nothing about the PC.
- `smoke.test.js` → *"search_graph rejects mode 'path' without both endpoints"* — the tool short-circuits
  on *"No knowledge graph at graphify-out/graph.json"* before it ever reaches argument validation, so the
  assertion never sees the message it is looking for. It is the missing gitignored graph again. Worth
  knowing but not worth changing: validating arguments before checking for the graph would make the test
  environment-independent, and that is a two-line reorder whenever someone is in that file anyway.

**Also worth knowing for any container session:** `mcp-server/node_modules` is absent on a fresh
checkout, so `npm test` first fails **5 of 20** with `Cannot find package 'zod'`. That is not a
regression — run `npm install` in `mcp-server/` first, then the real numbers above appear. A session that
reports the zod failures as defects has misread an empty `node_modules`.

### What to do about it — the decision is Ajmal's, the options are not technical

The daily check is worth having. It just has to run **where the four things live**, which is the Windows
PC with Revit open. Either:

- **Move it to the PC** — a Windows Scheduled Task running the checks locally, where `ask-brain-hybrid`,
  `graph-rebuild.py --check` and the vault are all reachable and `ping` can hit a live bridge. This is
  the one that actually answers the question as asked.
- **Or narrow the cloud routine to what it can honestly do** — repo integrity, consistency, counts,
  compile-adjacent checks, both repos in sync — and stop it claiming the other three. Then add a
  separate PC-side check for the live layers.

Doing neither leaves a daily notification that reads as four-tools-healthy while examining one.

---

## 2026-08-24 (evening) — SUITE MERGED, MCP SERVER VERSION-PROOFED, BRANCHES DELETED. NOTHING IS WAITING.

**State: `main` is at 394 fragments, plugin 1.1.33. GitHub holds `main` and nothing else — every branch deleted 2026-08-24.**
[PR #39](https://github.com/Ajmalpshaik/AJ-AI-Brain/pull/39) is **MERGED** — 6 new fragments, 8 upgraded,
18 corrected on this branch, plus 2 more corrected by a peer session. It took THREE merges of `main` to
get there: the library went 360 → 388 → 391 → 394 while the work was in flight.
Ledger: [`revitplugins-harvest.md`](revitplugins-harvest.md).

**A `git pull` on the PC now gets everything.** The one thing left is not a code question: **run the
fragments in the table below against a real model**, starting with `action-report-level-elevations.cs`.

### THE BRANCHES ARE GONE — and the four-session diagnosis was environment-specific, not a rule

**Deleted 2026-08-24 from the Windows PC, in one command each, first try.** `git ls-remote --heads
origin` now returns **`main` and nothing else.** All five `claude/*` branches went, plus six older ones
that turned out to have been deleted on GitHub already — the local `origin/*` refs were simply stale
until a `git fetch --prune`.

```bash
git push origin --delete claude/add-fragments-hvhxpt
```

**Four sessions had recorded this as impossible, and each correction was itself corrected.** The
sequence is worth keeping because the *shape* of the error repeated while the *explanation* kept
changing: first "GitHub refuses it", then "a session's git credential can push refs but not delete
them", then "an Anthropic-side GitHub API proxy blocks it, and `recentRelayFailures: []` never sees that
layer." That last one is **accurate about the container** — the REST 403 body really does say
*"Write access to this GitHub API path is not permitted through this proxy."*

What every version got wrong was the scope. The finding was written as a fact about *deleting branches*,
when it was only ever a fact about *deleting branches from a sandboxed session*. On this PC, with
Ajmal's own git credentials and no proxy in the path, the same command has never been tried — and it
just works.

**The transferable rule: an environment-specific block belongs in a sentence that names the
environment.** "Deleting branches is refused" invited four sessions to re-derive it. "Deleting branches
is refused *from a container session*" would have ended it at the first attempt, and would have said
plainly where to go instead — the PC.

Nothing was lost: every branch was measured at **0 commits ahead of `main`** before deletion, checked
with `git log --oneline origin/main..<branch>` rather than assumed from the PR being merged.

**"Automatically delete head branches" is now genuinely ON**, set from the API on 2026-08-24 and read
back fresh to prove it saved:

```bash
gh api -X PATCH repos/Ajmalpshaik/AJ-AI-Brain -F delete_branch_on_merge=true
gh api repos/Ajmalpshaik/AJ-AI-Brain --jq .delete_branch_on_merge   # must print true
```

So a merged PR now deletes its own branch and the pile-up above cannot happen again. **The read-back is
not ceremony** — this exact setting had been reported as on twice before and was off both times.

**Still true and still worth knowing:** across 2026-08-24 Ajmal reported three GitHub actions done that
measurement showed had not happened — a PR merged (still open), the branches deleted (still present),
and this setting turned on (proven off, because PR #39's head branch survived the merge). Twice the
belief came from a GitHub UI control that needs a **second confirming click** and silently does nothing
without it. **When he says a GitHub action is done, verify it before building on it** — not out of
distrust, but because the UI fails quietly and he has no way to see that it did. The API route above
avoids the whole problem: it writes and reads back in two commands.

**And the repo is PUBLIC, which the README denied for a month.** Confirmed 2026-08-24 two ways
(`gh api ... --jq .private` → `false`, `gh repo view --json isPrivate` → `false`) against a README that
had said *"This repo is private"* since 2026-07-22. Ajmal's decision, asked directly: **keep it public**
— it is what makes the one-line plugin install work — and fix the README, now done. Every tracked file
was scanned for tokens, keys and passwords when the mismatch surfaced: **nothing exposed**, the only
`API_KEY` hits being variable names in prose. Treat the repo as publicly readable from here on.

**Proven split, computed 2026-08-24 after everything below landed: 247 proven (63%), 108 flagged
not-yet-run, 28 with no status either way, 7 blocked, 4 impossible.** The proven COUNT did not move all
day — the percentage fell because the library grew by 34. Nothing here has been run on a real model since
the merge; the table further down says which to run first and what each one proves.

**The thing to know before you touch anything height-related.** `Level.Elevation` and
`Level.ProjectElevation` are two different numbers, and only the second is in the same coordinate space
as an `XYZ`. **Twenty fragments here mixed them** — the entire fire-sprinkler chain, coverage, routing,
ceiling heights, both dimensioning fragments, and the three oldest creators (`create-wall`,
`create-floor`, `create-ceiling`), which have been placing walls, slabs and ceilings at the wrong height
on survey-datum models since they were written. All fixed. On a test model the two agree and nothing
shows; on a site model with a survey offset every affected answer was wrong by that offset.
Read [`../knowledge/live-model/level-elevation-vs-project-elevation.md`](../knowledge/live-model/level-elevation-vs-project-elevation.md)
before "fixing" any remaining `Elevation` use — two of them are deliberate and say so in their headers,
and about two dozen more `.Elevation` hits in the library are correct code (sorting, report rows).

**And read the method lesson with it.** The first sweep for this defect grepped `Level\.Elevation`, which
cannot see `level.Elevation` where the variable is already a `Level` — the commoner shape. It returned
nothing and got written up as "checked and clean". The peer session found two it missed; a corrected
sweep found three more. **A grep that finds nothing is evidence about the pattern, not about the code —
prove the pattern can see a defect you already fixed before calling a sweep clean.**

**Run these first when a model is next open.** All read-only, all cost nothing:

| Run this | What it proves |
|---|---|
| `action-report-level-elevations.cs` | **Do this one first.** One line says whether this model is affected by the defect above. Everything below is easier to read once you know |
| `action-report-clashes.cs` with `linkInstanceIdInt` set | The linked-model path is brand new and is the reason this fragment matters. Needs a model with a structural link |
| `action-report-nested-families.cs` over Mechanical Equipment | Ajmal will recognise the answer instantly — if it says an AHU is one unit with three parts, it works |
| `action-report-fitting-area.cs` over duct fittings | Check ONE elbow by hand: net area ≈ duct perimeter × centreline arc. If it is roughly double, the connector subtraction did not happen |
| `action-audit-mep-openings.cs` | The biggest new capability, and the one most worth proving. **Run it on BOTH kinds of opening**: one cut by `create-mep-openings.cs` (a Revit `Opening` — a VOID, whose solid this fragment has to BUILD) and one sleeve family instance. The built-solid path is the part that needs a real model most — if a row says SUSPECT GEOMETRY, the boundary-to-solid construction is landing in the wrong place and the header says what to check |

**The compile gate now runs on Linux too.** `tools/check-scripts.cmd` on the Windows PC is still the
authority — it tests the Revit versions actually installed. But a container session can now compile
against the real shipped `RevitAPI.dll` for 2020/2024/2027 by pulling the API packages from the public
feed and running Roslyn under Mono. The scratchpad copy is gone with the session; the method is written
up in the ledger and takes about ten minutes to rebuild.

**A method lesson worth more than the code**, now in [`harvest-prompt.md`](harvest-prompt.md): a
correction is not finished until the `scripts/README.md` row moves with it. `action-compare-models.cs`
was corrected in the morning and its row still stated the wrong rule this evening — in the document a
session routes from. The consistency checker asks whether a row EXISTS, never whether it is TRUE.

**THE WHOLE LIBRARY COMPILES: 394 pass, 0 fail on Revit 2020, 2024 AND 2027** — measured
2026-08-24 at this branch's head, after every fix on both sides. That is the container gate (Roslyn under
Mono against the real shipped `RevitAPI.dll`); `tools\check-scripts.cmd` on the PC is still the authority
because it tests the Revit versions actually installed there, and is worth one run before the next job.

**THREE FRAGMENTS FROM THE OTHER SESSION DID NOT COMPILE — measured 2026-08-24, now FIXED.**
`action-check-flow-direction.cs` and `action-connect-open-connectors.cs` both use
`BuiltInParameter.RBS_SYSTEM_TYPE_PARAM`, which **is not a real API name on any Revit version** — they
could not run at all. `action-check-plumbing-fixture-connectivity.cs` used
`BuiltInCategory.OST_PlumbingEquipment`, which arrived at 2024, so it failed on 2020 only. Fixed with
`RBS_DUCT_SYSTEM_TYPE_PARAM` / `RBS_PIPING_SYSTEM_TYPE_PARAM` for the first two and `Enum.TryParse` for
the third; all three now compile on 2020, 2024 and 2027. **The lesson outlives the fix: the other
session's "all 388 compile" claim was asserted, not run — two of these failed on EVERY version. Run the
gate before writing that sentence.**

**Ajmal caught one defect himself and it is the model for how to check the rest.** He asked whether the
new opening audit would fit the opening tools we already had. It would not have: a Revit `Opening` is a
VOID with no solid, and the audit pulled solids, so `filter-by-openings.cs` → `action-audit-mep-openings.cs`
would have reported nothing for every opening our own recipe ever cut — silently. Fixed, and
`knowledge/live-model/mep-openings.md` now opens with a four-way routing table. **The lesson: the defect
was in the SEAM between the new fragment and the old one**, which neither reading the source nor the
compile gate can see. Ask the same question of the other five new fragments before trusting them.

**Still genuinely open on this repo:** the architectural and structural documentation plugins (lintel
placement, package documentation, rooms, coordination volumes, area boundaries, declarations) were
inventoried token by token but not read. Nothing absent-and-useful surfaced and Ajmal is MEP, so it is a
defensible skip — not a proven-empty one. The ledger names them with line counts.

---

## 2026-08-24 (evening) — MCP SERVER FIXED FOR ALL REVIT VERSIONS, EVERYTHING MERGED AND PUSHED

**State: 388 fragments. `check-scripts` green on Revit 2020, 2024 and 2027 — 421 scripts, which is the
388 fragments PLUS the 33 the MCP server generates, now checked together for the first time. All 13
consistency checks pass. MCP server 1.7.0, 51/51 tests green. Committed, merged with the ten commits
waiting on origin, and pushed.**

Ajmal asked whether the existing MCP server was any good. It is well built — but it had been broken on
Revit 2024 and 2027 for months and nothing had noticed.

**The bug.** The server generated `DisplayUnitType.DUT_MILLIMETERS` in eight places. That name is gone
from the API after 2020 (measured in the DLLs: 4 hits in 2020, 0 in 2024, 0 in 2027), and the bridge
compiles what it is sent, so it is a hard compile error on the first call. `model_summary` and
`move_elements` were dead on any Revit above 2020; twelve more tools died on any mm filter. Proved with
the repo's own harness rather than argued: the old line passes on 2020, fails on 2024 with `CS0122`.

**Why nothing caught it, which matters more.** The identical call was swept out of 93 fragment files on
2026-08-20 and `check-scripts` has reported green ever since — but it only ever read `scripts/*.cs`,
while roughly half the C# that reaches Revit is built at run time in `mcp-server/tools/*.js`. **A green
check is only as wide as what the checker reads.** `mcp-server/emit-generated-csharp.mjs` now writes out
every distinct script the server can generate and `check-scripts` compiles both halves. It walks
BRANCHES, not tools — three of the eight bad copies only appeared on a mm filter — and fails if a tool
has no case at all. **When you add a tool or a branch, add a case there.**

Also done: the SILENT half of the same version split (`shared/element-id.js`, reflection on
`ElementId.Value`/`.IntegerValue`); safety annotations for all 28 tools from one table in
`shared/register.js`, with `defineTool` refusing an unlisted tool; migration off the deprecated
`server.tool()`; the reported version now read from `package.json` (it had said 1.4.0 against 1.6.0);
`session_start`'s test un-stuck (it had asserted the fragment was unproven two days after it was
live-verified, so the suite was red); and both Brain search tools moved off `spawnSync`, which had been
freezing the entire server for the length of a search.

### Left undone, and why

- **The knowledge graph's document side is one merge behind.** `graphify . --update` refuses without an
  LLM API key (78 doc files need semantic extraction) — the one step `skills/brain-update-layers` already
  documents as manual. Set `GEMINI_API_KEY` (or another listed key) and re-run. The vector index and the
  Obsidian vault ARE current: rebuilt after the merge, 464 files / 6406 chunks and 2230 notes.
- **Typed output schemas for the MCP tools.** Deliberately skipped. It changes the reply shape of all 28
  tools for no benefit Ajmal would see, and it did not belong in the same pass as a version fix.
- **The open items below still need a live bridge and a model open.** Nothing was run against Revit this
  session.

### Housekeeping worth knowing

- **Ten commits were waiting on origin** when this session went to push — 28 MEP coordination fragments
  from parallel sessions, 360 -> 388. Merged here, four conflicts resolved (`plugin.json`, `CLAUDE.md`,
  `START-HERE.md`, `brain-log.md` — both sides kept in the log, nothing dropped). **Check `git status -sb`
  before starting: this repo genuinely has other sessions pushing to it.**
- `knowledge/live-model/graphic-override-precedence.md` had been flagged as an un-reviewed split
  candidate for two days *after* its review was written, because the review did not use the literal
  marker `brain-status.mjs` matches. Fixed. **If you keep a file whole on purpose, write the exact phrase
  `split-review: kept whole`** — a review nobody can read is indistinguishable from one nobody did.

---
## 2026-08-24 — SIX REPOS HARVESTED, HARVESTING PAUSED BY AJMAL

**State at the time: 360 fragments, all compiling on Revit 2020, 2024 and 2027. All 13 consistency
checks pass. Plugin at 1.1.19.** *(This entry said "NOTHING IS COMMITTED OR PUSHED — that is the first
job tomorrow." It was committed and pushed as `0c3af81` shortly afterwards. Corrected 2026-08-24
evening so the line does not read as current.)*

Ajmal's words at the end: *"no issue harvesting we will continew tomarow"*. Harvesting is paused, not
abandoned. He also asked, twice, whether it was complete — and the honest answer both times was no, so
**do not tell him it is finished without checking this list.**

### What was done today

Ledgers, all in `docs/` (outside the search index — nothing will surface them for you):

- [`revit-libraries-harvest.md`](revit-libraries-harvest.md) — 12 developer libraries, plus a tag-tool addendum
- [`explorers-and-office-suite-harvest.md`](explorers-and-office-suite-harvest.md) — five repos in three parts
- [`fragment-catalogue.md`](fragment-catalogue.md) — every fragment with a one-line description, generated

**Eight real defects were found in OUR code, which is the point of the method.** The two worth knowing:
the purge fragment was offering PAINT materials for deletion, and the clash report gave a clean bill of
health for elements it never tested. Both fixed. Also: a defect in a fragment written two hours earlier
(`GetSectionByNumber` vs `GetSectionByIndex`), caught by the second read.

**Four separate wrong "impossible" claims** — family loading, ceilings, placeholder sheets, purge. All
the same shape: an impossibility recorded against ONE Revit version, written down without naming the
version. **Assume there are more.**

### What is genuinely left

| Left | Size |
|---|---|
| Descriptors outside the relevant set (rebar, assets, print manager, point clouds) | 75 — deliberately out of scope, revisit only if a job needs one |
| Two real BUILD candidates, written up with their preconditions | depth cueing on a section; lighting power density per room |
| `action-create-from-room-boundaries.cs` cannot see rooms in a LINKED model | flagged in its header with the exact fix — **on an MEP job the rooms ARE in the link, so this fragment currently cannot do its job on Ajmal's real models** |
| `recipes/sprinkler-nfpa-grid.cs` mistakes a ROTATED room for an irregular one | grids on the project-aligned box; `action-report-room-dimensions.cs` distinguishes the two cases |

The five repos themselves are done: align-tag read in full, HOK all 18 projects verdicted, both
explorers mined for their trap lists, the add-in manager skipped on a read rather than a survey.

### The thing that matters more than any repo

> *Numbers below are as of that day and are kept as written. Today's are in the newest section at the
> top of this file, and `node tools/brain-status.mjs` computes them live — trust that over any line here.*

**247 of 360 fragments are proven (69%). That went DOWN from 74% this morning** — harvesting adds
unproven code faster than anything proves it. 63 are flagged untested and 38 have no status either way;
the second group is the dangerous one, because nothing warns you.

Three from today are READ-ONLY and cost nothing to try the moment a model is open:

- `action-report-mep-pressure-drop.cs` — on one system, checked against Revit's own System Inspector
- `action-report-ceiling-heights.cs` — on one room whose height is known. If it says "no ceiling" for a
  room that has one, that is the Upper Limit case it exists to catch, and pass 2 should find it
- `action-purge-unused.cs` in dry run — confirm painted materials no longer appear as unused

**Suggest proving before harvesting again.** A library where a third has never run will surprise him
mid-job, which is the exact problem this Brain exists to prevent.

### Housekeeping

- A second Claude session was working in parallel today in `knowledge/`, `recipes/`,
  `actions/structural-changes/` and `actions/qa-checks/`. Some fragments on disk are theirs. The rule
  learned from it is now in `CLAUDE.md`: private per-file work first, shared files (README rows, counts,
  brain-log, plugin-release) in one pass at the end.
- The harvest scratchpad clones are in the session temp folder and will be cleaned up automatically.

---
## 2026-08-23 — FOUR REPOSITORIES HARVESTED, EVERYTHING PUSHED

**State: 351 fragments, all compiling on Revit 2020, 2024 and 2027. All 13 consistency checks pass.
Committed and pushed to origin/main; working tree clean; plugin version bumped so installed copies
receive it.**

Four source repositories were gone through, each with its own ledger in `docs/` (outside the search
index, so nothing surfaces them for you — they are listed here on purpose):
`pyrevit-harvest.md`, `pyrevit-platform-harvest.md`, `rag-addin-harvest.md`, `book-samples-harvest.md`,
and `revit-libraries-harvest.md` written in parallel by a second session working in the same repo.

**Nothing about the harvests is outstanding.** All four are fully read and every tool has a verdict.

**What IS outstanding, and it is the same shape as everything below: none of the new fragments has been
run against a real model.** Roughly 30 fragments were added today. Every one compiles, every one is
read-only or dry-run by default, and every one says so in its own header. Compiling is a floor, not a
ceiling. The ones most worth proving first, because they are the ones a real job will reach for:

| Run this first | Why it is the one that matters |
|---|---|
| `action-set-link-overrides.cs` | Closes a real hole: `recipes/mep-grayout.cs` never handled LINKED models, so on the normal coordination setup it greyed nothing and reported success. Needs a model with a link — `school.rvt` has none |
| `action-audit-view-filters.cs` | Read-only, so it is the safest first run of the batch. Needs a view with filters on it |
| `action-report-curtain-elements.cs` | Read-only. Needs a curtain wall — check its panel count against Revit's own schedule |
| `action-export-families.cs` | Writes .rfa files to disk, never touches the model. Try `maxFamilies = 3` first and open one of the results |

**The one improvement deliberately left unbuilt, with the reason:** the RAG add-in harvested today
rewrites a plain-English question into several technical Revit-API queries before searching. That is a
direct answer to the one weakness `CLAUDE.md` records about our own search — site vocabulary the files
do not use — and `knowledge/glossary.md` is already the map to expand from. It belongs to
`semantic-index/`, whose whole discipline is that changes are measured against the 28-row set in
`semantic-index/score-history.md`. **Building it without a before-and-after on that set is exactly the
unmeasured change that file exists to prevent.** Next session on that layer, with the eval open.

**A note on working in this repo:** two Claude sessions were writing to it simultaneously today. It was
survivable — the consistency checker and the compile gate both caught everything — but it changed a
verdict once (a fragment planned as a BUILD became an UPGRADE when the other session's version
appeared). **Check what is on disk now, not what was there when you started.**

---

## 2026-08-22 — RUN_FRAGMENT AND FIVE NATIVE TOOLS (run_fragment + session_start now proven; grayout not)

**What it is.** A new MCP tool, `run_fragment`. Name one or more fragments, pass their input values, and
the `.cs` files go to Revit **byte-identical apart from their `INPUTS` declarations**. It replaces the
read-the-file / hand-edit / paste-into-`run_csharp` loop that every scripted job used until now.

**Why it was built, in one line:** "PROVEN" described a file that was never the thing that ran. Every job
sent a retyped copy. Two Revit round trips were lost to exactly that on 2026-08-20 alone.

**What is proven, and it is all off-model.** 14 tests, `mcp-server/test/run-fragment.test.js`, all
passing — including a sweep that rewrites **all 247 input-bearing fragments** and asserts the declared
types, names and comments come through unchanged, and a byte-identical check on everything outside the
INPUTS block. It has **never been run against a real Revit document.**

**So do this first, and it is two minutes.** Ajmal must close and reopen Revit only if the add-in
changed — it did not, this is Node-side only — but the MCP server must be reloaded to see the new tool.
Then, on the test model:

1. `run_fragment(describe: "Count the ducts", fragments: "filter-by-category",
   inputs: {targetCategory: "OST_DuctCurves"}, preview: true)` — read the composed script, confirm it is
   the fragment plus one `return sb.ToString();`.
2. Same call without `preview` — check the count against `count_elements` for the same category. Two
   independent mechanisms agreeing is the proof.
3. A composed one: `fragments: ["filter-by-category", "action-set-color-uniform"]`, with
   `colorR/colorG/colorB`. This exercises the multi-field declaration line, which is where the one real
   bug was found while building it.

Then mark it in `knowledge/brain-log.md` as live-verified, with the numbers.

### Five native tools came out of the same session (21 -> 26)

`grayout` · `session_start` · `verify_connectivity` · `report_length_by_size` · `color_by_group`.
Each is a typed front door onto ONE proven fragment — **they contain no Revit code**, so there is never
a second copy to drift from the original. Table and reasoning:
[`mcp-server/tools/README.md`](../mcp-server/tools/README.md).

**`grayout` is the fastest way to prove the whole chain at once.** Its recipe declares 33 inputs and
32 of them ARE the settled standard, so the tool sets one value and leaves the rest byte-identical.
Run `grayout()` on a coordination view: if the view greys correctly, then `run_fragment`, the shared
composition engine and the native-tool wrapper are all proven in one shot.

**`session_start` wraps an unproven fragment** (`context-session-start.cs`, never verified against a
real model). Not a blocker — it says so in its own result — but it is the one to check the output of
rather than trust.

**They were picked from the wrong evidence, and that is on the record.** Ajmal's job log never travels
in git, so from the container they were ranked by how often each fragment is named across the skills
plus AGENT-SPEC §9.4. `node tools/job-report.mjs` gives the real ranking. The 89% `run_csharp` figure
in the 2026-08-21 section is the measurement that should have chosen them — re-check before promoting
any more.

### And a shortlist now rides on every message

[`tools/shortlist.mjs`](../tools/shortlist.mjs), injected by `auto-search-hook.mjs`: Ajmal's most-used
fragments, ranked from his last 30 days of real work, so the everyday jobs skip the search entirely.
**0.17 ms on a real log, 2.0 ms on a simulated year, ~92 tokens.** It shows nothing in a fresh
checkout because the log lives only on the PC — `node tools/shortlist.mjs` there will show the real one.

**One bug fixed that this depends on:** `job-log-revit.mjs` read fragment names out of the pasted
`code`, which only `run_csharp` has. `run_fragment` NAMES its fragments, so every call through the new
tool was recording zero — the usage record was quietly becoming a record of the old way only.

**Known limits, recorded rather than hidden:**
- It does **not** compile-check. Only Revit and `tools\check-scripts.cmd` do. It validates the form —
  names, inputs, types, whether the composition is legal C# structurally.
- 43 of the 290 fragments have no `INPUTS` block; those still go through `run_csharp` unchanged.
- Two filters cannot be composed (both declare `sb` and `elements`) — it refuses with that message,
  which is correct, not a bug to fix.

**The two follow-ups this makes cheap**, both agreed as the ranking on 2026-08-22 and neither started:
promoting the ~10 remaining everyday jobs to native tools (`AGENT-SPEC.md` §11 names them), and the
test-set work that would let the cross-encoder and skill-weighting be judged (`semantic-index/
rag-architecture-decisions.md`, "What is actually limiting the search").

**One thing to check on the PC that is not code.** `.mcp.json` in the repo hard-codes
`D:\Ajmal\AJ AI Brain\mcp-server\index.js` — that is SETUP.md's Option B, the per-folder path. If the
Brain is running that way rather than as an installed plugin, it only exists when that folder is open.
Installing it as a plugin (SETUP.md step 1, Option A) makes all 11 skills and the bridge available in
every project on the machine. **Do not run both** — same server key registered twice.

## 2026-08-22 — a documentation-only session, and what it left for you

No Revit, no bridge, no model touched. The whole session was one job: **find every place this Brain states
a number about itself and check it against disk.** It found a lot, and the durable outcome is not the
fixes — it is four new checks that make the same drift impossible to ship again.

### What was wrong

| Where | What it claimed | Truth |
|---|---|---|
| 8 documents | 267 / 269 / 282 / 285 fragments | **290** |
| 3 places | 17 or 19 native tools | **20** at the time; **26** once `run_fragment` and the five fragment-backed tools merged the same day |
| 19 fragment headers | "NOT YET LIVE-VERIFIED" | proven live 2026-08-06/07, recorded in `scripts/README.md` only |
| 7 fragment headers + 4 docs | named outside tools | stripped, per the 2026-08-20 rule that never reached `scripts/` |
| `README.md` | search scores `3/14 at #1` | **5/28 at #1, MRR 0.267** — and `6/14 in top 5` matched no run at all |
| `tools/api-surface.mjs` | stamped every regeneration `2026-08-20` | a hardcoded literal; now reads the clock |
| this file | 270 / 283 / 287 fragments, `3/14` | stamped or corrected above |

### The four checks added, all tested by deliberately breaking a file first

- **9** — every fragment / skill / native-tool count stated anywhere in markdown, against disk.
- **10** — each fragment's own header status against its `scripts/README.md` row (241 verified fragments).
- **11** — outside-source names in `scripts/`, `skills/` and `knowledge/` (344 files). Found 3 more on its
  first run, in a file the hand-written grep had never looked at.
- **12** — any `N/M at #1` retrieval score against the last run in `semantic-index/score-history.md`.

`tools/verify-consistency.mjs` now runs **13 checks** and the edit hook runs it on every file change. The
PowerShell copy trails at 8 and the docs say so — do not port checks 9–12 from a Linux container, that is
the `.ps1` encoding trap this repo has already been bitten by twice.

### Decisions taken by Ajmal this session, so they are not re-argued

1. **KV cache / prompt caching — nothing to build.** Every layer that pays is already on by default or
   already built and measured here. Full record in `semantic-index/rag-architecture-decisions.md`.
2. **`knowledge/dynamo-vocabulary-map.md` deleted** — *"we dont have anyting related to dynamo."* Git has it.
3. **The NFPA source links stay** — *"keep the nfpa links."* Fire-code values whose own heading warns they
   are secondary summaries; a blockquote in that file now records the decision, and check 11 skips it.
4. **brain-log.md keeps its full detail** — it is 11% of the searchable corpus; at 20%, move entries older
   than 60 days to `docs/brain-log-archive.md` (outside the index). `brain-status.mjs --full` prints the
   share every session. Move, never shorten.

### What Ajmal still has to do — none of it possible from a cloud session

1. ~~**Merge PR #30.**~~ **DONE 2026-08-22** — merged, along with PR #31 (the daily check's PowerShell
   port). **All three merged `claude/*` branches still exist, and a cloud session cannot remove them.**
   Diagnosed properly 2026-08-22 rather than guessed: `git push origin --delete` returns **HTTP 403**,
   and the egress proxy's own `recentRelayFailures` log stays **empty** — so the request reached GitHub
   and *GitHub* refused it. The session's git credentials allow pushing refs but not deleting them, which
   is a deliberate guardrail, not a misconfiguration; the proxy README says to report a 403 rather than
   route around it. The GitHub API tools available here have `create_branch` and no delete counterpart.
   **Two ways to clear them, both yours:** delete each on GitHub (Branches → the bin icon, ~20 seconds
   total), or better, turn on **Settings → General → "Automatically delete head branches"** so every
   future merged branch cleans itself up and this never comes back. Verified safe first — each branch is
   **0 commits ahead of `main`**, so nothing is lost either way.
2. **Run `tools\check-scripts.cmd`.** One fragment was rewritten this session without a compiler:
   `filters/by-identity/filter-by-wrong-category.cs` used `ElementId.IntegerValue`, removed at Revit 2024.
   It was written on 2026-08-21, *the day after* the whole library was migrated off that API — which is why
   check 11's lesson is that a sweep fixes what exists and does nothing about the next file somebody writes.
3. **Two more test questions, in his own words.** The set is at **28 of 30**. Two finished features — the
   cross-encoder re-ranker and skill weighting — are switched off only because the set is too small to
   judge them. `semantic-index/rag-architecture-decisions.md` records that assistant-written questions do
   not count, so this one genuinely cannot be done for him.

Everything below this line is the 2026-08-20 Windows session and is unchanged.

---

This replaces the 2026-08-14 handover, which
had gone stale (it said 270 fragments; there were 287 that day, and 290 now — which is the point).

## 2026-08-21 — the search was measured, corrected and made ~5x faster

Ajmal walked a RAG checklist through the Brain under one rule: *check whether we already have it,
take the better version, and say which.* Most of it already existed. The value came from measuring
things nobody had measured, and several claims the Brain made about itself turned out to be false.
Full working, with every number: [`semantic-index/rag-architecture-decisions.md`](../semantic-index/rag-architecture-decisions.md).

**Built:** automatic spelling correction (65% of real questions contain a word in no file, mostly
ordinary Revit words typed fast); a warm search server so the embedding model loads once, not on
every message; a disk cache for the fragment index; a corpus/BM25 cache inside `hybrid_search`; a
duplicate check on every build; build provenance stamped into the manifest.

**Fixed, and these mattered more than the features:**
- **A rebuild could mark itself finished before it had.** The manifest was written before the build
  was verified; a crash left **1,200 chunks of 3,895** reporting "UP TO DATE". Both paths now count
  first. General rule: *write the "this is finished" record last.*
- Derived files now write-then-rename — a search during a re-index could read a half-written file.
- A black console window appeared over Revit on every message (PR #29). The venv `python.exe` is a
  Store-Python launcher shim that loses the detach flag; `pythonw.exe` fixes it. Same shim, same fix
  as the voice layer made on 2026-08-11.

**Claims corrected:** the live embedding model is `all-MiniLM-L6-v2`, not BGE (which is still not
downloaded and still has no score line); the cross-encoder is not inert — it changes the #1 answer on
**88%** of real questions, so it stays off for a much better reason; `site-vocabulary.md` rows
**replace** the matched phrase, they do not merely add to it.

**Speed, measured:** per message **3,536 ms -> ~650 ms**; consistency check per edit
**3,720 -> 1,307 ms**; end-of-turn hooks **2,105 -> 900 ms** quiet, ~31 s -> ~20 s busy.

### What changed in the open items

- **The test questions are DONE, and the older sections below are wrong about this.** The set went
  from 14 to 28 rows, taken from `job-log/questions.jsonl` — Ajmal's own real questions, spellings
  kept. First 28-row baseline: **5/28 at #1, 7/28 in top 3, 20/28 retrievable, MRR 0.267.** Not
  comparable to any 14-row line. **8 rows fail outright and 10 more sit below #5**, which is the
  material the parked decisions were waiting for.
- **Still owed by Ajmal, and only by him:** check the 14 new expected answers. They were drafted by
  reading the library, never by running the search, and are marked as the assistant's reading in
  `semantic-index/test-questions.md`. A wrong target quietly teaches the search the wrong thing.
- **Now unblocked — four sweeps that could not be decided on 14 rows:** the cross-encoder,
  BGE against MiniLM, the meaning-versus-words balance (`WEIGHT_MEANING`/`WEIGHT_WORDS`, both 1.0 and
  never swept), and `RRF_K` (60, inherited, never tested here). Sweep against the 28-row set, never
  the old one, and remember `AREA_WEIGHT` — a sweep that "wins" by moving points between halves of a
  small set is fitting the sample.
- ~~**Not started, and the last speed item:** 89% of logged Revit calls are `run_csharp`, compiling
  fresh C#, while 20 native tools sit nearly unused.~~ — **ADDRESSED 2026-08-22, and this measurement
  is what justifies it.** `run_fragment` runs the proven library by name instead of pasting retyped C#,
  and five everyday jobs became native tools (21 → 26). That 89% is the number to re-measure once both
  have run on a real model — `node tools/job-report.mjs` shows the split by tool. **It is also the
  usage evidence the five tools should have been picked on**, and were not: the job log never travels
  in git, so they were ranked by how often each fragment is named across the skills instead. Check them
  against the real ranking before promoting any more.

---

---

## The prompt

```text
Read D:\Ajmal\AJ AI Brain\docs\HANDOVER.md and CLAUDE.md first, then continue the work listed there.
```

---


## THE BRIDGE NOW HANDLES SEVERAL REVITS AND SEVERAL PROJECTS — 2026-08-20, end of session

**Do this first: Ajmal must close and reopen Revit.** The add-in only loads at Revit startup, so
**AJ Tools 1.56.0 is built and deployed but not yet running** in the session that was open.

### What changed, and why it was two problems

| Question | Fix | Version |
|---|---|---|
| Which **Revit session**? | one named pipe per process id | AJ Tools **1.55.0** |
| Which **project inside it**? | optional `document` title on the request | AJ Tools **1.56.0** |

The bridge used to assume exactly one Revit: every session hosted the same pipe name, which has room for
two server instances — and one Revit uses BOTH (one servicing the chat, one listening so preemption is
instant). **Measured, not inferred:** creating four servers on one name gives two, then
`All pipe instances are busy`. So a second Revit's bridge simply refused to start.

The second half is subtler and was still live after the first fix: `RevitExecutionService` builds its
globals from `app.ActiveUIDocument`, so `Document` means **the front window**. With two projects open in
one Revit, clicking the other one silently moved where the next script landed.

**The rule both halves share: a name that does not resolve is an ERROR listing the real choices, never a
fall back to whatever is in front.** Falling back IS the failure.

**Ajmal's rule for choosing, asked and answered:** *ask, don't guess.* One Revit, one project — no
prompt, behaves exactly as it always did. More than one — nothing is sent until he names one.

### Three new tools (native tools 17 → 20)

`list_revit_instances` · `use_revit_instance` · `use_revit_document`. They read
`%APPDATA%\AJTools\bridges\<pid>.json`, which each Revit writes with its version and open document.

### Proven live on 2026-08-20, and what is NOT

**Proven:** two Revits (`school.rvt` pid 1320, `vila.rvt` pid 23876) hosted bridges **simultaneously**,
and 10 sheets were created in each — `school 01..10`, `vila 01..10` — switching between them mid-job and
verifying each by reading it back. That is the thing that was impossible before.

**NOT yet proven, and it needs Ajmal:**

1. **Document targeting has never run.** 1.56.0 was built after that test, so the running Revit did not
   have it. Open **two projects in ONE Revit**, then pin one with `use_revit_document` and confirm a
   write lands in the named project no matter which window is in front.
2. **The three tools have never been called.** They were added after this session's mcp-server had
   already loaded, so the switching on 2026-08-20 was done by hand — by rewriting `ajai-bridge.json` to
   point at the other instance file. A fresh chat gets the real tools.

### Known limit, recorded rather than hidden

A `UIDocument` for a background project still carries **that project's** active view. So view-scoped work
(isolate, colour, **grayout**, crop) follows Revit, not the caller. Model work — create, rename,
parameters, schedules — is unaffected. Do not quietly widen this claim.

### Small thing worth doing

Both `school.rvt` and `vila.rvt` still have **Project Name and Project Number unset** (Revit's literal
placeholder text). Title blocks normally read those fields, so sheets print with blanks.

---

## UPDATE — 2026-08-20, later the same day, ON the Windows PC

The remote session above was picked up on the PC. **Step 1 and step 3 below are now done, and both
were blocked by bugs that had to be fixed first.** Read this section instead of taking steps 1 and 3
at face value.

### Two things were broken on arrival, both now fixed

1. **The search was dead.** The remote session made `bge-small-en-v1.5` the *default* embedding model,
   but its weights are a separate ~127 MB download that had never been fetched. Every single
   `ask-brain-hybrid` call died with "model has not been downloaded yet". A default that requires a
   download before the tool runs at all is not a default, so `brain_common.py` now defaults to
   `all-MiniLM-L6-v2`, which ships inside chromadb and always works. BGE is still one env var away —
   which is all the A/B ever needed. Index rebuilt on MiniLM: **348 files, 3750 chunks**, search
   verified working.

2. **`tools/check-scripts.cmd` could not run at all** — the very command step 1 calls "the single
   command that answers the whole session". It was written in the Linux container as UTF-8 **without a
   BOM**, and Windows PowerShell 5.1 reads a BOM-less file as ANSI, so the eight em dashes in it
   corrupted and broke the string terminators. This is the exact trap `CLAUDE.md` warns about. Fixed by
   adding a UTF-8 BOM — which is what the three `.ps1` files that already worked all have.
   **Lesson for any future container session: a `.ps1` written there needs either a BOM or pure ASCII.**

### What step 1 actually found, once it could run

| Revit | Result |
|---|---|
| 2020 | **281 of 287 compile** — 6 real failures |
| 2024 | **274 of 287 compile** — 13 real failures |
| 2027 | 4 of 287 — **a harness bug, not 283 broken fragments. See below.** |

So the version-proofing largely held. It did not hold everywhere.

**Revit 2027 is a harness problem, and it is well understood.** Every failure is
`error CS0012: The type 'Object' is defined in an assembly that is not referenced. You must add a
reference to assembly 'System.Runtime, Version=10.0.0.0'`. Revit 2027 runs on **.NET 10**, so its
`RevitAPI.dll` references `System.Runtime 10.0.0.0`, while `verify-fragments-compile.ps1` still compiles
against the .NET Framework reference set. The checker's own advice covers this case exactly: *"unless the
error names a type the harness failed to supply"*. **Fix the harness to pass the .NET 10 reference
assemblies for 2027+; do not touch the fragments themselves.**

**The real fragment failures — these are genuine and worth fixing:**

Fails on **both 2020 and 2024** (fix these first):

- `recipes/mep-grayout.cs` — **this one matters most.** It is Ajmal's own standing "do the grayout" job.
- `actions/sheets-views/action-report-schedule-definition.cs`
- `actions/structural-changes/action-place-accessory-on-run.cs` — already a known open item, see below.
- `context/context-model-categories.cs`
- `recipes/connect-equipment-to-air-terminals.cs`
- `recipes/sprinkler-layout-options.cs`

Fails on **2024 only** (7 more): `action-add-project-parameter.cs`, `context-project-units.cs`,
`create-floor.cs`, `filter-by-tag-status.cs`, `create-equipment-family-from-datasheet.cs`,
`create-parametric-box-family-with-duct-connector.cs`, `tag-elements-in-active-view.cs`.

Error codes are `CS0122` (member not accessible in that version) and `CS1061` (member does not exist)
on 2024, plus `CS0308`/`CS0030`/`CS0019`/`CS0029`/`CS1503`/`CS1501` shared with 2020 — all genuine
per-version API differences, not unit or id problems. Re-run `tools\check-scripts.cmd` any time; the full
error text lands in `fragment-compile-failures.txt` at the repo root (gitignored).

### So the next job, in order

**DONE on the PC, 2026-08-20. Every Revit installed on this machine now compiles the whole library:**

| Revit | Result |
|---|---|
| 2020 | **SAFE — 287/287** |
| 2024 | **SAFE — 287/287** |
| 2027 | **SAFE — 287/287** |

Three separate problems were behind the original failures, and they needed three different answers.

**1. Migration slips (6 fragments).** The version-proofing pass left `int` where its own new code returns
`ElementId`, each time on the line *directly after* a correctly migrated one — `mep-grayout.cs` is the
clearest: `ElementId wallId = IdOf(...)` then `int doorId = IdOf(...)`. One word each. Plus three
`(BuiltInCategory)someId` casts that needed `(BuiltInCategory)IdValue(id)`, which is what the prelude's
helper is for. `sprinkler-layout-options.cs` was not a version bug at all — 9 fields in a `System.Tuple`,
which stops at 8, so it had **never compiled on any version**; now a named ValueTuple.

**2. Real API removals (13 fragments).** These compiled on the old Revit *because* they used the old
surface. Every one is now resolved **by name at run time**, so a single source still serves 2020:

| Removed | Replacement |
|---|---|
| `ParameterType` | `SpecTypeId` |
| `DisplayUnitType` | `UnitTypeId` (electrical — the one conversion that cannot be arithmetic) |
| `UnitType`, `FormatOptions.DisplayUnits`/`UnitSymbol` | `GetAllMeasurableSpecs()`, `GetUnitTypeId()`, `GetSymbolTypeId()` |
| `IndependentTag.TaggedLocalElementId`, `.LeaderElbow` | `GetTaggedLocalElementIds()`, `Get`/`SetLeaderElbow(Reference)` |
| `Document.Create.NewFloor` | `Floor.Create` |
| `BuiltInParameterGroup`, `Definition.ParameterGroup` | `GroupTypeId`, `GetGroupTypeId()` |
| string-rule `caseSensitive` argument | dropped at 2023 — comparison is case-insensitive by definition |

The overload is always chosen from the **real method's own parameter type**, never by testing whether
`ForgeTypeId` exists — Revit 2021 ships `ForgeTypeId` while those methods there still take the old enum.

**3. A harness fault that looked like 283 broken fragments.** `verify-fragments-compile.ps1` compiled
against the .NET **Framework** reference set while Revit 2027 runs on **.NET 10**, so every fragment
failed with `CS0012 ... System.Runtime 10.0.0.0`. It now detects a .NET-based Revit from the
`RevitAPI.runtimeconfig.json` beside `RevitAPI.dll` and uses the matching reference pack with
`/nostdlib+`. **If a future Revit reports every fragment failing, suspect this first.**

### The one thing that could NOT be fixed, and should not be re-attempted

**`creators/create-hvac-zone.cs` cannot work on Revit 2027.** Not a naming change — the capability is
gone: `Autodesk.Revit.Creation.Document` has no zone method left, `Zone.AddSpaces` does not exist, and
`Space.Zone` is read-only. On 2027 an HVAC Zone must be created and filled through the Revit UI. The
fragment compiles there and reports exactly that. It still works normally on 2020 and 2024.

That was settled by **reading 2027's own `RevitAPI.dll`**, using the new
[`tools/probe-revit-api`](../tools/probe-revit-api/README.md). It exists because Windows PowerShell 5.1
cannot load a .NET 10 assembly at all, so the usual `Assembly::LoadFrom` one-liner fails on exactly the
versions whose API has moved most. Use it before assuming a member was renamed.

### What is still open

1. **Nothing here has been run against a live model.** Step 2 below is untouched and is the only thing
   that proves correctness. The reflection work deserves it most: a differently-shaped API can compile
   perfectly and still act on the wrong element.
2. ~~Ajmal's 15 test questions for the search~~ — **DONE 2026-08-21**, the set is now 28 rows. See the 2026-08-21 section at the top of this file. What is still owed is Ajmal checking the 14 drafted expected answers.

**Still true and unchanged: compiling is a floor, not a ceiling.** None of the 2026-08-20 work has been
run against a real model yet.

## What happened on 2026-08-20 — and the one thing that matters about it

A lot changed, and **none of it has been compiled or run.** The whole session happened in a Linux
container with no Revit, no C# compiler and no access to Autodesk's sites. Everything was verified
statically — carefully, but statically.

So the brain-status line saying **83% verified describes the state BEFORE this session.** Treat every
fragment touched on 2026-08-20 as unproven until step 1 below says otherwise.

What changed:

1. **The whole library was made version-proof** (287 fragments that day; 290 now) — one source now runs on Revit 2020 through 2027. 202
   unit conversions became arithmetic (`mm / 304.8`, which no Revit version can deprecate) and element
   ids stayed as `ElementId` instead of being turned into numbers. No `#if`, no fork.
2. **A new opening check** — `scripts/context/context-session-start.cs`, one call that reports the Revit
   version, which API generation is live, the document, what unit the project displays, unloaded links,
   closed worksets, warnings.
3. **The search was measured properly for the first time** and a real bug fixed (RRF was fusing chunk
   ranks while ranking files). The embedding model is now selectable. **Superseded the same day:** BGE was
   briefly the default and broke search on the PC — see item 1 above. The default is `all-MiniLM-L6-v2`
   again, and **BGE still has no score line at all.**
4. **A separate Revit API index** in `api-index/`, fed by reflecting over the running Revit's own
   `RevitAPI.dll`. Deliberately a different database — the Brain's search never reads it.
5. **`recipes/audit-flex-curves.cs`** — flex duct and flex pipe had no fragment at all.
6. **`tools/check-scripts.cmd`** — see step 1.
7. **Two sprinkler fragments merged in from PR #17** — `sprinkler-pipe-schedule-size.cs` (pipe sizing by
   the schedule method) and `sprinkler-set-room-hazard.cs` (hazard class recorded per Room). They were
   written before the migration, so they were brought in line the same way when the branch merged:
   arithmetic units, `ElementId` keys throughout. Step 1 covers them like everything else. Both also
   need a live run — the pipe one needs modelled sprinkler pipe connected to heads, and the hazard one
   needs three text project parameters bound to Rooms through the Revit UI, which no script can create:
   `FF_Hazard_Class`, `FF_Hazard_Source`, `FF_Standard`.

---

## What I want you to do, in this order

### 1. FIRST, AND IT TAKES A MINUTE — does the migration hold?

```
tools\check-scripts.cmd
```

Double-click it. Revit does **not** need to be open and nothing is changed. It finds every Revit on the
PC and compile-checks every fragment against each, then says in plain words which versions are safe.

**This single command answers the whole session.** Green everywhere means the version-proofing worked.
Any FAIL list is a small fix — send it to Claude, because every change follows one of three patterns, so
one fix usually applies across the library.

Do this before anything else. Everything below is wasted effort if step 1 is red.

### 2. DONE 2026-08-20 — the migration holds on a live model, and the id assumption is confirmed

Ran on Revit 2020, model `Project1` (3,262 elements, 4 rooms, 2 levels). **`{l.Id}` prints the bare id
number exactly as `{l.Id.IntegerValue}` used to** — observed as `Id 316` for the active view and
`Id 918776`…`918785` for the rooms, with no `Autodesk.Revit.DB.ElementId` wrapper text and no braces.
**That settles the assumption the whole 211-site migration rests on: no further change needed.**
`context-session-start.cs` ran clean and reported Revit 2020 / 32-bit ElementId / DisplayUnitType, no
links, 0 warnings. UNITS printed `level 'Level 1' elevation displays as: 0` — a bare `0` with no unit
suffix, which is what this project displays; every mm figure in the session was converted explicitly
with `/ 304.8` regardless, per START-HERE rule 3.

Also proven the same session, well beyond the pilot: the four fire-sprinkler fragments (survey, grid,
place, audit) — 38 heads placed and independently audited. See the foot of `knowledge/brain-log.md`.
**Still true: none of the 2024-only reflection fixes has been run live.**

<details><summary>The original step 2 instructions, kept for the parts not yet done</summary>

```
ping
scripts/context/context-session-start.cs      <- read the UNITS line and the LINKS line carefully
scripts/context/context-levels-and-grids.cs   <- the pilot: do ids still print as numbers?
```

The pilot exists to check one assumption the whole migration rests on: that `{l.Id}` prints the id
number the way `{l.Id.IntegerValue}` used to. If the ids look wrong in that output, say so — it changes
211 places and I will fix them a different way.

Then run something heavier — `mep-grayout.cs` or the tagging recipe — because those exercise the parts
the simple ones do not.

</details>

**Still owed from step 2:** `mep-grayout.cs` and the tagging recipe have not been run live. They were the
two named as exercising the parts the simple fragments do not, and `mep-grayout.cs` is Ajmal's own
standing "do the grayout" job — it was one of the six fixed on 2026-08-20 and has only been compiled.

### 3. Is the new search model actually better?

**Still owed. It is the one measurement this Brain has never taken.**

```
cd semantic-index
venv\Scripts\python.exe embed_bge.py --download     (~127 MB, once, needs huggingface.co)
venv\Scripts\python.exe embed_bge.py                (smoke test, should print PASS)
echo bge-small-en-v1.5 > embed-model.txt            (pin it, so search and index agree)
index-brain.cmd --full                              (~80 s)
score-brain.cmd
```

**The number to beat, as of 2026-08-21, is `5/28 at #1, MRR 0.267` on `all-MiniLM-L6-v2`** (the test set doubled from 14 rows to 28 that day, so it is not comparable to the `3/14, MRR 0.325` this line used to quote) — in
`semantic-index/score-history.md`, stamped with the model that produced it.

**Read the note at the bottom of `score-history.md` before you judge the result.** That row is
knife-edge: measured 2026-08-20, `what does duck mean` flips between #1 and #6 on a **2-chunk** corpus
change, which is 7% of a 14-row score all by itself. **A 1-point move either way proves nothing.** If BGE
wins or loses by one, it is a tie — say so rather than adopting or rejecting on noise.

If BGE loses, delete `embed-model.txt` and rebuild. That is exactly why the model was made selectable
rather than replaced.

### 4. Optional — build the API reference index

```
run scripts/context/harvest-revit-api.cs through the bridge   (edit outputRoot first)
api-index\index-api.cmd
api-index\ask-api.cmd "how do I collect every element of a category"
```

Nice to have, not blocking. Ask it about signatures; ask the Brain about jobs.

### 5. Only Ajmal can do this — and it unlocks the most

Add about **15 more rows to `semantic-index/test-questions.md`**, in your own site words. Two things are
switched OFF and waiting on it: the cross-encoder re-ranker, and a skill-weighting that measured well but
could not be trusted on 14 questions. Both are built. Neither can be proven at this sample size.

Write them the way you would say them out loud. The file explains why they must be yours and not the
assistant's.

---

## Since this handover was written — merged from the review branch (PR #22)

- **`semantic-index/setup.sh`** — one command sets the search up on Linux/macOS. Nothing to do on the
  PC; it exists so a container session is not searching blind. Proven on a wiped venv.
- **Model choice can be pinned** in `semantic-index/embed-model.txt` (git-ignored, per-machine), so an
  upgrade to BGE survives without setting an env var on every command — the failure item 1 above
  describes, in its other direction.
- **`docs/superpowers/` is gone** — two plans and a spec with 60 unticked boxes for work that is
  entirely built. The five genuinely live items moved into `semantic-index/rag-architecture-decisions.md`.
- **`semantic-index/rag-architecture-decisions.md`** is new: why the retrieval layer is not being
  rewritten, the six standard upgrades already measured and reverted here, and the folder-structure
  verdict. Read it before anyone proposes rebuilding the RAG.

## Still open from before — carried forward, still true

- `actions/structural-changes/action-place-accessory-on-run.cs` — METHOD proven, but the file as one
  uninterrupted run threw an unisolated null reference. Read its STATUS block. Suspect: an element handle
  reused across a transaction boundary after `BreakCurve`.
- `recipes/ray-trace-to-ceiling.cs` — **an ASK, not a wait.** `Ceiling.Create` has 0 overloads before
  2022. Draw ONE ceiling by hand, ten seconds, then it runs.
- A nested shared family and a sleeve family — the last two genuine fixture blocks. Both could be
  authored via `Application.NewFamilyDocument`, which is proven to work.
- Graphify's markdown half needs a subagent pass or `GEMINI_API_KEY`. The authoritative staleness count
  is `python tools/graph-rebuild.py --check` **on Windows** — it cannot be run from a container. Note
  `brain-log.md` reports stale on nearly every run because it is written every session; judge by the
  other files.

## From the daily check — 2026-08-21, container run

**One thing needs a single run on the PC.** `tools/verify-consistency.ps1` gained a ninth check
(fragment-count claims in CLAUDE.md / START-HERE.md / README.md, which were stale in six places against
290 on disk and are now fixed). The Node half is negative-tested and passing. **The PowerShell half has
never been parsed** — the container has no PowerShell, which is the exact blind spot `CLAUDE.md` records
after `check-scripts.ps1` and `verify-fragments-compile.ps1` both shipped broken this way. It is
ASCII-only and the file's UTF-8 BOM is intact, so the *encoding* trap is ruled out; the parse is not.
Run `tools\verify-consistency.ps1` once and confirm it prints nine checks and "All checks passed".

**Unchanged and expected, not a fault:** the vector index, the Graphify graph and the Obsidian vault are
gitignored, so a container run sees their code and none of their state. `brain-status.mjs` says so out
loud since 2026-08-20 rather than passing silently. Their freshness is answerable **only on the PC** —
`semantic-index\ask-brain-hybrid.cmd` (its STALE INDEX banner compares content) and
`python tools/graph-rebuild.py --check`. The daily routine cannot settle those three from the cloud.

## Housekeeping

- ~~A stray remote branch `claude/revit-api-surface` needs deleting by hand.~~ **Done on the PC,
  2026-08-20.** Verified first rather than trusted: `tools/api-surface.mjs` was byte-identical to `main`,
  and `knowledge/revit-api-surface.md` differed only by being the *older* generated snapshot (283
  fragments vs 285 on `main`), so nothing unique was lost. Two other fully-merged branches went with it
  — `claude/rag-folder-structure-4pui5e` and `claude/sprinkler-spacing-coverage-lp914z`, both zero
  unique commits. **`claude/rag-architecture-review-bqpjah` was kept**: it carries open **draft PR #22**
  ("Answer the 'rebuild the RAG properly' question with the measurements"), which is Ajmal's to merge or
  close. Remote is now `main` + that one branch.
- **Two new fragments need one live run each — nothing else is outstanding on them.** Added 2026-08-20
  after Ajmal asked for the useful capabilities from an outside tool evaluation to be kept "in our part".
  Both are written as this Brain's own, carry no outside reference, and are **289/289 compile-clean on
  Revit 2020, 2024 and 2027** — but compiling is a floor, see the rule at the bottom of this file.
  - [`action-test-view-filter-match.cs`](../scripts/actions/reporting/action-test-view-filter-match.cs)
    — dry-runs a View Filter against `elements` without applying it. **To verify:** point it at a filter
    you already know the answer for, with a deliberate mix — some elements it catches, and some from a
    category the filter was never scoped to. The third verdict (*N/A — category not in scope*) is the
    whole reason it exists; confirm those land as N/A and not as "no match".
  - [`action-manage-named-set.cs`](../scripts/actions/visibility/action-manage-named-set.cs) — name a
    set once, then select / isolate / hide / show it by name. **To verify:** `mode="list"` first (it is
    read-only), then `create` from a small selection, then `isolate`, then `show`. Also worth proving
    the staleness report: delete one element of a saved set and re-run — it should say so out loud
    rather than quietly acting on a shorter list.

- `knowledge/live-model/core.md` is at 303 lines, just past the ~300-line split rule, and has not been
  reviewed for splitting.

## The two rules that matter most here

**Ajmal is not a coder** — his own words, 2026-08-20: *"am not a coder or programmer i dont know the
programing side anything but i know how to work in revit"*. Every programming decision is the
assistant's. Ask him Revit questions; make the code decisions and report them.

**Compiling is a floor, not a ceiling.** A fragment can compile perfectly on every Revit version and
still act on the wrong elements. Step 1 proves nothing crashes. Step 2 is what proves it is right — one
element first, check the real result, then trust it on a batch.
