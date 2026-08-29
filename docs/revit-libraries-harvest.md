# Harvesting a set of Revit developer libraries into the Brain — the ledger

**2026-08-23.** Ajmal pointed at a GitHub account rather than a single repository and said there was a
lot in it. There was: **17 repositories, of which 12 were worth cloning — 1,186 C# files, ~139,000 lines.**
This is the third harvest of the day and the first where the target is **libraries rather than tools**.

Working ledger, not knowledge — it lives in `docs/`, outside the search index, so a fresh session never
re-reads something already judged. Method and the two earlier worked examples:
[`pyrevit-harvest.md`](pyrevit-harvest.md), [`pyrevit-platform-harvest.md`](pyrevit-platform-harvest.md).
**Per the standing rule nothing here names the source** — not the account, the author, the repositories or
the packages. Everything kept was rewritten as this Brain's own C#, in its own words.

## Why this one was different, and what that changed about the method

The two pyRevit harvests read **tools**: a button, a job, a verdict. Here there were almost no tools. What
there was instead:

| Kind | Size | What a "unit" is |
|---|---|---|
| An extension-method library over the Revit API | 146 files, 19.5k lines | one wrapped API family per file |
| A model-inspection application | 492 files, 41.4k lines | 132 **descriptors**, one per Revit type |
| A plugin-authoring toolkit | 89 files, 9k lines | base classes, handlers, options |
| An AI-agent skill set for the Revit API | 60 markdown files | written-down technique |
| One real plugin | 13 files, 562 lines | a batch file-upgrade job |
| A .NET reflection engine, its WPF layer, test/benchmark harnesses, templates, build tasks, dependency tools | ~65k lines | infrastructure |

**A library has no button label to be misled by — and that removed the survey's usual value.** So the
mechanical pass became an **API-surface diff** instead: extract every `*Utils` / `*Filter` / `*Manager`
class the harvest touches, and check each against this library's 347 fragments. That is what produced the
build list, and it is the step worth repeating on the next library-shaped harvest.

The full read then did what it did the last two times: **the most valuable findings were defects in our
own code**, and none of them was visible in an API list.

## What the read found in OUR code — five defects, two of them silent data loss

### 1. `action-purge-unused.cs` could offer a material that IS in use for deletion

`Element.GetMaterialIds(bool)` returns **two different sets**. `false` gives geometry and
compound-structure materials; `true` gives **paint** materials — applied face by face with Modify > Paint.
The materials mode read only the `false` set, so a finish that exists purely as paint had no user and was
classified unused. Deleting it strips the paint off every face it was on. Dry-run-by-default limited the
blast radius, but the *report itself* was wrong, and telling you what is safe to delete is the entire job
of that fragment. Both sets are now unioned.

### 2. `action-report-material-takeoff.cs` under-reported by the same mechanism

Same blind spot, different consequence: a painted finish reported as zero. Fixed, and with the nuance
that matters — **paint has area but never volume** (there is no paint overload of `GetMaterialVolume`), so
a paint-only material now shows an area against a blank volume, and the table sorts by area so painted
finishes are not buried. The area call has to be given the same flag as the id call, which is the part
that would have been got wrong on a quick fix.

`filter-by-material.cs` already had an `includePaint` switch and was correct — checked, not assumed.

### 3. `action-report-clashes.cs` gave a clean bill of health for elements it never tested

`ElementIntersectsElementFilter` does not accept every element. The old code wrapped it in
`catch { continue; }` and still reported "checked N elements". **A coordination report that quietly
under-reports is worse than one that fails**, because nobody goes back to it. Revit answers in advance —
`ElementIntersectsFilter.IsElementSupported()` and `.IsCategorySupported()` — so both sets are now
pre-checked and anything untestable is listed by Id and category under UNTESTED, with the summary
separating "tested" from "in the set".

`filter-by-element-intersection.cs` had the same blind spot in the loud direction (it threw) and now
reports which of the two checks refused, and says plainly that this is not the same as "nothing clashes".

### 4. `load-family.cs` recorded a ceiling that is not there

Its header said overwriting an existing family "needs an `IFamilyLoadOptions` implementation" — which
read as out of reach, because a fragment cannot declare a class. True of a class *we* would write. Not
true here: **`UIDocument.GetRevitUIFamilyLoadOptions()` returns Revit's own implementation**, the one
behind File > Load Family. Handing it to `LoadFamily` makes Revit ask the question in its own dialog, so
the reload path is available *and* nothing is silently overwritten — better than either the old "cannot"
or a handler of our own. Added as an opt-in that warns it shows a modal prompt, reached by reflection so
one source still compiles everywhere.

**The general rule is now written into
[`failure-handling-without-a-class.md`](../knowledge/live-model/failure-handling-without-a-class.md):
before recording a technique as impossible because it needs an interface, check whether Revit already
ships an implementation.** `IFailuresPreprocessor` and `IDuplicateTypeNamesHandler` genuinely have none.
This one did, and it sat in the header as a fact for weeks.

### 5. `model-health-audit.cs` was not asking Revit what Revit thinks

Revit ships its own model rule engine — `PerformanceAdviser.ExecuteAllRules(doc)`, behind Manage >
Performance Adviser — and nothing here ran it. Added as section 9, with section 10 listing the **add-in
updaters registered in the session**: other people's code that runs whenever matching elements change,
which is the honest answer to "why did that value change by itself" and appeared nowhere in this Brain.

## Two facts the compile check taught us, which no amount of reading would have

Both found by `check-scripts` refusing to build the new fragments:

- **`GlobalParameter.GetLabels()` does not exist.** The real member is **`GetLabeledDimensions()`**,
  present on Revit 2020 through 2027. Confirmed by reading the member-name strings straight out of
  `RevitAPI.dll` — a fast way to settle "what is this member actually called" without opening Revit or
  loading the assembly, worth remembering.
- **`DataStorage` is inaccessible to a fragment.** It is a normal-looking public API type, and naming it
  is a compile error — *"inaccessible due to its protection level"* — on every version here. So the
  invisible document-wide extensible-storage carrier is recognised by `GetType().Name` instead. Same
  discipline as a version-gated member: do not name the type.

## BUILD — 9 new fragments

| Built | The thing it knows |
|---|---|
| [`action-report-element-dependencies.cs`](../scripts/actions/reporting/action-report-element-dependencies.cs) | **"What goes with it if I delete this."** `element.GetDependentElements(null)` is Revit's own list of what it would take too — tags, dimensions, hosted families, the sketch a floor is drawn from, openings through it — and it is not derivable by hand. The list always contains the element itself, which is why counts read one high. Paired with `DocumentValidation.CanDeleteElement`, because an element with zero dependents can still be undeletable. Joins and cuts are reported separately: Revit does not delete those, it re-cleans the geometry, which is the change nobody sees coming |
| [`action-connect-air-terminals.cs`](../scripts/actions/structural-changes/action-connect-air-terminals.cs) | **`MechanicalUtils.ConnectAirTerminalOnDuct` cuts the tap and connects both ends in one call.** The layout half of this job was already here — the terminal-layout skill, `place-terminals-checkerboard.cs` — and every layout ended as loose components because nothing joined them to the ductwork. Automatic duct choice measures to the terminal's own CONNECTOR, not its insertion point, since a ceiling diffuser's connector is on top of it |
| [`action-check-open-pipe-ends.cs`](../scripts/actions/qa-checks/action-check-open-pipe-ends.cs) | `PlumbingUtils.HasOpenConnector` / `PlaceCapOnOpenEnds` — Revit's own answer to "is this run terminated", against the same state the system browser reads. Reports before it fixes, because **an open end is often deliberate** (a riser waiting for the next level) and capping them all makes a model look finished while hiding real gaps. Records that there is **no duct equivalent** — the call lives in PlumbingUtils only, which is ten minutes saved for whoever looks for the symmetrical one |
| [`action-report-external-references.cs`](../scripts/actions/reporting/action-report-external-references.cs) | **Every file the model depends on, not just the RVT links.** `context-linked-models.cs` collects `RevitLinkInstance`, so a model whose real risk is three unresolved DWGs and a point cloud reported as clean. `ExternalFileUtils.GetAllExternalFileReferences` is one list covering links, imports, point clouds, keynote tables and decals. Both paths are printed, because the SAVED path and what it RESOLVES TO now are different things and the saved one is usually still right after a move |
| [`action-report-addin-data.cs`](../scripts/actions/reporting/action-report-addin-data.cs) | **The fourth place data hides.** Extensible Storage puts values inside the .rvt with nothing in the UI showing them — where a tool records "I processed this", where a coordination system keeps its ids. `Schema.ListSchemas()` plus an `ExtensibleStorageFilter` per schema gives what is there and who carries it. **A vendor-locked schema refuses to be read and that is respected** — see the SKIP note below |
| [`action-batch-upgrade-revit-files.cs`](../scripts/actions/structural-changes/action-batch-upgrade-revit-files.cs) | **"I installed a newer Revit" — the content half.** `check-scripts.cmd` already answered whether the SCRIPTS still work; nothing moved the family library. `Application.OpenDocumentFile` returns a document with no UI window, which is why the bridge can save it. Writes to a different folder always: an upgrade is one-way, so overwriting the originals destroys the only copy the old Revit can still open. Project files are off by default because a workshared central is a project decision, not maintenance |
| [`action-report-global-parameters.cs`](../scripts/actions/parameters-naming/action-report-global-parameters.cs) | **A global parameter is not a project parameter** — one value for the whole model, attached to dimensions, so changing it moves geometry. Nothing here could see them, which meant a model driven by them looked from this side like a model where things moved for no reason. The useful part is `GetLabeledDimensions()`: the list of what moves when it changes |
| [`action-report-view-references.cs`](../scripts/actions/sheets-views/action-report-view-references.cs) | **A section mark and the view it opens are two different elements**, and the numbers on the bubble come from the view, not from the mark — so a wrong bubble is almost never a text problem. `ReferenceableViewUtils.GetReferencedViewId` / `ChangeReferencedView` reads and re-points it. The case it exists for: a duplicated set whose marks still reference the originals |
| [`action-create-assembly-views.cs`](../scripts/actions/sheets-views/action-create-assembly-views.cs) | **An assembly is the only thing in Revit that generates its own drawing set.** `AssemblyViewUtils` makes views OWNED by the assembly — they show only its members and orient to its own origin, which `create-view.cs` cannot reproduce with a cropped project view. `CreateSheet` takes a title-block TYPE id. Guards against a second set, since the API has no "already has views" check |

## UPGRADE — 6 existing fragments

The five defects above, plus **[`filter-by-phase.cs`](../scripts/filters/by-status/filter-by-phase.cs)**,
which could answer only half its own question. Phase Created is **authorship**; `GetPhaseStatus(phaseId)`
is **status**, and a phase plan is drawn from status. A wall created in Phase 1 is *Existing* when the view
is set to Phase 3 — so "give me the existing services" was unanswerable from Phase Created. Both modes now.

## KEEP OURS — where the harvest offered something and ours is right

| Theirs | Ours | Why |
|---|---|---|
| `MechanicalUtils.BreakCurve` / `PlumbingUtils.BreakCurve` | `action-place-accessory-on-run.cs` | Already uses both, and carries two hard-won facts theirs does not state: **BreakCurve swaps which half keeps the original Id**, and it does **not** connect the two halves |
| Wall / framing join control | `action-disallow-join.cs` | Already covers `WallUtils` and `StructuralFramingUtils` as the two separate calls they are, **and** detects the silent no-op on a pinned element |
| Crop-boundary validation (`BoundaryValidation.IsValidBoundaryOnView`) | `action-set-view-crop-to-shape.cs` | Ours already asks `ViewCropRegionShapeManager.IsCropRegionShapeValid` — **the better call**, because theirs is Revit 2023+ and ours works on every version here |
| `ElementTransformUtils.GetTransformFromViewToView` | `action-align-viewports-across-sheets.cs` | Considered and rejected: that transform maps MODEL points between views. Aligning viewports is a SHEET-space problem, and ours is right to work from the viewport box centre with the title-block-origin and scale preconditions it checks |
| Unit conversion wrappers over `UnitUtils` | plain arithmetic in `lib/prelude.cs` | Deliberate. 1 ft is exactly 304.8 mm by definition, so the conversion cannot be deprecated — which is why 93 fragments stopped naming a unit API at all. Re-introducing `UnitUtils` would undo the 2026-08-20 migration |
| Worksharing ownership helpers | `action-report-element-ownership.cs` | Same `WorksharingUtils` data, one fragment |
| Extension methods over `ElementTransformUtils`, `SolidUtils`, `JoinGeometryUtils` | the `move-copy-rotate/` and `structural-changes/` folders | Thin wrappers; the call is already made where it is needed |

## SKIP — and one of them is a deliberate refusal

- **All WPF/XAML, ribbon wiring, dockable panes, themes, converters, icons** (~50k lines). The bridge has
  no screen. Same verdict as both pyRevit harvests.
- **The add-in scaffolding** — project templates, MSBuild tasks, dependency-conflict tooling, installer
  generation, NuGet packaging of the Revit API assemblies (340 MB of repackaged Autodesk DLLs, not
  cloned). [`START-HERE.md`](../START-HERE.md) rules out working on the add-in that provides the bridge;
  this is that, for a different add-in.
- **The .NET reflection engine and its UI adaptation** — general-purpose object inspection, not Revit.
- **Test and benchmark harnesses** — they run code *inside* Revit from a test runner, which is the
  bridge's own job, done differently.
- **`IDirectContext3DServer` visualisation servers** — drawing solids, faces and meshes into the view. A
  server is an interface implementation, so **a fragment cannot declare one.** Genuinely out of reach,
  and no built-in implementation exists (unlike the family-load case above). Recorded rather than
  half-attempted.
- **Source generators and Roslyn analysers** — compile-time tooling for a codebase we do not have.
- **A native memory patch that unlocks vendor-locked Extensible Storage.** It resolves an undocumented
  exported symbol in one of Revit's own DLLs and overwrites the access-check function pointer with zero
  so that closed schemas can be read. It is ingenious and it is **not going in this Brain**: it defeats
  another vendor's decision about their own data, it depends on a mangled C++ symbol that changes without
  notice, and it patches live process memory in a session holding Ajmal's open model. `action-report-addin-data.cs`
  asks `schema.ReadAccessGranted()`, reports the refusal, and stops. **This is the harvest's one refusal,
  and it is recorded so nobody re-derives it as a good idea.**

## Knowledge added

- **[`live-model/query-cost.md`](../knowledge/live-model/query-cost.md)** — what a collector actually
  costs, and the four choices that change it: a **view-scoped collector is ~6× faster**; an existence
  check is ~80× cheaper than a count; `ToElementIds()` beats `ToElements()` beats `.Cast().ToList()`;
  `UnionWith` is ~2.6× the multi-argument filter (which this Brain already avoided for a *correctness*
  reason — it silently drops quick filters). **The note says plainly that these ratios come from a small
  seeded model and that the direction transfers, not the microseconds.** It also corrects the usual
  advice about parameter filters: on a small set they matched LINQ exactly, and their real win is that
  elements never materialise, which only matters at scale.
- **[`revit-version-compatibility.md`](../knowledge/revit-version-compatibility.md)** — *why* reflection
  and not a version check. The runtime resolves a method's types when the method is entered, so
  `if (version >= 2023) { NewApi(); }` throws before the `if` is evaluated: the guard is inside the thing
  it guards. Library code gets around it with a separate non-inlined method; **a fragment has no methods,
  so reflection is the only route.** Plus: a major version number is no longer enough, because the API
  has changed *within* Revit 2026.
- **[`failure-handling-without-a-class.md`](../knowledge/live-model/failure-handling-without-a-class.md)**
  — the "check for a shipped implementation first" rule, from the `load-family.cs` finding.

## State at the end

**All 398 fragments compile on Revit 2020, 2024 and 2027.** Two of the nine new fragments failed their
first compile and were fixed (`GetLabeledDimensions`, `DataStorage`) — the check earning its keep again.

**None of the 9 new fragments, and none of the 6 upgrades, has been run against a real model.** Every one
is report-only or dry-run by default and says so in its own header. Run each on ONE element and check the
real result before a batch — in particular `action-connect-air-terminals.cs`, which changes ductwork, and
`action-batch-upgrade-revit-files.cs`, which writes files.

**Two other Claude sessions were writing to this repo at the same time as this harvest.** They added
fragments of their own (`action-set-link-overrides.cs`, `action-audit-view-filters.cs`,
`action-report-curtain-elements.cs`, `action-report-tags-and-targets.cs`, `action-report-areas.cs`,
`action-set-view-underlay.cs`). Those are theirs, not this harvest's — recorded here only so the counts
and the git history are readable later.

---

# Addendum — a fifth harvest, the same evening: a tag-alignment add-in

**2026-08-23, later.** Ajmal pointed at one small repository: a tag alignment and arrangement add-in.
**9 C# files, 1,849 lines of real code** — two tools (Align, Arrange), one shared abstraction over
annotation elements, and a helpers file. It lands directly on his most-used ground: `action-dimension-rooms`
and `action-center-room-tags` are two of his six most-run fragments.

Small repository, **four findings**, one of them a live defect in a fragment we ship.

## What it found in OUR code

### `action-stack-tags.cs` was measuring the LEADER, not the tag

The automatic row gap came from `el.get_BoundingBox(view)` on each tag, described in its own header as
"measures the tallest tag in the view". **A tag's bounding box includes its leader line.** On a busy plan
that box is metres tall, so the automatic gap would have blown the stack apart — tags spaced by somebody's
leader length instead of a text height. It has never been run on a real model, so this would have surfaced
as "why are my tags three metres apart" on first use.

Fixed by measuring **leaderless tags only** — the only ones whose box is honest — and, when every tag in
the set has a leader, saying so and using a stated paper default rather than a wrong measured one. The
proper measurement is available as an opt-in and is the technique below.

### Measure-then-roll-back: how to measure something whose geometry depends on state you do not want

The harvested tool's entire preparation phase exists for this problem, and its shape is worth having:

> open a TransactionGroup → set `HasLeader = false` on every tag → **COMMIT** → measure the now-honest
> boxes → **roll the group back** so the leaders are exactly as they were → then do the real work.

The commit is the part that is easy to get wrong: measuring inside the still-open transaction returns the
old geometry, because the box only regenerates on commit. It is the same family as the
create-then-roll-back fixture trick already used in `action-report-sheet-title-blocks.cs`, but for
measurement rather than for fixtures. Recorded in
[`live-model/tagging.md`](../knowledge/live-model/tagging.md).

### The version boundary we had flagged as a guess is contradicted — honestly

[`revit-version-compatibility.md`](../knowledge/revit-version-compatibility.md) said `IndependentTag`
lost its single-reference members **in 2023**, and said plainly: *"2023 itself is not installed on this
machine; the shim names 2023 as the boundary and nothing contradicts that."* Something now does. This
add-in's conditional compilation splits at **2022** — `LeaderEnd` under 2019/2020/2021, `GetLeaderEnd`
from 2022 — and this Brain's own `action-report-tags-and-targets.cs`, written independently, also records
the change as "from 2022".

**It still cannot be proved here** (2021–2023 are not installed), so the note now says the boundary is
*probably* 2022, names both sources, and keeps what IS proved: present on 2020, gone by 2024. No code
depends on it — every fragment reaches these members by name at run time. The point of recording it this
way is that **the year was the guessed part, and a contradicted guess should say so rather than quietly
becoming a different confident number.**

### `action-center-room-tags.cs` — a proven fragment with an unproven line in it

Its header says *"this only moves the HEAD. Revit draws the leader automatically; it does not need
setting."* The live verification behind that ran on three centred room tags, and a room tag sitting on its
own room normally has no leader — so **the leadered case was never exercised.** A `SpatialElementTag`'s
leader end is a settable point, and the harvested code deliberately captures it before moving the head and
writes it back after, which is strong evidence the head drags it.

Flagged in the header rather than changed. Rewriting the move path of a proven, frequently-run fragment on
someone else's evidence is how a working tool gets broken; the fragment now says exactly which case is
unproven and what the fix would be. Its direction of travel also helps — it moves tags *into* their room,
where a leader is least likely.

## BUILD — 1 fragment

[`action-arrange-tags-to-view-edges.cs`](../scripts/actions/sheets-views/action-arrange-tags-to-view-edges.cs)
— park every tag in a column down the left and right crop edges, leaders fanned so they do not cross. **The
opposite job to `tag-elements-in-active-view.cs`**, which scores each new tag into a spot NEAR its element;
this is for views where nothing can sit near its element without covering something else — a plant room, a
riser, a dense detail. Not a duplicate: checked before building.

Three things it had to learn, all from the read:

- **The crop box is the coordinate system.** "Left" and "up" on the paper are not model X and Y on a
  rotated or sloped view. Every head and anchor goes through `view.CropBox.Transform.Inverse`, the
  arithmetic is flat 2D there, and the result comes back through `view.CropBox.Transform`. In model
  coordinates it works on an unrotated plan and fails silently everywhere else.
- **The crop must be ON, and that is a refusal not a default.** The crop edge IS what the tags are parked
  against; with no crop there is no honest answer to "how far out", so it reports and stops.
- **Uncrossing is a swap.** Two tags whose leaders cross are holding each other's slot, so exchange them;
  two passes settle almost every real view. It is a heuristic, so the crossings left over are counted and
  reported rather than claimed as zero.

## KEEP OURS

| Theirs | Ours | Why |
|---|---|---|
| Align left/right/top/bottom/centre/middle on a selection | `action-align-elements.cs` | Already aligns any elements on a chosen axis against a reference element, and is PROVEN |
| Distribute vertically/horizontally | `action-array-elements.cs`, `action-stack-tags.cs` | Covered between the two |
| Their `Arrange`'s tag PLACEMENT scoring | `recipes/tag-elements-in-active-view.cs` | Ours is substantially better for placing tags near elements — scored candidates, side consistency, a live registry — and was tuned against his own real drawings |
| Debug spheres / draw-a-circle-at-a-point helpers | `create-line.cs`, `create-point-based-element.cs` | Same idea, already available; not worth a fragment of its own |

## SKIP

The WPF window and ribbon wiring, the localisation resource plumbing, the `ExternalCommand` scaffolding,
and their `IFailuresPreprocessor` for the temporary commit — a fragment cannot declare a class, and
`SetForcedModalHandling(false)` covers the need (see
[`failure-handling-without-a-class.md`](../knowledge/live-model/failure-handling-without-a-class.md)).

## State

`action-arrange-tags-to-view-edges.cs`, `action-stack-tags.cs`, `action-center-room-tags.cs` and
`action-remove-tags.cs` all compile on **Revit 2020, 2024 and 2027**. The new fragment and the stack-tags
fix have **not been run against a real model**; both are dry-run by default.
