# Harvesting AJ Tools into the Brain — the ledger

Started **2026-08-22**. Ajmal's instruction: *"from the Revit Addins folder we will take one by one
tools and check and we can convert to the skills or knowledge or whatever... you will check entire
tools how it's working how it's doing and you will take the idea and check from that what skill we can
make or what we can take to AJ Brain — and if that already we have better option, leave it, check
that."*

He names a tool. This file records what happened to it, so a fresh session never re-reads a tool that
was already judged. **This is a working ledger, not knowledge** — it lives in `docs/`, outside the
search index, same as `HANDOVER.md`.

## The method, per tool

1. **Read it end to end** — the `Cmd*.cs` command *and* its `src/Services/<Name>/` folder. The command
   file is usually just ribbon wiring and a dialog; the real algorithm is in the service. Reading only
   the command file means harvesting nothing.
2. **Search the Brain first**, both ways: `node tools/fragment-index.mjs --find <word>` for fragments,
   `semantic-index\ask-brain-hybrid.cmd "<the job in plain English>"` for skills and knowledge.
3. **Verdict — one of four**, never silently one or the other:

   | Verdict | Means |
   |---|---|
   | **BUILD** | Brain has nothing. Make the skill / knowledge note / fragment. |
   | **UPGRADE** | Brain has a weaker version. The add-in's is better → replace ours, say what improved. |
   | **KEEP OURS** | Brain already has as good or better. Say *why*, and move on. |
   | **SKIP** | Nothing transferable — ribbon wiring, WPF plumbing, a dialog with no logic behind it. |

4. **Do it in the same turn**, log it here and in [`knowledge/brain-log.md`](../knowledge/brain-log.md),
   and tell him what happened. Create-then-report, never ask-then-create.

**What "transferable" means here.** The add-in is compiled C# with a UI. The Brain drives a live Revit
through a bridge with no UI. So what transfers is never the button — it is:

- **the algorithm** (how tag clash is actually resolved, how a duct gets sized, how a ceiling grid is found),
- **the gotcha** (which API lies, which version broke what, what order the transaction has to be in),
- **the standard** (Ajmal's own settled values, already argued out once and living in a settings class).

## What is already done

### `Revit Addins/.claude/scripts` — CLOSED, 2026-08-22, nothing owed

All **77** C# fragments in the add-in repo's own script library are already in the Brain's 290. Checked
by basename with the `action-`/`filter-` prefixes stripped, because the Brain reorganised them into
sub-folders (`actions/reporting/`, `filters/by-property/`) and dropped the flat naming. The only two
that looked missing were renamed, not absent:

| Add-in | Brain | Status |
|---|---|---|
| `actions/action-length-by-size.cs` | `actions/reporting/action-report-length-by-size.cs` | PROVEN |
| `actions/action-material-takeoff.cs` | `actions/reporting/action-report-material-takeoff.cs` | PROVEN |

**Do not re-harvest this folder.** If a fragment there ever looks new, check the basename against the
Brain before believing it.

### `Revit Addins/mcp-server/tools` — CLOSED, already the Brain's own bridge

The 26 native tools are the transport this Brain already runs on, documented at
[`knowledge/mcp-ui-surface.md`](../knowledge/mcp-ui-surface.md). Nothing to harvest — it *is* the Brain's
hands.

### MEP panel — DONE, 2026-08-22 (3 of 4; HVAC Schematic skipped on Ajmal's call)

Three of the four turned out to be genuine gaps. Nothing here was already covered, and nothing the Brain
had was better — reported honestly rather than forced into a "keep ours".

| Tool | Verdict | What actually transferred |
|---|---|---|
| **Pipe Sizing** | BUILD | A whole domain the Brain had **zero** of: water supply fixture units -> Hunter's curve -> velocity sizing -> Hazen-Williams. All four tables kept (33 fixtures, 44 curve rows, 4 materials' real bores, C-factors). |
| **Elements to Ceiling Grid** | BUILD | Reading a ceiling's real tile size/angle off the type's **model** surface pattern, and the exact "is this element over this ceiling" test using `Face.Project` instead of a bounding box. The Brain's `action-move-to-ray-hit.cs` snaps in **Z**; this snaps in **plan** — no overlap, they pair up. |
| **Connect MEP Elements** | BUILD (knowledge only) | Stretch-don't-create, `canTrim` vs `mayMove`, one sub-transaction per attempt, the crank sign trap, pair scoring. **Deliberately no fragment** — the builder is ~2,200 lines and porting it speculatively would be worse than writing it the day a job needs it. |
| **HVAC Schematic** | SKIP | Unfinished in the add-in. |

**Two things the round produced that were not the point of it:**

- The pipe-sizing tool's **copper C-factor is 130** while its other three materials are all 150. Recorded
  as Ajmal's own working value, flagged in the knowledge note rather than silently "corrected" — C 130
  vs 150 is about **+30%** head loss for the same flow and bore, so it is worth a deliberate decision.
- Compile-checking the new fragments against all three installed Revits found a **pre-existing break**:
  [`filters/by-identity/filter-by-wrong-category.cs`](../scripts/filters/by-identity/filter-by-wrong-category.cs)
  used `ElementId.IntegerValue`, which **no longer exists on Revit 2027**. Fixed in place (compare
  `ElementId` to `ElementId`) and re-verified on 2020 and 2027. Nothing to do with the MEP panel — it
  just fell out of running the checker.

**Method note for the next round:** compile-check every harvested fragment on the **newest** installed
Revit, not just the oldest. The 2020 pass was clean and hid the 2027 failure completely.

### View panel — DONE, 2026-08-22 (all 6)

A much better test of the method than the MEP round: **3 keep, 3 upgrade, 0 build**. This is a
well-covered area of the Brain, and half of it genuinely had nothing to learn.

| Tool | Verdict | Why |
|---|---|---|
| **View Crop** | UPGRADE | Our fragment's own header admitted the gap ("not handled here, same known limitation"). The add-in closes it. |
| **Highlight Selection** | UPGRADE | Insulation and lining must follow their host, or an insulated duct highlights half-grey. |
| **Filter Pro** | UPGRADE (knowledge only) | Our five view-filter fragments are fine. What was missing was three silent-failure facts about filters. |
| **Unhide All** | KEEP OURS | Same full-model collector, same `IsHidden(view)`, same Temporary Hide/Isolate clear. Ours also rolls back on failure. |
| **Toggle Links** | KEEP OURS | `action-set-category-visibility.cs` guards with `get_AllowsVisibilityControl(view)`, which is a **better** check than the add-in's `CanCategoryBeHidden`, and works for any category rather than only Revit Links. |
| **Colorize** | KEEP OURS | `action-color-by-group.cs` is the same "one colour per parameter value, applied direct to elements" job with **five** colour modes, backed by `color-vocabulary.md` for turning "pastel"/"neon" into real RGB. |

**Why the two upgrades mattered — both were silent failures, which is the worst kind:**

- **View Crop** wrote a world-aligned box with no `Transform`, but `CropBox` Min/Max are read in the
  box's **own** transform. On any rotated plan, section or elevation the crop landed somewhere else and
  the fragment still reported success. It only looked correct on a plain north-up plan — which is
  exactly what its 2026-07-22 live test used, so the test passed and the defect survived. Also added:
  the four reasons a crop refuses, three of which **do not throw** (no crop box, a **scope box** owns
  the crop, a **view template** controls it).
- **Highlight Selection** — insulation is a separate element from the duct it wraps. Highlight the duct
  and the sleeve stays grey.

**Filter knowledge that was simply absent** (the Brain had zero mentions of any of it): filter **order**
inside a view decides which colour wins when two filters catch the same element; there is **no reorder
API**, so you capture every filter's overrides and visibility, remove them all, re-add in order and
restore — and skipping the capture resets every filter in the view; `View.GetFilters()` order is not
guaranteed on 2020; `GetFilters()` returns **new `ElementId` wrappers that are not reference-equal**, so
comparing by reference reports "not there" about a filter that is; and a **view template blocks every
filter change, silently**.

**Method note:** two of the three "keep ours" were only defensible after reading the add-in's guard
conditions and comparing them line for line against ours. Skimming would have produced three lazy
UPGRADEs and made the Brain worse.

### Graphics panel — DONE, 2026-08-22 (all 3)

One of each verdict, which is the most useful shape a round can have.

| Tool | Verdict | Why |
|---|---|---|
| **Match Graphics** | BUILD | The Brain could set a colour you name, and clear a colour — but had **no way to copy the look off something already right**. No equivalent fragment existed. |
| **Reset Graphics** | UPGRADE | Ours could only clear categories you name by hand. Useless for the case that needs it. |
| **Apply Graphics** | KEEP OURS + knowledge | Our many small fragments (`set-color-uniform`, `set-halftone`, `set-transparency`, `set-line-style`, `set-category-color`) compose better than one big dialog. But the add-in's override builder carried API facts we had never written down. |

**Why "Reset Graphics" needed upgrading.** The real request is *"I ran grayout over this view, put it
back"* — and nobody can be expected to name which of ~90 categories got touched.
`recipes/mep-grayout.cs` alone writes **87 categories and 589 sub-categories**. Naming them by hand was
never going to happen, so the fragment existed but the job it was for could not be done with it. It now
takes `allCategories`, with a dry run, and it names how many the view refuses.

**The API facts, all of which fail plausibly rather than loudly** — the Brain had **zero mentions** of
any of these:

- **"No override" is a specific sentinel, not zero and not blank**: `Color.InvalidColorValue` for a
  colour, `OverrideGraphicSettings.InvalidPenNumber` for a line weight, `ElementId.InvalidElementId` for
  a pattern. Writing `0` as a line weight does not clear the override — it asks for something invalid.
  Valid weights are **1-16**.
- **A pattern id and its visible flag are two separate writes.** Set the id, forget
  `SetSurfaceForegroundPatternVisible`, and the pattern is present but not drawn — which reads on screen
  as "the override didn't work".
- **`new OverrideGraphicSettings(existing)` is a copy constructor** — the correct way to duplicate an
  override. Re-setting properties one at a time makes every property you forget into "no override", so
  you get a partial copy that looks nearly right. This is why Match Graphics clones rather than reads.
- **`view.IsCategoryOverridable(id)` is the real test** for whether a category accepts an override in a
  view. `CategoryType` alone is not enough, and a refusing category throws.

**Skipped with a reason:** `GraphicsOverrideMemoryService` persists last-used dialog settings. Nothing to
harvest — the Brain has no UI, and "remember the last value" is the exact opposite of its standing rule
that every number is a per-request input.

**Note on file size:** `graphic-override-precedence.md` is now **324 lines**, past the ~300 split
candidate line. Measured and left alone deliberately: every section in it answers one question — *why
did my graphic change not do what I expected* — and `tagging.md` (332) and `families.md` (466) already
sit above it unsplit. Splitting would add a hop between facts that get read together.

### Datums panel — DONE, 2026-08-22 (all 3)

**All three BUILD, and this was the emptiest area found so far.**
`node tools/fragment-index.mjs --find datum` answered *"Nothing matched"*, and
`DatumExtentType`, `SetCurvesInView` and `DatumPlane` appeared **nowhere** in the whole Brain — not in a
fragment, not in a knowledge note. Grids and levels could be listed and created but their extents could
not be touched at all.

New: [`knowledge/live-model/datums.md`](../knowledge/live-model/datums.md), plus
`action-reset-datum-extents.cs`, `action-set-datum-bubbles.cs` and `recipes/maximize-level-extents.cs`.

**The subject is the 2D/3D trap**, which is what makes this area worth having at all. A datum carries
**two** extents: one shared **Model (3D)** extent, and a **per-view 2D override**. The little 3D/2D
toggle at the grid end picks which one you drag — and dragging on 2D creates an override that never
follows the model again. That is how a project ends up with grids that look right in one plan and stop
short in the next. "Reset to 3D" means setting both ends back to `DatumExtentType.Model`, **per end and
per view**; there is no reset-everywhere call.

Three more facts worth having:

- **A level is a line only in elevation, section and 3D views** — in a plan there is no extent to read
  or write. Grids are lines in plans too. `GetCurvesInView` returning **nothing is the normal answer**,
  not an error: it is how you ask whether a view draws the datum at all.
- **Set both ends to `Model` BEFORE writing a Model-extent curve**, or the write does not land where
  you meant.
- **"Flip the bubble" is not one call.** Revit only has Is/Show/Hide per end, so a flip is read-both,
  hide one, show the other — and the two ambiguous cases (neither visible, both visible) have to be
  decided out loud or the tool silently does nothing.

**The section-box recipe carries two pieces of real geometry.** The section box is **in its own
transform**, so all eight corners go through it before min/max — the same trap as the view crop box,
now hit twice in this harvest. And the new level line is built **along the datum's own direction**, by
projecting the box's four plan corners onto its unbound line and taking min/max parameter. An
axis-aligned line would be wrong on any rotated building, which most are.

**A checker bug surfaced and was fixed.** Adding three fragments produced **19** "fix this count" lines,
every one of them inside `graphify-out/` — derived, gitignored output regenerated wholesale from the
sources, several from a stale 2026-08-13 snapshot still quoting 269 fragments and 9 skills. Check 9 was
asking the author to hand-edit generated files, which the next rebuild would overwrite. `graphify-out`
is now skipped; the check still covers **22** live claims in real sources. Whether the derived layers
have fallen behind is a real question, but the STALE INDEX banner and `graph-rebuild.py --check` answer
it — this checker should not.

### Modify + Opening + Coordination panels — DONE, 2026-08-22 (all 9)

Three panels in one round: **5 BUILD, 1 UPGRADE, 2 KEEP OURS, 1 SKIP.**

| Tool | Verdict | Why |
|---|---|---|
| **MEP Openings** | BUILD | ~136 KB, the add-in's largest service, and the Brain had nothing on openings. |
| **Reassign Reference Level** | BUILD | The offset compensation — see below. |
| **Match MEP Elevation** | BUILD | Aligning by top/bottom, which `action-align-elements.cs` structurally cannot do. |
| **3D Views by Workset** | BUILD | Small, clear, and absent. |
| **Pin/Unpin** | UPGRADE | Ours existed and was proven, but counted a failed write as a success. |
| **Smart Selection** | KEEP OURS | The whole `filters/` folder — 50 fragments — is this job, composably. |
| **Link Workset** | KEEP OURS | `action-set-workset.cs` writes `ELEM_PARTITION_PARAM`, which works on a link instance like any element; `filter-by-links.cs` selects them. |
| **Element ID lookup (linked)** | SKIP | A mouse-pick that shows a dialog. The Brain has no pick, and already reads linked elements. |

**The two facts worth the whole round:**

- **Re-pointing an element's level MOVES IT unless you compensate the offset**, because its height is
  stored as an offset *from* that level: `newOffset = oldOffset + oldLevel.Elevation - newLevel.Elevation`.
  Change the level alone and four hundred ducts jump a storey — **silently**, because nothing throws and
  it is invisible in a plan view. There is also no single "level" parameter: MEP curves use
  `RBS_START_LEVEL_PARAM`, family instances `FAMILY_LEVEL_PARAM` or `INSTANCE_REFERENCE_LEVEL_PARAM`.
- **`Document.Create.NewOpening` has three completely different overloads** — wall takes two corner
  *points*, floor takes a profile plus `true`, beam takes a profile plus an `eRefFace` **whose correct
  value is not predictable**, so CenterY/CenterZ/CenterX are tried in turn. And a crossing is found by a
  real `BooleanOperationsType.Intersect` with non-zero volume, never by overlapping bounding boxes: a
  duct passing a wall in the next room shares a box corner with it.

**Why Match MEP Elevation was a BUILD and not a duplicate.** `action-align-elements.cs` aligns
*insertion points*. For MEP the insertion point is the **centreline**, so aligning a 600 mm duct and a
100 mm pipe by it leaves their soffits 250 mm apart — the opposite of what a coordinated corridor needs.
The new fragment reads each element's real vertical half-size from whichever of four parameters its kind
actually uses.

**Two mistakes made and corrected in this round, both worth recording:**

- **A duplicate fragment was written and then deleted.** `action-set-pin-state.cs` already existed at
  `actions/visibility/` and was PROVEN. It did not surface because the search output was **truncated at
  12 lines** and a conclusion drawn from the partial list — the tool had reported it correctly. The
  duplicate was removed and the existing proven fragment upgraded instead. **Read the whole result list,
  or grep it for the name; do not `head` a search you are about to draw a negative conclusion from.**
- **The new openings fragment failed on Revit 2027** on `ElementId.IntegerValue` — the exact trap
  written into the log on this same day. Caught by the newest-Revit compile check, which is now earning
  its place twice over.

### Data + Manage panels — DONE, 2026-08-22 (14 tools)

**4 BUILD, 1 UPGRADE, 2 KEEP OURS, 1 DEFERRED** (the twelve Manage buttons collapse into five real jobs).

| Tool | Verdict | Why |
|---|---|---|
| **Duct Standard** | BUILD | It is a **sheet-metal weight takeoff**, not duct sizing — gauge, sheet area, kg. The Brain had nothing; `--find weight` returned only *line* weight. |
| **Assign Location** | BUILD | Nothing in the Brain WRITES an element's containing room onto it — every room fragment was read/filter/create. |
| **Transfer ×4** | BUILD | `--find transfer` answered "Nothing matched". |
| **Purge unplaced views ×5** | BUILD | Ours purges unused *definitions*; these are real views nobody placed. |
| **Purge unused groups** | UPGRADE | Added as a fourth mode to `action-purge-unused.cs`. |
| **Purge unused templates, filters** | KEEP OURS | Already two of that fragment's modes, dry-run verified. |
| **Purge unused family parameters** | DEFERRED, said out loud | It needs opening and editing every family document in turn. That is a genuinely big job of its own and pretending otherwise would be worse than leaving it. **This is the first thing in the harvest deliberately not built.** |

**Three facts carry the round, and each one is a silent-wrong-answer trap:**

- **A schedule is NOT placed via a Viewport.** Sheets carry schedules as **`ScheduleSheetInstance`**, a
  different class entirely. Collect only `Viewport` and every schedule in the project reports as
  unplaced — so a "purge unplaced views" run would cheerfully offer to delete every schedule on every
  sheet.
- **The document-to-document `CopyElements` copies the view SHELL ONLY.** A legend or drafting view
  arrives with the right name and nothing drawn in it. Its contents are a *second* copy, of the elements
  owned by that view, into the new view. Miss it and the transfer reports success and delivers blanks.
- **An oval duct carries width, height AND diameter.** Testing "has a diameter" calls every oval duct
  round and then uses the wrong perimeter formula. Shape must be tested oval-first.

**The number worth knowing from the duct takeoff:** fabrication allowances add **+24%** over bare sheet
weight (seam 3, joint 2, flange 4, fittings 10, wastage 5) and **+29%** where the gauge band requires
reinforcement. Quoting bare sheet weight understates a job by roughly a quarter. These are **Ajmal's own
values** shipped as the tool's defaults — this office's standard, not a code citation.

**A structural limit of the fragment harness was found and recorded.** The transfer needs an
`IDuplicateTypeNamesHandler` to resolve a clashing type name, **and that handler has to be a class** —
which a fragment body cannot declare, because the bridge wraps every fragment inside a single method.
Proved on the compile checker. The fragment now copies each view in its own try/catch and names any that
fail, pointing at Revit's own Transfer Project Standards for those. **This is the first recorded job the
fragment harness structurally cannot do**, and it is worth knowing before someone spends an hour on it.

### Family + Dimensions panels — DONE, 2026-08-22 (7 tools)

Ajmal's brief for this round was to look carefully, work out what is *better*, and update as well as
create. So it deliberately went looking for what our own side had **wrong**, not only what was missing —
and found more there than in the add-in.

| Tool | Verdict | Why |
|---|---|---|
| **Auto MEP Dimension** | BUILD | The one real gap, and it closes a documented dead end. |
| **Shared to Family** | KEEP OURS + strengthen | **Our knowledge is better than their code** — see below. |
| **Automatic Dimension (grid/level)** | KEEP OURS | `create-dimension.cs`, PROVEN, does exactly this. |
| **Quick Dimension / Dimension by Line / Copy Dimension Text** | SKIP | All three are mouse-pick workflows. The Brain has no pick; the transferable geometry is in the knowledge note. |

**The dimension gap was one this Brain had written off.** `action-add-aligned-dimensions.cs` measured on
2026-08-14 that MEP fittings expose **zero** of all four `FamilyInstanceReferenceType` values, and
concluded *"this fragment can never dimension them"*. The measurement is right; the conclusion was too
broad. Ducts, pipes, conduit and trays dimension perfectly well by **walking the geometry** for a
`.Reference` — provided `Options.IncludeNonVisibleObjects = true`, because **a run's centreline is a
non-visible geometry object**. Leave that one flag out and a round pipe yields nothing and it looks like
the API cannot do it. That header has been corrected and now points at the new fragment.

**Shared to Family is the most interesting collision of the whole harvest.** The add-in hits the same
`FamilyManager.ReplaceParameter` name-clash wall this Brain hit on 2026-07-19, and solves it with a
temp-name detour. But this Brain **measured that this family of sequence corrupted a family document** —
garbage duplicate parameters, one parameter silently deleted, values changed that nothing had written to,
and three extrusions where one had been made — **from a transaction that never committed**. The add-in
carries no such warning. So: keep ours, and `families.md` now records what the add-in does differently
(replace-to-temp **then** rename, versus our rename-then-replace) and why the caution stays stricter for
a script than for a human with Revit's undo behind them.

**Three things on our own side were found wrong and fixed:**

1. **`fragment-index.mjs` was under-reporting proven fragments by ELEVEN.** Its status test was the
   literal `verified 2026`, so a README row reading *"verified **live** 2026-08-14"* did not match and
   the fragment reported as UNPROVEN. **`brain-status.mjs` had already found and fixed this exact bug in
   itself on 2026-08-14** — and nobody fixed the shared library, so the two tools disagreed and the one
   CLAUDE.md tells every session to search with was the wrong one. Measured: **231 -> 242**.
2. **`ElementId.IntegerValue` was written three times in one day** — by the same session that wrote the
   warning about it that morning. Root cause: the rule lived in `revit-version-compatibility.md`, which
   is **not what a fragment author opens**. It is now a section in `scripts/README.md` with a table of
   what to write instead.
3. The over-broad "can never dimension MEP" claim, above.

### AJ Annotation: Family + Annotation panels — DONE, 2026-08-22 (4 tools)

**Ajmal caught a miss.** The previous round covered **AJ Tools -> Family** (Shared to Family) and
recorded it as "the Family panel" — but there are **two** Family panels, and **AJ Annotation -> Family**
(Center Annotation) was untouched. The ledger row was honest about it (`—`), the summary was not. Both
are now done.

| Tool | Verdict | Why |
|---|---|---|
| **Center Annotation / Center Room Tags** | BUILD | `tagging.md` had no mention of centring at all. |
| **Revision Clouds by Elements** | BUILD | Ours draws a rectangle from typed corners; this one comes from the model. |
| **Duct Flow Annotations** | BUILD | Nothing in the Brain placed a directional annotation. |
| **Copy / Swap Text Notes** | SKIP | Pick-driven text copy between two notes. `action-find-replace-text-notes.cs` already covers scripted text editing. |

**The best single fact of the round — how to find "the centre of a room".** Four methods, in order, and
the third exists for a reason people discover the hard way:

1. **True area-weighted boundary centroid** from `GetBoundarySegments`, summed across every loop, so a
   room with a hole in it still lands correctly.
2. Bounding-box centre — only if the boundary cannot be read.
3. **An interior grid point** — because **on an L-shaped or U-shaped room the true centroid falls
   OUTSIDE the room**, in the corridor or the next tenancy. A script without this step "works" and
   quietly puts those tags in the wrong space.
4. The room's own `Location` point.

**And the best fact from the flow arrows:** the direction comes from the **connectors**, never from the
location curve. A duct's curve runs whichever way it happened to be drawn, so using it points half the
drawing backwards with nothing at all to warn you. The arrow runs from the `In` connector toward the
`Out` one, and a run with no usable connector direction is flagged loudly rather than silently guessed.

**One limitation stated rather than faked.** The add-in's revision cloud rasterises element footprints
into a grid, extracts connected components, traces each boundary loop and simplifies it — so its cloud
follows the real **stepped shape** of what changed. That is a subtle algorithm, and half-implementing it
would produce plausible-looking wrong outlines. Ours clusters by proximity and draws one rectangle per
cluster, which gets the main benefit (several clouds, following the groups), and the header says exactly
what it does not do.

### Tags panel — DONE, 2026-08-22 (8 tools) — and the prediction was WRONG

Every earlier round called Tags "the biggest uncovered area". **It was the most-covered one**, and the
reason is on the record: on **2026-07-14 Ajmal pointed this Brain straight at the add-in's own
`SmartTagPlacementEngine`** — *"you can refer our smart tag program... take from there if you need"* —
and it was read in full and adapted then. So the crown jewel of this panel was harvested six weeks
before this harvest started, and `tagging.md` documents it with **better measured outcomes than the
add-in itself carries**: 1092/1092 tags placed, 546/546 (100%) flow-direction match, 3.3% needing the
last-resort fallback, 0 own-leader clashes.

| Tool | Verdict | Why |
|---|---|---|
| **Smart MEP Tags** | KEEP OURS | Already harvested 2026-07-14 at Ajmal's direction and live-verified at 1092 tags. |
| **Create Tags** | KEEP OURS | `action-tag-elements.cs` (PROVEN) + `filter-by-tag-status.cs` (PROVEN). |
| **Rearrange Tags / Fix Tag Clash** | KEEP OURS | Four sections of `tagging.md` and PASS 2 of the recipe, live-verified — including the straight-vs-L-shaped move preference Ajmal himself asked for. |
| **Center Room Tags** | done | Covered in the AJ Annotation round earlier today. |
| **Stack Tags** | BUILD | **Zero** mentions of stacking anywhere in the Brain. |
| **L-Shape Leader** | BUILD | We knew to *prefer not moving* an L-shaped leader; we had nothing that *makes* one. |
| **Section Mark Visibility** | BUILD | Zero coverage. |
| **Reset Text Position / Clear Clash Marks** | SKIP | Pick-driven undo helpers for the interactive tools. |

**The best API fact of the round, and it is a trap:** **tag classes share no base exposing
`LeaderElbow`, `LeaderEnd`, `TagHeadPosition` or `LeaderEndCondition`.** `IndependentTag`, `RoomTag`,
`SpaceTag`, `AreaTag` each declare their own. A fragment that casts to `IndependentTag` **silently skips
every room and space tag in the selection** — no error, just a smaller number than expected. Both new
tag fragments read and write these **by reflection, by name**.

**Second fact:** setting `LeaderElbow` often fails until `LeaderEndCondition` is set to **`Free`** — and
the original condition must then be **restored**, or every tag ends up detached from its element.

**Third:** stack order must be **nearest-element-first**, not the order the filter returned. Any other
order produces crossed leaders that look like the tool is broken.

**A maintenance item raised, not silently actioned:** `tagging.md` is now **379 lines across 14
sections** — comfortably the largest knowledge file after `families.md`, and no longer one job (it
covers placement scoring, clash resolution, leader logic, room-tag centring and flow arrows). It is a
genuine split candidate. It was **not** split at the end of a long session, because a split has to be
cut mechanically at the seams and proved lossless against a backup, and doing that carelessly loses
content. Recommended as the next maintenance action.

### Quick Menu + Game Mode — DONE, 2026-08-22 — and GAME MODE was the surprise of the harvest

Ajmal asked whether there was anything worth taking from the two that look like they have nothing:
a radial tool wheel and a first-person game. The honest answer turned out to be **no from one and
something genuinely important from the other**, which is exactly why they were read rather than
dismissed.

**Quick Menu — SKIP, with one idea noted and not filed.** It is ribbon UI, and the Brain has no ribbon.
Its `QuickMenuAvailability` does carry a sound design point — build an expensive shared input (the
selected-categories set) **once for the whole menu, never once per slot**, because doing it per slot
meant walking a 50,000-element selection dozens of times — but that is generic programming sense, not
Revit knowledge, and this Brain does not need a file for it.

**Game Mode — it found a real defect in five of our fragments.** Its collision service raycasts against
the model, and its notes say plainly: *"architecture usually lives in a linked model"*, so it sets
`FindReferencesInRevitLinks = true`.

**None of this Brain's five ray-casting fragments did.** On a normal project the ceilings and slabs are
in a linked architectural model, so *"snap the terminals up to the ceiling"* would have found **nothing**
and reported *"no hit"* — which reads as a broken tool when the model is simply arranged the usual way.
The worst kind of failure, because it looks like an answer.

Fixed in the two primary ones (`action-report-ray-hits.cs`, `action-move-to-ray-hit.cs`), including the
part that is easy to get wrong: **a linked hit's `Reference.ElementId` is the `RevitLinkInstance`, not
what you hit** — the real element is `LinkedElementId`, fetched from the link's own document. Resolve it
lazily and the report names the RVT file instead of the ceiling.

**Three fragments still owe the same fix** — `action-check-surface-fit.cs`, `ray-trace-to-ceiling.cs`,
`sprinkler-deflector-height.cs`. Left deliberately: the linked path is compile-checked, not live-proven,
and it deserves one real test against a model with links before being pushed through five files. Tracked
in a table in [`knowledge/live-model/core.md`](../knowledge/live-model/core.md).

**Two more ray facts came with it**, both live-verified in the add-in on Revit 2020: `ReferenceIntersector`
**works on a PERSPECTIVE `View3D`** with the same results as orthographic at ~0.1 ms per hitting ray, so
there is no need to hunt for an orthographic view; and it only reports what is **visible in that view**,
which this Brain had already learned the hard way.

**The lesson worth keeping:** the two panels that looked like they had nothing produced one of the
session's most material findings. Reading them cost ten minutes. Dismissing them by name would have left
five fragments quietly broken on every project with a linked architectural model.

## The tool list — status

`—` = not looked at yet. Panels are the ribbon's own grouping, which is how Ajmal names things.

### Tab: AJ Tools

| Panel | Tool | Source | Verdict |
|---|---|---|---|
| Quick | Quick Menu (radial wheel) | `src/QuickMenu/` | **SKIP** — ribbon UI; the Brain has no ribbon. One perf idea noted, not worth a file |
| Quick | Customise | `src/QuickMenu/` | **SKIP** — stores which ribbon buttons sit on the wheel |
| View | View Crop | `Commands/ViewCrop/`, `Services/ViewCrop/` | **UPGRADE** -> `action-set-view-crop.cs` rewritten |
| View | Unhide All | `CmdUnhideAll.cs`, `Services/UnhideAll/` | **KEEP OURS** — same algorithm, ours also rolls back |
| View | Toggle Links | `CmdToggleRevitLinks.cs` | **KEEP OURS** — `action-set-category-visibility.cs`, better guard |
| View | Filter Pro | `CmdFilterPro.cs`, `Services/FilterPro/` | **UPGRADE (knowledge)** -> [`graphic-override-precedence.md`](../knowledge/live-model/graphic-override-precedence.md) |
| View | Colorize | `CmdColorize.cs`, `Services/Colorize/` | **KEEP OURS** — `action-color-by-group.cs` does more |
| View | Highlight Selection | `GraphicsTools/CmdHighlightSelection.cs` | **UPGRADE** -> `action-highlight-vs-rest.cs` + insulation |
| Graphics | Apply Graphics | `GraphicsTools/`, `Services/GraphicsTools/` | **KEEP OURS** (workflow) + **UPGRADE** (the API sentinels) |
| Graphics | Match Graphics (element + category) | `GraphicsTools/CmdMatch*.cs` | **BUILD** -> `action-match-graphics.cs` |
| Graphics | Reset Graphics | `GraphicsTools/CmdReset*.cs` | **UPGRADE** -> `action-reset-category-graphics.cs` gains `allCategories` |
| Datums | Reset Grid/Level Extents to 3D | `CmdResetDatums.cs`, `Services/ResetDatums/` | **BUILD** -> `action-reset-datum-extents.cs` |
| Datums | Modify Level Extents | `CmdExtendLevelsBySelected.cs`, `CmdMaximizeLevelsBySectionBox.cs`, `Services/LevelExtents/` | **BUILD** -> `recipes/maximize-level-extents.cs` |
| Datums | Flip Grid/Level Bubbles | `CmdFlipGridBubble.cs` | **BUILD** -> `action-set-datum-bubbles.cs` |
| Modify | Match MEP Element Elevation | `CmdMatchElevation.cs` | **BUILD** -> `action-align-mep-elevation.cs` |
| Modify | Reassign Reference Level | `CmdReassignLevel.cs`, `Services/ReassignLevel/` | **BUILD** -> `action-reassign-level.cs` |
| Modify | Pin/Unpin Elements | `CmdPinElements.cs`, `Services/PinTools/` | **UPGRADE** -> `action-set-pin-state.cs` gains dry run + read-back |
| Modify | Smart Selection | `CmdSmartSelection.cs` | **KEEP OURS** — the whole `filters/` folder (50) is this job |
| MEP | Connect MEP Elements | `SmartConnectCommand.cs`, `Services/SmartConnect/` | **BUILD** -> [`knowledge/live-model/mep-connect-existing-runs.md`](../knowledge/live-model/mep-connect-existing-runs.md) |
| MEP | Elements to Ceiling Grid | `CmdCeilingMagnet.cs`, `Services/CeilingMagnet/` | **BUILD** -> [`knowledge/live-model/ceiling-grid.md`](../knowledge/live-model/ceiling-grid.md) + `action-snap-to-ceiling-grid.cs` |
| MEP | HVAC Schematic | `HvacSchematicCommand.cs`, `Services/HvacSchematic/` | **SKIP** — Ajmal, 2026-08-22: *"HVAC schematic is not good, that is not yet finish"*. Revisit only if he finishes it |
| MEP | Pipe Sizing | `CmdPipeSizing.cs`, `Services/PipeSizing/` | **BUILD** -> [`knowledge/plumbing-pipe-sizing.md`](../knowledge/plumbing-pipe-sizing.md) + `size-domestic-water-pipe.cs` |
| Opening | MEP Openings + settings | `CmdCreateMepOpenings.cs`, `Services/MepOpenings/` | **BUILD** -> [`mep-openings.md`](../knowledge/live-model/mep-openings.md) + `create-mep-openings.cs` |
| Coordination | Element ID lookup (linked) | `CmdLinkedElementIdViewer.cs`, `CmdLinkedElementSearch.cs` | **SKIP** — a mouse-pick dialog; the Brain has no pick, and reads links already |
| Coordination | 3D Views by Workset | `Cmd3DViewsAsPerWorkset.cs`, `Services/WorksetViews/` | **BUILD** -> `create-workset-3d-views.cs` |
| Coordination | Link Workset | `CmdSetLinkWorkset.cs` | **KEEP OURS** — `action-set-workset.cs` + `filter-by-links.cs` already does it |
| Data | Assign Location | `CmdLocationDataAssigner.cs` | **BUILD** -> `action-assign-location-data.cs` |
| Data | Duct Standard | `CmdDuctStandardsManager.cs`, `Services/DuctStandards/` | **BUILD** -> [`duct-sheet-metal-takeoff.md`](../knowledge/duct-sheet-metal-takeoff.md) + `action-report-duct-weight.cs` |
| Manage | Transfer ×4 (templates, schedules, legends, drafting) | `CmdTransfer*.cs`, `Services/Transfer/` | **BUILD** -> `action-transfer-views-between-documents.cs` (one fragment, all four kinds) |
| Manage | Purge ×8 | `CmdPurge*.cs`, `Services/Purge/` | **BUILD** unplaced views (×5) -> `action-purge-unplaced-views.cs` · **UPGRADE** groups -> `action-purge-unused.cs` · **KEEP OURS** templates + filters · **DEFERRED** family parameters |
| Family | Shared to Family | `SharedParamToFamilyParamCommand.cs` | **KEEP OURS + strengthen** — our `families.md` warning is stronger than their code |
| AI Assistant | AJ AI pane, bridge toggle, Run Pinned/Saved | `src/AiShell/` | — |
| Game | Game Mode | `src/GameMode/` | **UPGRADE — and it was the surprise of the harvest.** Its collision service fixed a real defect in our ray fragments |
| About | About | `AboutCommand.cs` | — |

### Tab: AJ Annotation

| Panel | Tool | Source | Verdict |
|---|---|---|---|
| Dimensions | Auto MEP Dimension | `Annotation/MepReferenceDimensionCommand.cs`, `Services/MepReferenceDimension/` | **BUILD** -> [`dimensioning.md`](../knowledge/live-model/dimensioning.md) + `action-dimension-mep-runs.cs` |
| Dimensions | Automatic Dimension (+ grid, + level) | `CmdAutoDimensions.cs`, `Services/AutoDimension/`, `Services/Dimensioning/` | **KEEP OURS** — `create-dimension.cs` covers grids/levels, PROVEN |
| Dimensions | Quick Dimension | `CmdQuickParallelDimension.cs`, `Services/QuickDimension/` | **SKIP** — a mouse-pick workflow; its geometry is in the knowledge note |
| Dimensions | Dimension by Line | `CmdDimensionByLine.cs`, `Services/DimensionByLine/` | **SKIP** — pick a line, dimension what it crosses; needs a pick |
| Dimensions | Copy Dimension Text | `CmdCopyDimensionText.cs` | **SKIP** — pick-driven text copy between dimensions |
| Annotation | Duct Flow Annotations | `CmdFlowDirectionAnnotations.cs`, `Services/FlowDirection/` | **BUILD** -> `action-place-flow-arrows.cs` |
| Annotation | Revision Clouds (+ by elements) | `CmdRevisionCloudByElements.cs`, `Services/RevisionCloud/` | **BUILD** -> `action-revision-cloud-around-elements.cs` |
| Annotation | Copy / Swap Text Notes | `CmdCopyText.cs`, `CmdSwapText.cs` | **SKIP** — pick-driven; `action-find-replace-text-notes.cs` covers scripted text editing |
| Text | Arrange Text in Box | `CmdArrangeTextInBox.cs`, `Services/ArrangeTextInBox/` | — |
| Family | Center Annotation (AJ Annotation tab — the SECOND Family panel) | `Services/RoomTags/` | **BUILD** -> `action-center-room-tags.cs` |
| Tags | Smart MEP Tags | `CmdSmartMepTag.cs`, `Services/SmartTag/` | **KEEP OURS** — already harvested 2026-07-14 at Ajmal's direction, live-verified at 1092 tags |
| Tags | Create Tags | `CmdCreateTags.cs`, `Services/CreateTags/` | **KEEP OURS** — `action-tag-elements.cs` + `filter-by-tag-status.cs`, both PROVEN |
| Tags | Stack Tags | `CmdStackTags.cs`, `Services/TagArrange/` | **BUILD** -> `action-stack-tags.cs` |
| Tags | Rearrange Tags / Fix Tag Clash | `CmdIntelligentTagArranger.cs`, `CmdFixTagClash.cs`, `Services/TagClash/`, `Services/LeaderLogic/` | **KEEP OURS** — four sections of `tagging.md` + PASS 2 of the recipe, live-verified |
| Tags | L-Shape Leader | `CmdForceTagLeaderLShape.cs`, `Services/ForceTagLeaderLShape/` | **BUILD** -> `action-force-tag-leader-lshape.cs` |
| Tags | Center Room Tags | `CmdCenterRoomTags.cs`, `Services/RoomTags/` | **DONE** in the AJ Annotation round -> `action-center-room-tags.cs` |
| Tags | Section Mark Visibility | `CmdSectionMarkVisibility.cs`, `Services/SectionMarkVisibility/` | **BUILD** -> `action-set-section-mark-visibility.cs` |
| Tags | Reset Text Position | `CmdResetTextPosition.cs` | **SKIP** — pick-driven undo helper for the interactive tools |

### Not a ribbon tool, but holds harvestable knowledge

| What | Source | Why it matters | Verdict |
|---|---|---|---|
| Version-compat shims | `src/Helpers/RevitCompat.cs`, `TagCompat.cs`, `FilterRuleCompat.cs`, `CeilingGridApiCompat.cs` | Each one is a *recorded* API break, already solved. | **DONE 2026-08-22 — and `TagCompat` caught a defect in fragments written the same day.** `IndependentTag` lost `LeaderElbow`/`LeaderEnd`/`TaggedLocalElementId` in Revit 2023; reflection hid it from every compile check. Both tag fragments now bridge at runtime. `FilterRuleCompat`: already handled. Other two: nothing owed |
| Transaction / undo discipline | `src/Helpers/TransactionHelper.cs` | How the add-in keeps undo clean. | **KEEP OURS 2026-08-22 — measured, not assumed.** Its `HasStarted() && !HasEnded()` guard exists because it starts the transaction INSIDE the try. Ours starts it OUTSIDE, so a failing `Start()` propagates its real message and the catch never runs — safe by construction. **184 fragments start outside the try, 0 inside.** Recorded in `scripts/README.md` |
| Selection filters | `src/Helpers/SelectionFilters.cs`, `SmartSelectionFilter.cs`, `SmartConnectSelectionFilter.cs` | What counts as a valid pick. | **SKIP as code, BUILD as knowledge** — they are `ISelectionFilter` classes for mouse picking and the Brain has no pick, but the CATEGORY TAXONOMY inside them is the real answer to "what is connectable". Added to [`mep-connect-existing-runs.md`](../knowledge/live-model/mep-connect-existing-runs.md) |
| Settled default values | the `*Settings.cs` commands and `Helpers/*Settings.cs` | Ajmal's own numbers, already argued out once. | **DONE 2026-08-22** -> [`ajtools-settled-values.md`](../knowledge/ajtools-settled-values.md) — framed as material to ASK with, never to apply silently. Surfaced two disagreements with our own fragments |
