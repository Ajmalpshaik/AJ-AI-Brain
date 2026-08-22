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

The 17 native tools are the transport this Brain already runs on, documented at
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

## The tool list — status

`—` = not looked at yet. Panels are the ribbon's own grouping, which is how Ajmal names things.

### Tab: AJ Tools

| Panel | Tool | Source | Verdict |
|---|---|---|---|
| Quick | Quick Menu (radial wheel) | `src/QuickMenu/` | — |
| Quick | Customise | `src/QuickMenu/` | — |
| View | View Crop | `Commands/ViewCrop/`, `Services/ViewCrop/` | **UPGRADE** -> `action-set-view-crop.cs` rewritten |
| View | Unhide All | `CmdUnhideAll.cs`, `Services/UnhideAll/` | **KEEP OURS** — same algorithm, ours also rolls back |
| View | Toggle Links | `CmdToggleRevitLinks.cs` | **KEEP OURS** — `action-set-category-visibility.cs`, better guard |
| View | Filter Pro | `CmdFilterPro.cs`, `Services/FilterPro/` | **UPGRADE (knowledge)** -> [`graphic-override-precedence.md`](../knowledge/live-model/graphic-override-precedence.md) |
| View | Colorize | `CmdColorize.cs`, `Services/Colorize/` | **KEEP OURS** — `action-color-by-group.cs` does more |
| View | Highlight Selection | `GraphicsTools/CmdHighlightSelection.cs` | **UPGRADE** -> `action-highlight-vs-rest.cs` + insulation |
| Graphics | Apply Graphics | `GraphicsTools/`, `Services/GraphicsTools/` | **KEEP OURS** (workflow) + **UPGRADE** (the API sentinels) |
| Graphics | Match Graphics (element + category) | `GraphicsTools/CmdMatch*.cs` | **BUILD** -> `action-match-graphics.cs` |
| Graphics | Reset Graphics | `GraphicsTools/CmdReset*.cs` | **UPGRADE** -> `action-reset-category-graphics.cs` gains `allCategories` |
| Datums | Reset Grid/Level Extents to 3D | `CmdResetDatums.cs`, `Services/ResetDatums/` | — |
| Datums | Modify Level Extents | `CmdExtendLevelsBySelected.cs`, `CmdMaximizeLevelsBySectionBox.cs`, `Services/LevelExtents/` | — |
| Datums | Flip Grid/Level Bubbles | `CmdFlipGridBubble.cs` | — |
| Modify | Match MEP Element Elevation | `CmdMatchElevation.cs` | — |
| Modify | Reassign Reference Level | `CmdReassignLevel.cs`, `Services/ReassignLevel/` | — |
| Modify | Pin/Unpin Elements | `CmdPinElements.cs`, `Services/PinTools/` | — |
| Modify | Smart Selection | `CmdSmartSelection.cs` | — |
| MEP | Connect MEP Elements | `SmartConnectCommand.cs`, `Services/SmartConnect/` | **BUILD** -> [`knowledge/live-model/mep-connect-existing-runs.md`](../knowledge/live-model/mep-connect-existing-runs.md) |
| MEP | Elements to Ceiling Grid | `CmdCeilingMagnet.cs`, `Services/CeilingMagnet/` | **BUILD** -> [`knowledge/live-model/ceiling-grid.md`](../knowledge/live-model/ceiling-grid.md) + `action-snap-to-ceiling-grid.cs` |
| MEP | HVAC Schematic | `HvacSchematicCommand.cs`, `Services/HvacSchematic/` | **SKIP** — Ajmal, 2026-08-22: *"HVAC schematic is not good, that is not yet finish"*. Revisit only if he finishes it |
| MEP | Pipe Sizing | `CmdPipeSizing.cs`, `Services/PipeSizing/` | **BUILD** -> [`knowledge/plumbing-pipe-sizing.md`](../knowledge/plumbing-pipe-sizing.md) + `size-domestic-water-pipe.cs` |
| Opening | MEP Openings + settings | `CmdCreateMepOpenings.cs`, `Services/MepOpenings/` | — |
| Coordination | Element ID lookup (linked) | `CmdLinkedElementIdViewer.cs`, `CmdLinkedElementSearch.cs` | — |
| Coordination | 3D Views by Workset | `Cmd3DViewsAsPerWorkset.cs`, `Services/WorksetViews/` | — |
| Coordination | Link Workset | `CmdSetLinkWorkset.cs` | — |
| Data | Assign Location | `CmdLocationDataAssigner.cs` | — |
| Data | Duct Standard | `CmdDuctStandardsManager.cs`, `Services/DuctStandards/` | — |
| Manage | Transfer ×4 (templates, schedules, legends, drafting) | `CmdTransfer*.cs`, `Services/Transfer/` | — |
| Manage | Purge ×8 (unplaced views/sections/schedules/legends/drafting, unused templates/filters/groups, family params) | `CmdPurge*.cs`, `Services/Purge/` | — |
| Family | Shared to Family | `SharedParamToFamilyParamCommand.cs` | — |
| AI Assistant | AJ AI pane, bridge toggle, Run Pinned/Saved | `src/AiShell/` | — |
| Game | Game Mode | `src/GameMode/` | — |
| About | About | `AboutCommand.cs` | — |

### Tab: AJ Annotation

| Panel | Tool | Source | Verdict |
|---|---|---|---|
| Dimensions | Auto MEP Dimension | `Annotation/MepReferenceDimensionCommand.cs`, `Services/MepReferenceDimension/` | — |
| Dimensions | Automatic Dimension (+ grid, + level) | `CmdAutoDimensions.cs`, `Services/AutoDimension/`, `Services/Dimensioning/` | — |
| Dimensions | Quick Dimension | `CmdQuickParallelDimension.cs`, `Services/QuickDimension/` | — |
| Dimensions | Dimension by Line | `CmdDimensionByLine.cs`, `Services/DimensionByLine/` | — |
| Dimensions | Copy Dimension Text | `CmdCopyDimensionText.cs` | — |
| Annotation | Duct Flow Annotations | `CmdFlowDirectionAnnotations.cs`, `Services/FlowDirection/` | — |
| Annotation | Revision Clouds (+ by elements) | `CmdRevisionCloudByElements.cs`, `Services/RevisionCloud/` | — |
| Annotation | Copy / Swap Text Notes | `CmdCopyText.cs`, `CmdSwapText.cs` | — |
| Text | Arrange Text in Box | `CmdArrangeTextInBox.cs`, `Services/ArrangeTextInBox/` | — |
| Family | Center Annotation | `Services/RoomTags/` | — |
| Tags | Smart MEP Tags | `CmdSmartMepTag.cs`, `Services/SmartTag/` | — |
| Tags | Create Tags | `CmdCreateTags.cs`, `Services/CreateTags/` | — |
| Tags | Stack Tags | `CmdStackTags.cs`, `Services/TagArrange/` | — |
| Tags | Rearrange Tags / Fix Tag Clash | `CmdIntelligentTagArranger.cs`, `CmdFixTagClash.cs`, `Services/TagClash/`, `Services/LeaderLogic/` | — |
| Tags | L-Shape Leader | `CmdForceTagLeaderLShape.cs`, `Services/ForceTagLeaderLShape/` | — |
| Tags | Center Room Tags | `CmdCenterRoomTags.cs`, `Services/RoomTags/` | — |
| Tags | Section Mark Visibility | `CmdSectionMarkVisibility.cs`, `Services/SectionMarkVisibility/` | — |
| Tags | Reset Text Position | `CmdResetTextPosition.cs` | — |

### Not a ribbon tool, but holds harvestable knowledge

| What | Source | Why it matters | Verdict |
|---|---|---|---|
| Version-compat shims | `src/Helpers/RevitCompat.cs`, `TagCompat.cs`, `FilterRuleCompat.cs`, `CeilingGridApiCompat.cs` | Each one is a *recorded* API break between Revit versions, already solved. The Brain's [`knowledge/revit-version-compatibility.md`](../knowledge/revit-version-compatibility.md) may not have them all. | — |
| Transaction / undo discipline | `src/Helpers/TransactionHelper.cs` | How the add-in keeps undo clean across a multi-step change. | — |
| Selection filters | `src/Helpers/SelectionFilters.cs`, `SmartSelectionFilter.cs`, `SmartConnectSelectionFilter.cs` | What counts as a valid pick for each job. | — |
| Settled default values | the `*Settings.cs` commands and `Helpers/*Settings.cs` | Ajmal's own numbers, already argued out once. Highest-value, lowest-effort harvest. | — |
