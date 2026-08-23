# Harvesting an LLM-code-generation add-in into the Brain — the ledger

**2026-08-23.** Ajmal supplied a repository and asked for all of it to be gone through, same method as the
two pyRevit harvests earlier the same day ([`pyrevit-harvest.md`](pyrevit-harvest.md),
[`pyrevit-platform-harvest.md`](pyrevit-platform-harvest.md)), and the same four-way verdict:
**BUILD** / **UPGRADE** / **KEEP OURS** / **SKIP**.

Working ledger, not knowledge — it lives in `docs/`, outside the search index. Its job is so a fresh
session never re-reads something already judged. Per the standing rule nothing here names the source;
everything kept was rewritten as this Brain's own C#.

## This one is a different kind of thing, and that changes the harvest

The first two harvests were **tool libraries** — people had written tools, and the job was deciding which
to carry across. This is not that. It is a **peer system**: a Revit add-in that takes a plain-English
question, retrieves Revit API documentation for it, asks an LLM to write IronPython, runs that script
inside a transaction it opened itself, and on error feeds the error back for up to five more attempts.
It is, structurally, the same idea as this Brain's own bridge.

So it has two harvestable halves, and they are judged on different grounds:

| Part | Size | What it is |
|---|---|---|
| `GeneratedSuccessfulCode/` | **584 scripts, 67,848 lines** | Machine-written IronPython kept after it ran. **The closest thing to a tool library here** — and the reason the verdicts below are so weighted towards SKIP |
| The add-in shell + retrieval script | 5 C# files (1,368 lines), 1 Python file (404 lines) | The architecture. Judged as a design to learn from, not as code to take |
| `protos/`, `packages/` | 174 DLLs, 112 protos | Vendored dependencies. Nothing authored, nothing to harvest |

## The headline finding, and it is about trust

**"Successful" here means "IronPython raised no exception". It does not mean the script did the right
thing, and it does not even mean the script could run.** Three independent proofs, in increasing order
of how much they matter:

1. **The add-in's own code says so.** The execution helper runs the script, and if `Execute` returns
   without throwing it logs *"No Exceptions Thrown by IronPython Engine"* and returns true. True commits
   the transaction. There is no check on what the script actually did.

2. **Six of the 584 contain no executable code at all** — a single comment line stating the task is
   impossible ("the API does not expose a filterable Last Modified Date", "elements in a linked document
   are read-only", and so on). A comment executes without error, so it lands in the success folder. One
   of the six is not even an API limit: it is a DLL that failed to load, filed as though it were.

3. **Twenty-six of them call Revit API members that do not exist.** Not deprecated — never existed, on
   any Revit installed on this PC. Checked against the shipped `RevitAPI.dll` by reflection on 2020 and
   2024, and by compiling on 2027:

   | Called by the corpus | Files | What is actually there |
   |---|---|---|
   | `ParameterFilterElement.IsNameUnique` | 13 | nothing — the statics are `Create`, `AllRuleParametersApplicable`, `ElementFilterIsAcceptableForParameterFilterElement` |
   | `View.IsFilterApplicable` | 5 | nothing |
   | `View.CanApplyFilter` | 5 | nothing |
   | `View.CanApplyFilterOverrides` | 3 | nothing |
   | `RevitLinkGraphicsSettings.SetHalftone` / `.SetTransparent` / `.SetCategoryOverrides` | 1 | the class carries **only** `LinkVisibilityType` and `LinkedViewId` |
   | `RevitLinkGraphicsType` | 1 | the enum is `LinkVisibility` |
   | `ParameterFilterUtilities.IsParameterApplicableToCategory` | 1 | the real name is `IsParameterApplicable` |

   Every one of those scripts would raise on the line that names the missing member. They are in the
   folder labelled successful.

**Why this is worth writing down rather than just noting:** this is the clearest evidence this Brain has
ever had for its own compile-check rule. `tools\check-scripts.cmd` exists because Ajmal said scripts
break on a newer Revit. What this repository shows is the harder version of the same problem — an
LLM-written Revit script is *most* likely to be wrong in exactly the way a compiler catches instantly and
a human never does. The names above are all plausible. `IsNameUnique` is what the method *should* be
called. Reading the code would not catch it; running it costs a round trip through Revit; compiling it
costs a minute.

**One thing this does NOT prove:** that the retrieval was at fault. A weaker generator, or a smaller
context, produces the same failure. It is a data point, not a verdict.

## Method

**Pass 1 — survey.** All 584 measured mechanically: size, every Revit API call, every `BuiltInCategory`.
Median 106 lines; 74 under 60 lines, 468 between 60 and 200, 42 over 200. Categories are overwhelmingly
architectural — Walls 66, Rooms 56, Floors 36, Doors 35, Windows 29 — and the API surface is dominated
by view filters and graphic overrides (`OverrideGraphicSettings` 144, `AddFilter` 71, `SetFilterOverrides`
62). That shape alone said most of this would be KEEP OURS: `color-graphics/` is one of the best-covered
folders here.

**Pass 2 — full read**, because a survey shows what a script *calls* and never what it had to *learn*.
Read in size bands with imports and error-handling scaffolding stripped, 28,493 lines of real content.

**Pass 3 — a completeness proof, which the earlier harvests did not have.** Every API token in all 584
files was extracted and set-differenced against every `.cs` file in `scripts/`. **1,049 distinct tokens in
the corpus; 400 of them appear nowhere in this Brain.** That list is what the BUILD section below is
drawn from, so "did we miss anything" has a mechanical answer rather than a judgement. It is also how the
invented-API finding surfaced: several of the 400 turned out to be absent from Revit as well.

## BUILD — 6 new fragments

All six are gaps the token diff proved, not guesses. **All compile on Revit 2020, 2024 and 2027. None has
been run against a real model** — every one is read-only or dry-run by default and says so in its header.

| Built | The gap it fills |
|---|---|
| [`action-set-link-overrides.cs`](../scripts/actions/color-graphics/action-set-link-overrides.cs) | **Grey a LINKED model in a view.** The most valuable thing in this harvest, and it is a hole in his own standard: `recipes/mep-grayout.cs` sets CATEGORY overrides on the host document, and the word "link" appears nowhere in it or in the grayout skill. On a coordination job the architecture *is* a link, so the recipe greys nothing, reports success, and the view looks untouched. Two ways in, because they behave differently: one override on the link INSTANCE (one API call per link, works on every Revit here), or reaching the elements inside it per category (needs the view+link collector, which Revit 2020 does not have — the fragment says so instead of failing) |
| [`action-report-curtain-elements.cs`](../scripts/actions/reporting/action-report-curtain-elements.cs) | **Curtain walls, which this library had never touched** — `CurtainGrid`, `Mullion` and `Panel` appeared in no fragment at all. Four things make a naive version wrong, and they are all in the header: a "panel" can be a **Wall** used as infill (cast to `Panel` and you drop every spandrel); `GetPanelIds` and `GetUnlockedPanelIds` answer different questions (all vs. the ones Revit will let you change — the difference IS "why won't this panel swap"); a CurtainSystem carries `CurtainGrids` **plural**, one per face, so reading `.CurtainGrid` off walls finds no atrium; and mullion length is the instance's `LocationCurve`, not the type |
| [`action-audit-view-filters.cs`](../scripts/actions/qa-checks/action-audit-view-filters.cs) | **"The filter is on the view, so why is nothing coloured?"** Four states must all be right and Revit reports none of them as an error. The one that costs an afternoon: **applied but DISABLED** — still listed, greyed, doing nothing. `SetIsFilterEnabled` appeared in no fragment here. Also catches the EMPTY override, which is what `AddFilter` leaves on its own |
| [`action-report-tags-and-targets.cs`](../scripts/actions/reporting/action-report-tags-and-targets.cs) | **Tags pointing INTO a link.** `GetTaggedLocalElementIds()` returns only host-document targets, so on an MEP job — where the architecture is a link — it returns an empty set and reports nothing wrong. `GetTaggedElementIds()` is the other half, returning `LinkElementId` (which link, which element inside it). Also separates the two ways a tag can point at nothing: target deleted, and link unloaded |
| [`action-set-view-underlay.cs`](../scripts/actions/visibility/action-set-view-underlay.cs) | **The ghosted level behind the one you are working on** — the MEP "show me the floor below while I route" habit, across many views at once. The trap is that the underlay is three settings that must agree: writing the base level alone leaves a stale top level and the view shows a multi-storey ghost that reads as corruption rather than a wrong setting. `SetUnderlayRange` sets base and top together, which is why it is used instead of `SetUnderlayBaseLevel` |
| [`action-report-areas.cs`](../scripts/actions/reporting/action-report-areas.cs) | **Areas — the third spatial kind**, after Rooms and Spaces. `OST_Areas` appeared in no fragment. The rule that makes a naive count meaningless: **schemes overlap on purpose.** The same floor is measured under "Gross" and again under "Rentable", both sets live in `OST_Areas` at once, and summing the category gives roughly double the building. Every number is per scheme and there is deliberately no grand total |

## UPGRADE — 2, both replacing a hand-written rule with Revit's own answer

**[`action-place-views-on-new-sheets.cs`](../scripts/actions/sheets-views/action-place-views-on-new-sheets.cs)**
decided "can this view go on more than one sheet" from a hand-maintained list of seven view types. That
list was already corrected once, in the platform harvest earlier today, after the first harvest wrote it
as a guess. **Revit answers it directly from 2022 with `View.GetPlacementOnSheetStatus()`** — no sheet
needed, nothing to maintain. Now used first where it exists, with the list kept as the 2020/2021 fallback
and the status reported in Revit's own words. Reached by reflection: the method is absent on 2020 and
naming it would stop the fragment compiling there.

**[`action-set-view-crop-to-shape.cs`](../scripts/actions/visibility/action-set-view-crop-to-shape.cs)**
told you the loop was "valid and closed" on the strength of its own geometry checks, then found out
whether Revit agreed by trying to write it. **`IsCropRegionShapeValid` exists on every Revit here** and
gives Revit's verdict before the write. The dry run now asks it. A loop can chain and close perfectly and
still be refused — self-intersecting, not planar, not in the view's plane.

## What the harvest found in OUR code — the more valuable half, again

- **The grayout has never handled linked models.** `recipes/mep-grayout.cs` is 200+ lines of his own
  measured standard and contains no mention of a link; nor does the skill. It is not wrong, it is
  incomplete in the case that matters most on a real job. Now covered by the new fragment, and the
  recipe cross-references it.
- **`OverrideGraphicSettings` background patterns are used nowhere in the library** — every colour
  fragment sets the FOREGROUND pattern only. For his line-only default that is correct and deliberate
  (his own rule), so nothing was changed. Recorded because it is a real half of the API that is unused,
  and the day a solid fill has to sit over a material's own hatch, that is the reason it does not cover.
- **`SetIsFilterEnabled` appeared in no fragment**, so the library could apply a filter and never notice
  one was switched off. The silent-no-op class this Brain keeps finding.
- **Confirmed correct rather than assumed, all three checked against the code**: paint materials are
  already handled properly in `action-report-material-takeoff.cs` (both `GetMaterialIds` flags, and the
  note that paint has area but never volume); per-view datum extents are already handled
  (`SetDatumExtentType` / `GetCurvesInView` / `SetCurveInView`); and the room-point lookup in
  `action-assign-location-data.cs` is arguably better than `doc.GetRoomAtPoint` because it probes at the
  ROOM'S mid-height, so a ceiling-mounted element still resolves. `GetRoomAtPoint` is faster on big
  batches and is noted for the day that matters — it would not fix the height problem.

## KEEP OURS — ours is as good or better

| What the corpus does | Ours | Why ours stays |
|---|---|---|
| ~180 scripts creating/applying view filters and graphic overrides | the whole `actions/color-graphics/` folder | Not close. Ours are parameterised, dry-run, version-proof and read back what actually stuck; each of theirs hard-codes one filter name, one colour and one threshold for one job |
| Rename / prefix / suffix views, sheets, levels, families, types (43 scripts) | `action-find-replace-element-name.cs`, `action-rename-element.cs`, `action-renumber-sequential.cs` | Three general fragments against 43 single-purpose ones |
| Report/export to CSV (51 "extracts…", 18 "exports…") | `action-export-parameters-to-csv.cs`, `action-export-schedule-to-csv.cs`, `action-report-material-takeoff.cs` | Same job, and ours respect the Windows list separator |
| Set parameters from an input string / by type name (68 "updates…") | `action-set-parameter-value.cs`, `action-import-parameters-from-csv.cs` | Ours takes a file rather than a string pasted into the script body |
| Hide / isolate / select by rule (35 scripts) | `filters/` + the native `hide_elements` / `isolate_elements` / `select_elements` tools | One filter plus one tool call |
| Place views on sheets, align viewports, duplicate views | `action-place-views-on-new-sheets.cs`, `action-align-viewports-across-sheets.cs`, `action-duplicate-views.cs` | Same job; ours plan before they write |
| View range, view templates, scope box assignment | `action-set-view-range.cs`, `action-set-view-properties.cs`, `action-assign-scope-box-to-view.cs` | Same job |
| Room/space reporting and totals | `action-count-by-spatial-container.cs`, `action-report-space-airflow.cs` | Broader, and MEP-aware |
| Worksets, design options, phases | `action-set-workset.cs`, `filter-by-design-option.cs`, `action-set-element-phase.cs` | Same job |
| Scope box → reference plane alignment (2 scripts) | `action-assign-scope-box-to-view.cs` + `action-move-elements.cs` | A scope box **can** be moved (`ElementTransformUtils.MoveElement`) though `action-update-scope-box.cs` correctly records that it cannot be RESIZED. Aligning one to a plane is two existing fragments and a job nobody has asked for |
| Pie chart drawn as filled regions inside the view | `skills/ajtools-visual-report/SKILL.md` | Charts belong in the chat reply (his rule 8), not built out of `FilledRegion` geometry in the model |

## SKIP — nothing transferable

- **The add-in shell** — `App.cs`, `PromptForm.cs`/`.Designer.cs`, ribbon wiring, the WinForms prompt.
  [`START-HERE.md`](../START-HERE.md) rules out working on the add-in that provides a bridge; this is
  that, for a different bridge.
- **The Gemini transport** — endpoint, API key from an environment variable, safety settings, JSON
  parsing, the fenced-code extractor, the 5-attempt fix-it loop. All of it is one specific model behind
  one specific HTTP call, and this Brain reaches its model a different way.
- **The IronPython host** — engine setup, assembly loading, stdout capture. The bridge runs C#, decided
  before either pyRevit harvest and for the same three reasons: `run_csharp` takes C# only,
  `check-scripts.cmd` cannot compile-check Python, and the index only collects `.cs`.
- **`protos/` and `packages/`** — vendored gRPC and NuGet. Nothing authored.
- **All 584 as artefacts.** Not one is transferable without a rewrite: every one is Python, one-shot, with
  its inputs hard-coded into the body (a level called "L1 - Block 35", a filter called "Linked Arch
  Podium Elements - Halftone"). What was worth having from them is the six BUILDs above, which is what a
  full read is for.

## The one idea worth taking, recorded rather than built

**The retrieval script does not search the user's words. It rewrites them first.** An LLM turns the
plain-English question into about five technical Revit-API queries — class names, method names,
`BuiltInParameter` names — searches all five, merges the results keeping the best distance per document,
and hands the top 15 to the generator. The original question, unmodified, is what the generator finally
sees; the rewriting exists purely to aim the search.

**That is a direct answer to this Brain's one measured weakness.** [`CLAUDE.md`](../CLAUDE.md) records it
plainly: the search "fails on site vocabulary the files don't use" — *"add 4 more floor levels"* returns
`create-floor.cs`, the slab creator, instead of `create-levels.cs`. The fix in place today is
[`knowledge/glossary.md`](../knowledge/glossary.md) plus a human noticing and re-running with the Revit
word. Query expansion would do that step automatically, and this Brain already has the site-word →
Revit-word map to expand from.

**Not built, and the reason holds.** It belongs to `semantic-index/`, not to `scripts/`, and the whole
point of that layer is that its accuracy is *measured* — every change is a line in
[`semantic-index/score-history.md`](../semantic-index/score-history.md) against the 28-row set. Adding
query expansion without a before-and-after on that set would be exactly the unmeasured change the score
history exists to prevent. It is the next improvement to that layer, and it needs its own session with
the eval in front of it.

## What they indexed, and what it says about our decision not to

Their knowledge base is the **entire Revit 2025 API documentation**, chunked out of the SDK `.chm` into a
vector store. That is precisely what [`START-HERE.md`](../START-HERE.md) rules out, on the grounds that
~1,700 classes and 30,000+ members would leave this Brain as roughly 11% of its own index and every
question would land on a reference page.

This repository is a worked example of what that produces, and the outcome is at least consistent with
the decision: a system with the whole API indexed still generated twenty-six scripts calling members that
do not exist. **It does not prove the indexing caused it** — the generator and the prompt matter more —
but it is evidence against the assumption that indexing the reference is what makes the difference. What
replaces it here — [`knowledge/revit-api-surface.md`](../knowledge/revit-api-surface.md), 245 types each
tied to a working fragment — has the property the reference lacks: every entry has been compiled.

## Small facts worth keeping, found only in the full read or in checking it

Each is a one-line trap that costs a wasted round trip if met cold. None justifies its own fragment.

- **`OverrideGraphicSettings` setter and getter names do not match.** You write with
  `SetSurfaceTransparency(int)` and read with the plain `Transparency` property. Reaching for the
  symmetrical name is a compile error, not a null. Cost one compile round here.
- **Revit 2020 is missing more than expected**, all confirmed against the shipped DLL:
  `GetIsFilterEnabled`/`SetIsFilterEnabled`, the three-argument `FilteredElementCollector`
  (doc + view + link instance), `RevitLinkGraphicsSettings` and `View.SetLinkOverrides`,
  `View.GetPlacementOnSheetStatus`, and multi-reference tags. Each is a reflection lookup, not a
  try/catch.
- **`BuiltInParameter.ROOF_SLOPE` is a RATIO, not an angle.** It stores the tangent, so comparing it to
  a value in degrees silently compares the wrong quantity — one of the few places the corpus was right
  and explicit about it.
- **A curtain "panel" can be a `Wall`.** Revit allows a wall type as infill and returns it from
  `GetPanelIds()`.
- **`GetUnlockedPanelIds` / `GetUnlockedMullionIds` are the changeable subset**, not a second list. The
  difference between them and the full list is the set the grid has pinned.
- **`CurtainSystem` has `CurtainGrids` — plural**, one per face. A wall has `CurtainGrid`, singular.
- **`AreaScheme` membership lives on the area's own `AREA_SCHEME_ID` parameter**, not on its view — an
  area survives the deletion of the view it was drawn in.
- **`LinkElementId` carries two ids**, `LinkInstanceId` and `LinkedElementId`, and an invalid link half
  is how you tell a host target from a linked one.
- **Loading two Revit versions' `RevitAPI.dll` in one .NET process gives you the first one twice** —
  same assembly identity, so `LoadFrom` returns the cached one and every answer after the first is
  silently wrong. Introspecting more than one version needs a separate process each. Caught here because
  a version comparison came back claiming a type was absent from a Revit that has it.
- **Revit 2027's `RevitAPI.dll` will not load in Windows PowerShell 5.1** at all ("incorrect format") —
  it is a different runtime. Compile-checking against it works; reflecting on it from PS 5.1 does not.

## State at the end

All 13 consistency checks pass. **The 6 new fragments and the 2 upgrades compile on Revit 2020, 2024 and
2027** — checked with `tools\check-scripts.cmd`. **None of the 6 has been run against a real model**;
each is read-only or dry-run by default and says so in its own header. Run each on ONE view or ONE wall
first and check the real result before a batch.
