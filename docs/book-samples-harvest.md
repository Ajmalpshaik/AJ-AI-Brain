# Harvesting a set of Revit API book samples — the ledger

**2026-08-23.** The fourth repository of the day. Same method and the same four-way verdict —
**BUILD** / **UPGRADE** / **KEEP OURS** / **SKIP** — as
[`pyrevit-harvest.md`](pyrevit-harvest.md), [`pyrevit-platform-harvest.md`](pyrevit-platform-harvest.md)
and [`rag-addin-harvest.md`](rag-addin-harvest.md).

Working ledger, not knowledge — it lives in `docs/`, outside the search index. Per the standing rule
nothing here names the source; everything kept was rewritten as this Brain's own C#.

## What is in it

**8 distinct samples, 37 C# files, 2,155 lines** — small enough to read in full, which is what happened.
Teaching samples written against Revit 2017, each a standalone add-in with a WPF window in front of a
short piece of API work.

Two things the survey settled before any reading:

- **Two of the nine folders are the same sample.** The two batch-upgrade projects have byte-identical
  code; one just adds a ribbon tab. So it is 8 samples, not 9.
- **The API surface is thin and the UI surface is thick.** `MessageBox.Show` 20, `FolderBrowserDialog`
  7, WPF binding scaffolding throughout, against a genuine Revit API list short enough to write out:
  `Grid.Create`, `Line.CreateBound`, `View3D.CreateIsometric`, `Document.Export`, `Document.SaveAs`,
  `Document.EditFamily`, `Document.LoadFamilySymbol`, `FilteredElementCollector`. That ratio is what a
  teaching sample looks like, and it is why the SKIP column below is mostly "the window".

**A live overlap, recorded because it affected the verdicts:** a second session was working in this repo
at the same time and had already added `action-batch-upgrade-revit-files.cs`, which covers the same job
as the batch-upgrade sample. That moved it from BUILD to UPGRADE — see below. Its own ledger is
`docs/revit-libraries-harvest.md`.

## BUILD — 3 new fragments

All three compile on Revit 2020, 2024 and 2027. **None has been run against a real model.**

| Built | The gap it fills |
|---|---|
| [`action-export-families.cs`](../scripts/actions/sheets-views/action-export-families.cs) | **Pull every loadable family OUT of a project as .rfa.** `Document.EditFamily` appeared in NO fragment here, so "get me that family out of this model" had no answer at all — the exact reverse of `creators/load-family.cs`. It works because `EditFamily` hands back a document with **no UI window**, and the bridge's save restriction is about the UI document only — the same mechanism that lets families be authored end to end. Three kinds of family will not come out and none is an error: **system** families (walls, ducts — no .rfa exists), **in-place** families, and anything the file cannot open. Name clashes are real and silent — two families can share a name and the second would overwrite the first — so the second is numbered |
| [`action-export-3d-to-fbx.cs`](../scripts/actions/sheets-views/action-export-3d-to-fbx.cs) | **FBX**, the handover format for Navisworks, 3ds Max and Twinmotion. The library exported to IFC, NWC, PDF, DWG, images and CSV and had no FBX path. **The export call is the easy half** — the value is preparing the view, because FBX carries exactly what the view draws: a live section box ships a sliced building, a hidden category ships a missing trade, Coarse detail ships ducts as sticks, and annotations arrive as floating junk. `prepareCleanView` builds a correct isometric, exports it, and deletes it again so nobody's working view is touched |
| [`create-grid-series.cs`](../scripts/creators/create-grid-series.cs) | **A whole structural grid from bay spacings** — "6000, 6000, 7500 across; 8000, 8000 up" — both directions, auto-named A/B/C and 1/2/3. `creators/create-grid.cs` takes explicit mm endpoint pairs, which is right when the lines are known and means computing every coordinate by hand for a bay layout. **Spacings are gaps, not positions**: four spacings make five grids, and reading them the other way gives one grid too few with every line misplaced. Letters increment the way a drawing does — A..Z then AA, AB — and a name Revit already holds is refused, so the grid is created and reported rather than losing the run to one clash |

## UPGRADE — 1

**[`action-batch-upgrade-revit-files.cs`](../scripts/actions/structural-changes/action-batch-upgrade-revit-files.cs)**
— written by the other session about an hour before this harvest, and correct as far as it went. Two
things the sample does that it did not:

- **`SaveAsOptions.PreviewViewId`.** A Revit file's thumbnail comes from ONE nominated view. Save without
  saying which, and the upgraded copy can come out with a blank preview — and **for a family library
  browsed by thumbnail in Revit's own dialog, that is most of how anyone finds anything.** The fallback
  order matters too: keep whatever the file already nominated, else the first 3D view Revit will accept,
  else the first plan. `DocumentPreviewSettings.IsViewIdValidForPreview` is the gate — not every view can
  be a preview, and handing it one that cannot makes `SaveAs` throw.
- **`SaveAsOptions.Compact = true`.** An upgrade rewrites the whole file anyway, so compacting is free
  here and is the difference between a library that grows every version and one that does not.

## KEEP OURS

| Their sample | Ours | Why ours stays |
|---|---|---|
| **2.3 FamilyLoader** — loads a family symbol, with a hand-written `IFamilyLoadOptions` class deciding the overwrite behaviour | `creators/load-family.cs` | **Ours is better, and for a reason worth restating.** A fragment body cannot declare a class, so it cannot implement `IFamilyLoadOptions` — which reads as "out of reach". It is not: `UIDocument.GetRevitUIFamilyLoadOptions()` returns **Revit's own implementation**, the one File > Load Family uses, so Revit asks the question in its own dialog instead of anything being overwritten silently. That was found earlier the same day and is already in the fragment. **Noted, not built:** `Document.LoadFamilySymbol(path, typeName, options, out symbol)` loads ONE type rather than the whole family, which ours does not do — and the same `GetRevitUIFamilyLoadOptions` unlocks it |
| **1.9 RoomNumbering** — renumber rooms grouped by level | `action-renumber-sequential.cs` | Same job, more general. **Their version has a defect worth recording rather than copying: a tool called RoomNumbering sets `room.Name`, not `room.Number`** — so it writes the name and leaves the number alone. The idea underneath is sound and is what makes it worth mentioning at all: derive a level ordinal by regex on the level NAME, then number as `level*100 + index`, with the sign flipped for basements so B1 runs -100, -101. That is how storey-based numbering is actually specified |
| **2.2 ExportImport** — every element of a category to CSV | `action-export-parameters-to-csv.cs`, `action-export-schedule-to-csv.cs` | Different question, and ours matches the one that gets asked: ours takes a named list of columns; theirs discovers the UNION of every parameter across every element and writes a variable-width table. **Noted, not built** — the "everything" dump is a real variant for a handover audit, and nobody has asked for it |
| **2.1 ElementSearch** — pick a category, list elements, select one | `filters/filter-by-category.cs` + the native `select_elements` tool | One filter plus one tool call. The sample is 350 lines of WPF and MVVM around that |
| **1.7 GridCreation** (the picking half) | — | `Selection.PickPoint` is an interactive prompt at the Revit UI. There is no bridge equivalent and there should not be — a base point arrives as a number in the request. The *layout* half became `create-grid-series.cs` above |

## SKIP — nothing transferable

- **Every WPF window and its code-behind** — `FormUpgrade`, `FormExporter`, `FormCreation`, `FormExport`,
  `FormSearch`, the XAML, `NotifyObjectBase`, the `ObservableCollection` view-models. The bridge has no
  UI. This is the bulk of the repository.
- **Ribbon and add-in plumbing** — `Application.cs` × 4, `Command.cs` × 8, nine `.addin` manifests, nine
  `AssemblyInfo.cs`, the icons. Out of scope by [`START-HERE.md`](../START-HERE.md).
- **`Thirdparty/`** — vendored DLLs.
- **Progress bars and `DoEvents()`** — a WPF idiom for keeping a window responsive mid-loop. Meaningless
  across a bridge call, where the reply is the progress report.

## Small facts worth keeping

- **`Family.IsEditable` is the honest gate for "can this come out as .rfa"** — false for system families
  and in-place families, and that is a fact about the family, not a failure to report.
- **`EditFamily` can in principle hand back something that is not a family document**, so
  `IsFamilyDocument` is worth checking before `SaveAs` — saving a project to a `.rfa` path produces a
  file that looks right and opens as the wrong thing.
- **A family document opened this way must be `Close(false)`d inside the loop.** Leaving them open is
  what makes a library-sized export run out of memory halfway through.
- **`Document.Export` has thirteen overloads and they do not agree on their arguments.** FBX, DWF and
  DWFX take a `ViewSet`; DWG, DXF, DGN and SAT take an `ICollection<ElementId>`; IFC, NWC, OBJ, STL and
  gbXML take neither and export the whole document; PDF takes an `IList<ElementId>`. Reaching for the
  wrong shape is a compile error, which is the good outcome.
- **`View3D` carries `CanModifyViewDiscipline()`, `CanModifyDetailLevel()` and `CanModifyDisplayStyle()`**
  — ask before setting, because a view driven by a template refuses and the refusal is not a failure.
- **A source and target folder that overlap will quietly reprocess their own output.** The sample checks
  for it by prefix, both directions, and that guard is worth having in any batch-folder job.

## State at the end

All 13 consistency checks pass. **The 3 new fragments and the 1 upgrade compile on Revit 2020, 2024 and
2027**, and the full library compiled clean on all three in the same session. **None of the 3 has been
run against a real model** — each is dry-run by default and says so in its own header. The family export
and the FBX export both WRITE FILES TO DISK; neither changes the model. `create-grid-series.cs` creates
real elements — run it once on an empty area first.
