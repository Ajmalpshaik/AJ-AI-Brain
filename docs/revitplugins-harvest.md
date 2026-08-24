# Harvesting a 72-plugin Revit suite — the ledger

**2026-08-24, second session.** The largest target so far: **2,724 C# files, 183,262 lines, 72 plugins,
480 XAML, MIT.** Bigger than the pyRevit platform and this library combined. The first session did the
survey and the API diff and stopped there, recording itself as unfinished — correctly, because *"a
survey shows what code CALLS and never what it LEARNED"* and 183k lines cannot get the read the method
demands alongside anything else.

Working ledger, not knowledge — it lives in `docs/`, outside the search index. Per the standing rule
nothing here names a source.

## What the first session left, and what this one did with it

| Left by the first session | What happened |
|---|---|
| Survey + API diff complete, **510** Revit API names used there and absent here | Rebuilt from scratch — the fragment count had moved, and the method got better (below). **1,014** names this time |
| 1 fragment built (`action-report-model-file-info.cs`) | Untouched |
| `Category.GetBuiltInCategory` verified 2024+ only; `Solid.IntersectWithCurve` decided against | Both re-confirmed and left alone. The `IntersectWithCurve` decision is revisited below — the *ray-cast* use is a different capability from the clash use it was compared against |
| Five biggest plugins unread; `Level.ProjectElevation`, `SuperComponent`, `NamingUtils`, `ParameterFilterUtilities`, `UnitFormatUtils`, `WorksetConfiguration`, `Outline`/`Transform` all confirmed present and absent | This is the session that read them. Every one of those names is now either built, upgraded into an existing fragment, or explicitly declined with a reason |

## The API diff, rebuilt — and the tooling now works in a Linux container

The first session reflected `RevitAPI.dll` in PowerShell on the Windows PC and hit the documented trap:
*"Reflecting on two Revit versions in ONE PowerShell process silently gives you the first one twice."*
This session had no Windows, no Revit and no .NET — and ended up with a **better** instrument:

- The three shipped `RevitAPI.dll` / `RevitAPIUI.dll` pairs (2020, 2024, 2027) were pulled from the
  public package feed and their **CLI metadata tables parsed directly in Python** — TypeDef, MethodDef,
  Field and Property rows, filtered to `public`/`protected` types under `Autodesk.Revit`. No .NET
  runtime involved, three versions in one pass, and **no shared-process caching to be fooled by.**
- **26,114 / 36,105 / 38,388** public names for 2020 / 2024 / 2027, emitted both bare and
  **type-qualified** (`ElementId.IntegerValue`), plus **arity** per method so overloads separate.
- Sanity-checked against everything the Brain already knows: `GetBuiltInCategory` absent on 2020,
  `GetChangedElements` absent on 2020, `VersionGuid` absent on 2020, `GetAllUnusedElements` absent on
  2020, `ElementId.IntegerValue` present on 2020/2024 and **gone on 2027**. All five agreed.
- The packages also ship **`RevitAPI.xml` for 2024 and 2027** — Autodesk's own documentation. That
  turned several judgement calls into quotations; two of the findings below rest on it.

The diff itself: every token in the target repo, intersected with the real Revit API names, minus every
token appearing in any of our fragments. **19,754 tokens absent from ours → 1,014 that are genuinely
Revit API.** As expected for a suite with its own internal library, the top of the ranked list is their
own helper vocabulary colliding with API parameter names (`GetParamValue`, `OpeningType`, `Params`) and
WPF (`IsChecked`, `ShowDialog`); the signal is further down.

## What was read, and what was not — stated plainly

| | Files | Lines | Revit-bearing lines |
|---|---:|---:|---:|
| Whole repo | 2,698 | 184,487 | 126,090 |
| **Zero `Autodesk.Revit` reference anywhere** — skipped on evidence, not assumption | 1,263 | — | **58,397** |
| Plugins opened and read | 1,173 | 86,688 | 61,619 |
| Plugins not opened | 1,525 | 97,799 | 64,471 |

**Every Revit-bearing file in all 72 plugins went through a mechanical per-token inventory** — for each
plugin, every Revit API name it uses that appears in none of our fragments, ranked by frequency, with a
file and line number. The close reading (~4,500 lines) went to the MEP plugins in full and to the top
hits of that inventory everywhere else. That is how `ParameterFilterUtilities` was found in five
different plugins, `GetModelUpdatesStatus` in one, and `SuperComponent` in four.

**The 18 plugins opened**: openings placement, sleeves, both clash tools, mechanical specification,
split MEP curve, MEP totals, unmodelling MEP, opening slopes, server folders, checking levels, set level
section, schedule import, batch print, set coord params, mirrored elements, creating filters by values,
pylon documentation.

**The largest not opened**, each still inventoried: lintel placement (4,602 Revit lines), package
documentation (3,885), rooms (3,399), coordination volumes (2,953), area boundaries (2,922),
declarations (2,858), architectural documentation (2,416), create view sheet (2,383). They are
architectural and structural documentation tools; their inventories surfaced no API name that is both
absent here and useful, and Ajmal is MEP. **That is a defensible skip and not a proven-empty one** — if
a future job needs sheet-set automation or area boundary repair, they are the place to look.

## BUILD — 6 new fragments

All compile on Revit 2020, 2024 and 2027. **None has been run against a real model.** All read-only.

| Built | The gap it fills |
|---|---|
| [`action-audit-mep-openings.cs`](../scripts/actions/qa-checks/action-audit-mep-openings.cs) | **The revision question, and nothing here could answer it.** `create-mep-openings.cs` cuts openings; `action-report-clashes.cs` finds services hitting structure. Neither can tell you an *existing* opening has gone wrong — and that is the real failure: the hole was right in revision B, the pipe moved 200 mm in revision C, and the clash report is still clean *because the pipe now goes through the hole and past its edge into the concrete*. Statuses: STALE / UNDERSIZED / COMBINED / OVERLAPPING / UNHOSTED / SPLIT / BLOCKED |
| [`action-report-mep-clearance.cs`](../scripts/actions/qa-checks/action-report-mep-clearance.cs) | **An exact gap in millimetres between two MEP runs.** `action-report-nearest-elements.cs` measures with bounding boxes and its own header says to "use `action-report-clashes.cs` when the number must be exact" — but that fragment returns a yes/no with no distance in it. The advice pointed at a fragment that cannot answer |
| [`action-report-level-elevations.cs`](../scripts/actions/reporting/action-report-level-elevations.cs) | **The diagnostic for the defect this harvest found in our own code** (below). Both level heights, the Elevation Base of each level type, the base points, and a one-line verdict |
| [`action-report-nested-families.cs`](../scripts/actions/reporting/action-report-nested-families.cs) | **Ajmal's "four different family names can be one piece of kit."** An AHU with a nested fan, coil and filter is four instances in one category; every count returns 4. `SuperComponent` appeared in **no fragment** — we could walk down but never up |
| [`action-report-fitting-area.cs`](../scripts/actions/reporting/action-report-fitting-area.cs) | **The measurement `action-report-duct-weight.cs` already asks for and could not get.** Its header says fittings at 10% is "an allowance, not a measurement... set fittingsPercent = 0 if the model's fittings ARE in `elements` too" |
| [`action-report-filterable-parameters.cs`](../scripts/actions/reporting/action-report-filterable-parameters.cs) | `ParameterFilterUtilities` was used by **no fragment**, and it answers both ways `ParameterFilterElement.Create` actually throws |

### The techniques those builds are made of

- **Subtract the opening from the service and test the remainder.** An opening is correct when the
  service is entirely inside it where it crosses the structure. Testing the service against the
  structure directly cannot distinguish "goes through the provided hole" from "goes through the
  concrete". Union the services running through the opening, `BooleanOperationsType.Difference` the
  opening's own solid out of them, and test what is left. That one idea is the whole fragment.
- **Host = the largest intersected volume, with an early exit at half the opening's volume.** An
  opening near a wall/slab junction clips both, and "the first one the collector returned" is not an
  answer.
- **A fitting's solid is capped at every connector, and those caps are holes, not metal.** Sum the
  faces, subtract π·r² (round) or H×W (rectangular) per connector. On a small elbow the caps are a
  large share of the total, so leaving them in biases the *mix* rather than just the sum.
- **Clearance is jacket to jacket.** Centreline distance minus each run's outer half-size **plus its
  insulation**. A 100 mm pipe with 25 mm insulation is 150 mm across, and the gap that has to be
  bracketed is between the jackets. Round-to-round is exact; rectangular takes half the *diagonal*,
  which is deliberately pessimistic so a pass is a real pass.
- **Five solid-geometry traps**, all of which fail quietly, are now written up in
  [`knowledge/live-model/geometry-and-transforms.md`](../knowledge/live-model/geometry-and-transforms.md).

## UPGRADE — 8 existing fragments improved, plus 15 corrected

| Fragment | What changed, and why it mattered |
|---|---|
| [`action-report-clashes.cs`](../scripts/actions/qa-checks/action-report-clashes.cs) | **Set B can now be a LINKED model.** This is the most-used fragment here and it collected set B with `new FilteredElementCollector(Document)` — the active document only. **On an MEP job the structure IS the link**, so the honest answer to "check my ducts against the structure" was that it could not, while reporting "0 clashing pairs" in a tone that reads as a pass. Same defect and same shape as the one already flagged on `action-create-from-room-boundaries.cs`. Also: a quick `BoundingBoxIntersectsFilter` now runs before the slow intersection filter |
| [`action-compare-models.cs`](../scripts/actions/qa-checks/action-compare-models.cs) | **Proper `OpenOptions`.** The bare `OpenDocumentFile(path)` on a workshared CENTRAL touches the central. Now detached, worksets closed (a parameter comparison does not need them, and it is dramatically faster), wrong-user locals allowed. Plus: Revit reports a **wrong-version** file as *corrupt*, which sends people looking for damage that is not there — the header is read and the real reason is named |
| [`action-batch-upgrade-revit-files.cs`](../scripts/actions/structural-changes/action-batch-upgrade-revit-files.cs) | Same `OpenOptions` fix, and here it is a **safety** fix rather than a speed one: upgrading and saving a workshared central opened as the central is a genuinely damaging thing to do to a live project. Worksets stay OPEN here on purpose — an upgrade must rewrite every element |
| [`action-report-element-ownership.cs`](../scripts/actions/reporting/action-report-element-ownership.cs) | `WorksharingUtils.GetModelUpdatesStatus` — the **other half** of the safety question. Checkout status says whether you MAY edit; this says whether what you are looking at is still what is in the central. A free-to-edit element can already be changed or deleted there |
| [`action-report-duct-weight.cs`](../scripts/actions/reporting/action-report-duct-weight.cs) | Shape now comes from **`MEPCurveType.Shape`** — Revit's own answer — instead of guessing from which dimension parameters have values, which is the oval trap its own header describes. And the developed area is **cross-checked against Revit's `RBS_CURVE_SURFACE_AREA`**: two independent calculations of the same quantity, so a >5% disagreement is flagged, and the kilos are out by the same proportion |
| [`action-report-length-by-size.cs`](../scripts/actions/reporting/action-report-length-by-size.cs) | Surface area (`RBS_CURVE_SURFACE_AREA`, unread anywhere here before) and optional grouping by system and type — a BOQ line is "Supply Air / Rectangular Duct / 300x150", not a size summed across the building |
| [`action-rename-element.cs`](../scripts/actions/parameters-naming/action-rename-element.cs) and [`action-find-replace-element-name.cs`](../scripts/actions/parameters-naming/action-find-replace-element-name.cs) | `NamingUtils.IsValidName` — **and it fixes a wrong diagnosis, not just a message.** A name with a prohibited character made EVERY element fail, and the summary then blamed *"name collision, or this element type doesn't support renaming"*, sending you to look for a collision that does not exist. Asked ONCE before the transaction in the first (the name is a property of the string), and PER ELEMENT in the second (which builds a different name for each) |
| [`create-levels.cs`](../scripts/creators/create-levels.cs) | `.ToHashSet(...)` → the `new HashSet<string>(sequence, comparer)` constructor. Identical behaviour, and it no longer depends on the machine having .NET Framework 4.7.2 when Revit 2020 targets 4.7 — see finding 4 |
| **15 fragments** switched from `Level.Elevation` to `Level.ProjectElevation` | The defect below |

## What this harvest found in OUR code

### 1. A level has two heights, and fifteen fragments used the wrong one

**The largest finding, and it is silent and conditional — which is the worst combination.**

`Level.Elevation` is measured from whatever the level type's "Elevation Base" parameter says — Project
**or Shared**. `Level.ProjectElevation` is always measured from the project origin. Autodesk's own
wording, from the shipped documentation:

> `ProjectElevation` — *"Retrieves the elevation relative to project origin, **no matter what values of
> the Elevation Base parameter is set**."*

**Every `XYZ` the Revit API hands you is in project-internal coordinates** — location points, bounding
boxes, solid vertices, ray origins, `Room.IsPointInRoom`. So the moment a level height meets a
coordinate, only `ProjectElevation` is in the same space.

Fifteen fragments mixed them: the **whole fire-sprinkler chain** (obstruction check, obstruction survey,
adjust-for-obstructions, sidewall layout, layout options, place heads, deflector height, compliance
audit, NFPA grid), the coverage and routing tools (`action-report-coverage`,
`action-plan-shortest-route`, `generate-room-coverage-layout`), `action-report-ceiling-heights`,
`maximize-level-extents` and both dimensioning fragments. The typical line was

```csharp
double zProbe = room.Level.Elevation + mm(1000);   // then used as a world Z for a ray or a point-in-room test
```

On a test model with Elevation Base = Project the two numbers are identical and nothing shows. On a real
site model set out to a survey datum — normal on any project with real site levels, and normal for
Ajmal's work — every affected answer is wrong by exactly the survey offset, with no exception and a
plausible number. All fifteen are fixed, each with a note in its own header saying what changed and why.

**Two fragments were deliberately left on `Elevation`** and now say so out loud, because a future
session "fixing" them would introduce the error: `action-reassign-level.cs` and
`action-change-wall-constraints.cs` compute a level-to-level *difference* to re-derive an offset
parameter, and the offset an element stores is measured against the same base the level reports — so the
base cancels.

Written up as [`knowledge/live-model/level-elevation-vs-project-elevation.md`](../knowledge/live-model/level-elevation-vs-project-elevation.md),
with [`action-report-level-elevations.cs`](../scripts/actions/reporting/action-report-level-elevations.cs)
as the ten-second check on any model.

### 2. The most-used clash fragment cannot see a linked model

Covered above as an upgrade, but it belongs here too: `action-report-clashes.cs` is run about twenty
times a month, and until today it could not do the single job an MEP coordinator most needs it for. It
did not fail — it returned zero.

### 3. A README row still stated the rule its fragment had been corrected away from

`action-compare-models.cs` was built on 2026-08-24 with the confident, wrong rule *"you cannot match two
models on ElementId"*, and corrected the same day when the full read of a mature implementation showed a
save-as preserves ElementIds. **The fragment was fixed and its `scripts/README.md` row was not.** The row
still read *"Elements are NOT matched on ElementId... the key is category + family + type + Mark"* —
which is now the opposite of what the code does.

That matters more than a stale sentence usually would: `scripts/README.md` is the routing document a
session reads to choose a fragment, so the wrong rule was still live in the place it gets read. **The
consistency checker cannot catch this** — check 3 asks whether a fragment has a row, never whether the
row is still true. Fixed, and worth noting as a class: a correction is not finished until the README row
moves with it.

### 4. `Enumerable.ToHashSet` needs .NET Framework 4.7.2, and Revit 2020 targets 4.7

`creators/create-levels.cs` used `.ToHashSet(StringComparer.OrdinalIgnoreCase)`. That extension arrived
in **.NET Framework 4.7.2**; Revit 2020 add-ins target **4.7**. On a machine with only 4.7.x installed
it is a `MissingMethodException` at run time — **not a compile error**, because the bridge compiles
against whatever runtime is loaded.

It has never bitten because .NET Framework upgrades in place and every current Windows box has 4.8. It
surfaced here only because the container gate pinned the **4.7.1 reference assemblies** for Revit 2020,
while `tools/verify-fragments-compile.ps1` deliberately leaves the framework references empty for a
.NET Framework Revit and lets `csc` use its defaults — which resolve to whatever is installed, i.e. 4.8.
**So the Windows gate is checking a newer framework than the one Revit 2020 targets, and always has
been.** Rewritten to the `new HashSet<string>(sequence, comparer)` constructor, which has existed since
.NET 2.0 and behaves identically. It was the only occurrence in 366 fragments.

**Recommendation, deliberately not implemented here:** point the Revit-2020 branch of
`verify-fragments-compile.ps1` at 4.7.x reference assemblies instead of csc's defaults. That is a `.ps1`
edit, and `CLAUDE.md` is explicit that a PowerShell file written in a Linux container is the exact
encoding trap that has bitten twice — it waits for a session on Windows that can run the result.

### 5. Three tolerances Revit will tell you, and we invent instead

`Application.ShortCurveTolerance`, `AngleTolerance` and `VertexTolerance` are used by **no fragment
here**. A hardcoded `1e-6` can pass our own check and then be rejected by Revit two lines later.

### 6. Our section-creation and our filter-rule handling are already right

Two places where the harvest confirmed us rather than improving us, which is worth recording so nobody
re-derives them:

- **Element-aligned sections.** `creators/create-section-at-element.cs` already builds the section's
  transform from the element's own direction, with the "turn the bounding-box axis by the instance
  rotation" trap documented. Identical approach. **KEEP OURS.**
- **String filter rules across versions.** `ParameterFilterRuleFactory.CreateBeginsWithRule` (and
  Contains / EndsWith and their Not- variants) has **only the 3-argument form on 2020, both on 2024, and
  only the 2-argument form on 2027** — so **no single call compiles across the range**. Our two filter
  fragments already choose the overload by reflection at run time and say so in their headers.
  **KEEP OURS.**

  **And a correction to my own working note, caught by actually compiling it.** From the arity data I
  wrote that `CreateEqualsRule` keeps a 2-argument string form on every version and so was safe written
  plainly. **It does not.** The `/2` overload visible on 2020 is a numeric one; compiling
  `CreateEqualsRule(id, "abc")` against the real 2020 assembly **fails**, and
  `CreateEqualsRule(id, "abc", false)` **fails on 2027**. Equality behaves exactly like the others:
  **no string rule of any kind has a single call spanning 2020 to 2027.** Arity alone cannot tell two
  overloads apart when they differ by parameter TYPE — which is the same shape of mistake as inferring
  behaviour from a name, and the reason the compile gate is worth having in the room while writing.

## SKIP — and why each holds

| Skipped | Why |
|---|---|
| **All 480 XAML files, and the `ViewModels/` + `Views/` folders** (837 files, 69,466 lines) | WPF scaffolding. The bridge has no UI. This is the MVVM cost the brief predicted |
| **1,263 files / 58,397 lines with zero `Autodesk.Revit` reference** | Grepped, not assumed. Config serialisation, localisation, DI wiring, Excel plumbing |
| **Interactive selection** — `ISelectionFilter`, `PickObject(s)`, `AllowElement`/`AllowReference` | Requires implementing an interface. A fragment body cannot declare a class. Fifth instance of that structural limit |
| **Custom export** — `IExportContext`, and `IExternalEventHandler` | Same reason |
| **Batch printing to a physical printer** — `PrintManager`, `PrintParameters`, printer format creation | Needs a printer and Windows printer settings; out of the bridge's reach. **One trick kept**: the print settings are applied inside a transaction that is then **rolled back**, so the model is not left carrying them. Also noted: `OriginOffsetX/Y` replaced `UserDefinedMarginX/Y` at Revit 2022 |
| **Their duct gauge tables** (a foreign national standard) | We have Ajmal's own working values in `knowledge/duct-sheet-metal-takeoff.md`. Importing a different country's table would look authoritative and be wrong here |
| **Pylon / lintel / area-boundary / declaration documentation tools** | Architectural and structural drawing production. Inventoried, nothing absent-and-useful surfaced |

### Revisited, and still declined — but for a better reason than last time

`Solid.IntersectWithCurve` was declined by the first session on the grounds that `create-mep-openings.cs`
and `action-report-clashes.cs` already do real intersection. That comparison was to **clash detection**,
and it holds there. The read found the *other* use: `SolidCurveIntersectionMode.CurveSegmentsOutside`
shoots a ray at a solid and returns where it hits — a **view-independent ray cast**, which is precisely
what `action-report-ray-hits.cs` cannot be. That fragment's biggest documented trap is that
`ReferenceIntersector` only sees what the 3D view shows, so a hidden category makes a ray report "clear"
with a wall standing right there. A solid-curve intersection does not care about views at all.

Still not built, and the reason is honest: `IntersectWithCurve` needs the solid in hand, so it answers
"does this line pass through THIS element and where", not "what is out there". It is a *verification*
tool for a ray hit, not a replacement for the cast. **Recorded as the next candidate** if the ray-view
trap ever costs a real job.

## Small facts worth keeping

- **`RBS_CURVE_SURFACE_AREA`** is Revit's own surface area for a duct or pipe, already computed and
  sitting in the model. For ductwork that IS the sheet area of the straight runs. Nothing here read it.
- **`RBS_REFERENCE_INSULATION_THICKNESS`** gives a duct's or pipe's insulation thickness directly; zero
  means uninsulated. Cheaper than walking to the insulation element, though the insulation's own
  `RBS_CURVE_SURFACE_AREA` is still the way to get its area.
- **A pipe has three diameters.** `RBS_PIPE_DIAMETER_PARAM` (nominal), `RBS_PIPE_INNER_DIAM_PARAM` and
  `RBS_PIPE_OUTER_DIAMETER`. Wall thickness is `(outer − inner) / 2`, and which one belongs in a name
  is a project standard, not a fact.
- **`PartType` is what a fitting IS** — Elbow, Tee, Cross, Transition, TapAdjustable, Union, Cap,
  MultiPort — reached through `(instance.MEPModel as MechanicalFitting).PartType`. Family names are
  whatever somebody typed.
- **`RBS_CALCULATED_SIZE` lists every connector's size** ("300x200-250x200"). For anything but a tee or
  a transition the first segment before the `-` is enough.
- **`Document.EditFamily(family)` → collect `ConnectorElement` → `Close(false)`** answers "how many
  connectors does this family have, and what shape" without placing an instance. Expensive, correct.
- **`WorksharingUtils.GetUserWorksetInfo(modelPath)`** reads a file's workset list **without opening
  it** — the counterpart to `BasicFileInfo.Extract`, and the input to opening only the worksets you need
  via `WorksetConfiguration.Open(ids)`.
- **`WorksetConfigurationOption.CloseAllWorksets`** opens a workshared model with no worksets loaded.
  For a read-only pass over parameters that is dramatically faster and the elements still resolve.
- **`Document.Settings.Categories` filtered on `CategoryType.Model`** is how you enumerate every model
  category, rather than hardcoding a list.
- **`FillPatternElement` where `GetFillPattern().IsSolidFill`** is the language-proof way to find the
  solid fill pattern — the name differs per Revit language. We already do this everywhere. Good.
- **`FamilyInstance.Mirrored`** is a single property and appears in no fragment here. A mirrored
  instance is a real QA finding (mirrored text, kit that cannot be installed that way).
- **`View.EnableTemporaryViewPropertiesMode(view.Id)`** — passing the view's own Id works; passing
  `view.ViewTemplateId` silently creates no temporary view.
- **`UnitFormatUtils.Format`** prints a value exactly as Revit displays it, honouring the project's
  units, accuracy and decimal symbol. Its signature is a hard version split — 6 arguments with
  `UnitType`/`DisplayUnitType` on ≤2020, 5 with `SpecTypeId` on ≥2021 — so it needs reflection. Worth it
  only where a report is being read side by side with Revit's own Properties palette.
- **A defect found by reading, in their code**: a duplicate-detection routine compares an element's
  location to *its own* location (`placedOpening.Location.DistanceTo(placedOpening.Location)`), which is
  always zero, so every candidate passes. Recorded because it is the same shape as the mistakes this
  method exists to catch, and because it is a reminder that mature production code is not a proof.
- **An ambiguity left unresolved rather than guessed at.** Their "host solid without its cuts" routine
  dispatches on `CompoundStructure.IsVerticallyCompound` to choose side faces vs top/bottom faces. The
  API documentation says that property means *"a layout more complicated than a simple set of parallel
  layers"* — **not** "is a wall" — so a plain wall takes the slab path. Whether
  `HostObjectUtils.GetTopFaces` returns anything useful for a wall cannot be settled without a live
  Revit, so nothing here was built on it. If the technique is ever wanted, dispatch on `is Wall` /
  `is FaceWall`, which is provably correct, and guard for an empty face array.

### 7. Ajmal caught a defect in a fragment I had just written, by asking the right question

He asked: *"for me mep opening we have before — am i right that will clash with we make new fragment,
it will collapse or it will get confused and it will not achieve what we need?"*

**He was right, and it was the more dangerous of the two failure modes he named.** It would not have
collapsed. It would have got confused, reported nothing, and looked like a pass.

`recipes/create-mep-openings.cs` calls `Document.Create.NewOpening(...)`, which produces a Revit
**`Opening` element**. An `Opening` is a VOID: `get_Geometry()` yields no usable solid — the class
exposes only `BoundaryRect`, `BoundaryCurves`, `Host` and two transparency flags. The new
`action-audit-mep-openings.cs` extracted solids. And `filters/by-relationship/filter-by-openings.cs`,
the obvious filter to put in front of it, returns mostly that kind. **So the single most natural
composition in the library — find the openings, audit the openings — would have reported "NO GEOMETRY"
for every opening our own recipe had ever cut.** No crash, no error, an empty table that reads as clean.

`place-sleeves-at-wall-penetrations.cs` places a FamilyInstance, which does have solids, so the audit
worked on that half. Half-working is exactly what makes this kind of defect survive a test.

**Fixed**: an `Opening` now gets a solid BUILT from its boundary and its host's thickness. Two rules
came out of doing it, both now in the knowledge note:

- **Extrude generously along the host's normal, never in-plane.** Depth is free — it cannot hide a
  fault, because the hole's SIZE lives in the perpendicular plane. Widening it in-plane would hide an
  UNDERSIZED opening, the one answer that must never be optimistic.
- **Verify the built solid against the element's own bounding box before trusting it.** The API does not
  document what coordinate space `BoundaryRect` is in, so the construction is checked, not assumed; a
  solid whose centroid lands outside the element's own box is rejected and reported as suspect rather
  than audited on geometry in the wrong place.

And the "will it get confused" half was real too: four fragments now carry "opening" or "sleeve" in the
name. [`knowledge/live-model/mep-openings.md`](../knowledge/live-model/mep-openings.md) opens with a
four-way routing table, and all four fragments carry the same cross-reference block in their headers.

**The method lesson.** This is the failure the harvest method is built to catch — *"a plausible rule,
written confidently, from a survey"* — and it got past a full read of the source, three-version compile
checks, and an adversarial re-read of my own fragment. What caught it was **the person who knows what is
already in the model asking whether the new thing fits the old thing.** No amount of reading the
harvested repo would have found it, because the defect was in the seam between the new fragment and
OUR existing one.

### 8. The fragment catalogue was hand-written, and stale within a day

`docs/fragment-catalogue.md` said **359 fragments** against 366 on disk. It is now generated by
[`tools/catalogue-build.mjs`](../tools/catalogue-build.mjs) from `fragment-index.mjs --json`, stamps its
own date, and says so at the top. The consistency checker did not catch it — check 9 looks at the
indexed set and this file lives in `docs/`, deliberately outside it.

## The merge with the parallel session — 388 + 6 = 394

While this harvest was running, the other session merged two PRs into `main`: **28 MEP coordination
fragments** and an audit of them. `main` moved from 360 to 388 while this branch was still at 360 + 6,
so the PR came back `dirty` and the base had to be merged in. Twenty files conflicted; nineteen of them
were nothing but the fragment-count number, which `sync-counts.mjs` recomputes anyway. The two real ones
were `scripts/README.md` (both sessions inserted rows in the same table) and `knowledge/brain-log.md`
(both appended entries the same day) — **both resolved by keeping both sides**, with this branch's row
winning for `action-report-clashes.cs` because this branch changed that fragment.

**Checked, and it came back clean:** none of their 28 new fragments carries the `Level.Elevation`
defect. The only remaining uses in the whole 394 are the three this harvest deliberately left — two
level-to-level differences where the base cancels, and one display line.

**Two real overlaps, and neither is a duplicate — but a session would have picked whichever it found
first, so all five now cross-reference each other:**

| Question | Fragments | The distinction |
|---|---|---|
| How close are these two? | `action-check-minimum-clearance.cs` and `action-check-insulation-clearance.cs` (theirs) vs `action-report-mep-clearance.cs` (this branch) | Theirs works on **any element pair** and measures by **sampling points** on the solids — general and approximate, and its own header says to check a reported gap against a Revit dimension. This branch's is **linear runs only** and **exact** (`Curve.ComputeClosestPoints`). General-and-approximate versus narrow-and-exact |
| Is this sleeve/opening right? | `action-check-sleeve-size.cs` (theirs) vs `action-audit-mep-openings.cs` (this branch) | Theirs is a **specification** check — hole big enough for service + insulation + annular clearance, not so big that fire-stopping suffers. This branch's is a **coordination** check against the structural link — nothing runs through it, the service leaves it and re-enters concrete, it spans two structure types, it landed in a column. Run the size check when the sleeves are placed; run the audit after the MEP moves. They overlap on *undersized* alone and disagree on nothing |

That is the same "will it get confused" question Ajmal asked about the openings, arriving a second time
at merge scale — and the answer was the same shape. **Two sessions adding to one library will produce
fragments that answer neighbouring questions, and the seam between them is nobody's file.** Worth doing
deliberately at every merge, not just when someone asks.

## State at the end

- **366 fragments.** 6 new, 7 upgraded, 15 corrected for the elevation defect, 2 annotated to prevent a
  wrong "fix".
- **The compile gate ran here, in a Linux container, against the real shipped `RevitAPI.dll` for Revit
  2020, 2024 and 2027** — Roslyn under Mono, with the same harness shape as `tools/check-scripts.ps1`
  (including its `prelude-smoke-test.cs` special case). It was validated by running the **whole
  pre-existing library** through it first. Final result, the whole library on every version:

  | Revit | Result, this branch's 366 before the merge |
  |---|---|
  | 2020 | **366 pass, 0 fail** |
  | 2024 | **366 pass, 0 fail** |
  | 2027 | **366 pass, 0 fail** |

  **After merging the parallel session's 28, three of THEIR fragments do not compile** — measured, not
  assumed, and NOT caused by anything on this branch:

  | Fragment | 2020 | 2024 | 2027 | Cause |
  |---|---|---|---|---|
  | `action-check-flow-direction.cs` | FAIL | FAIL | FAIL | `BuiltInParameter.RBS_SYSTEM_TYPE_PARAM` — **not a real API name on any version** |
  | `action-connect-open-connectors.cs` | FAIL | FAIL | FAIL | same |
  | `action-check-plumbing-fixture-connectivity.cs` | FAIL | pass | pass | `BuiltInCategory.OST_PlumbingEquipment` — **2024+ only** |

  Two of them cannot run on ANY Revit. **They are left as they are**: the standing rule in `CLAUDE.md`
  is that a compile FAIL naming a file another session wrote is reported, not fixed, and widening this
  PR to repair another PR's work is the wrong shape. Reported with the patches instead:

  - `RBS_SYSTEM_TYPE_PARAM` does not exist. The real names are domain-specific —
    `RBS_DUCT_SYSTEM_TYPE_PARAM` and `RBS_PIPING_SYSTEM_TYPE_PARAM` (both present 2020–2027) — or the
    general `RBS_SYSTEM_CLASSIFICATION_PARAM`. Both sites use it as a *fallback* for the system NAME, so
    trying duct then pipe is the faithful fix.
  - `OST_PlumbingEquipment` arrived at 2024. Reach it by reflection (`Enum.TryParse`) so 2020 simply
    skips that category, which is the version-proof pattern this library already uses elsewhere.

  **Worth noting how they got in**: the other session's own ledger claims all 388 compile on
  2020/2024/2027. Two of these fail on every version, so that claim was never measured on those two.

  **This does not replace `tools\check-scripts.cmd` on the Windows PC** — that checks against the Revit
  versions actually installed there — but it is no longer true that nothing can compile-check from a
  container.
- **Nothing has been run against a real model.** Everything new is read-only; the two upgraded
  background-open fragments are the only ones that touch another file, and both are now safer than they
  were, not less safe.
