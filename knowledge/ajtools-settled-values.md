# The settled values — Ajmal's own numbers, from his tools' own settings

Back to [`INDEX.md`](INDEX.md).

Every number here is the **shipped default of one of Ajmal's own AJ Tools commands**. They were decided
once, on real work, and they are what his tools do when nobody changes anything. Harvested 2026-08-22
from the `*Settings` classes.

**How to use this — and it is not "apply these".** Rule 3 of [`../START-HERE.md`](../START-HERE.md)
still holds: *every number is a per-request input, never a default.* What this file changes is the
**quality of the question**. Instead of asking "what clearance do you want?" from nothing, say:

> *"Your MEP Openings tool uses a 25 mm cutout buffer on ducts and merges crossings within 100 mm —
> same here, or different on this job?"*

That is a question he can answer in one word. **Restate the number, name where it comes from, and get a
yes** — never silently apply it.

**These are his office's values, not a code.** Where a real code or standard governs (NFPA for
sprinklers, a project spec for velocity limits) the code wins and these are just the starting point.

---

## Tagging

The largest cluster, and the one most worth having, because tag work is full of small constants.

| Setting | Value | Where |
|---|---|---|
| Tag spacing (stacking / arranging) | **12 mm** paper | `TagArrangeSettings` |
| Tag offset from the element | **300 mm** | Smart MEP Tag, Create Tags |
| Minimum run length worth tagging | **1000 mm** | Smart MEP Tag, Create Tags |
| Filter by size before tagging | **on** | Smart MEP Tag |
| Minimum width to tag | **100 mm** | Smart MEP Tag |
| Minimum height to tag | **0 mm** (i.e. no height filter) | Smart MEP Tag |
| Use a leader | **on** | Smart MEP Tag |

**Clash fixing** (`TagClashSettings`):

| Setting | Value | Range allowed |
|---|---|---|
| Fix passes | **5** | 1–50 |
| Maximum drift a tag may be pushed | **50 mm** | 1–500 |
| Clash tolerance | **1.5 mm** | — |
| Minimum gap between tags | **5 mm** | — |
| Mark failures in the view | **on** | — |
| Skip vertical runs | **on** | — |
| Full search | **on** | — |

**The two worth noticing.** A **50 mm maximum drift** says a tag that cannot be fixed within 50 mm
should be *reported*, not dragged across the drawing — the tool would rather fail visibly. And
**5 passes** is the answer to "how many times do you re-run the resolver": this Brain's own resolver
used 15 and converged in 3, so 5 is a reasonable middle.

Used by: [`live-model/tagging.md`](live-model/tagging.md),
[`../scripts/actions/sheets-views/action-stack-tags.cs`](../scripts/actions/sheets-views/action-stack-tags.cs),
[`../scripts/recipes/tag-elements-in-active-view.cs`](../scripts/recipes/tag-elements-in-active-view.cs).

## MEP openings / sleeves

| Setting | Value |
|---|---|
| Merge nearby crossings within | **100 mm** |
| Cutout buffer — **pipe** | **20 mm**, shape **circle** |
| Cutout buffer — **duct** | **25 mm**, shape **rectangle** |
| Cutout buffer — **cable tray** | **25 mm**, shape **rectangle** |
| Include insulation in the size | **on** |
| Sources / hosts | current model **on**, linked **off** by default |

**Round for pipes, rectangular for ducts and trays** is a real drafting decision, not an accident — and
**insulation is included in the opening size**, which the Brain's own
[`../scripts/recipes/create-mep-openings.cs`](../scripts/recipes/create-mep-openings.cs) currently does
**not** do (it measures the bare service). Worth raising on any job that uses insulated ducts.

## Connecting MEP runs

| Setting | Value |
|---|---|
| Bend angle | **90°**, allowed range **5–90°** |
| Fallback order when the chosen angle will not build | **45, 30, 60, 90** |
| Copy insulation and lining onto new pieces | **on** |
| Copy workset | **on** |
| Auto transition on a size mismatch | **on** |
| Allow non-parallel ends | **on** |

Used by [`live-model/mep-connect-existing-runs.md`](live-model/mep-connect-existing-runs.md).

## Dimensioning

**Grids and levels** — note these are **paper millimetres**, so they scale with the view:

| Setting | Value |
|---|---|
| First row offset | **8 mm** paper (allowed 0–200) |
| Spacing between dimension rows | **6 mm** paper |
| Create individual + overall rows | both **on** |
| Skip the overall row when there are only two datums | **on** |
| Story levels only | **off** |

**MEP runs:**

| Setting | Value |
|---|---|
| Minimum run length worth dimensioning | **1000 mm** (same as tagging) |
| Skip vertical runs | **on** |
| Row spacing | **8 mm** paper |
| Padding | **6 mm** paper |
| Search band either side of the measuring line | **150 mm** |
| Chain style | single string |
| Dimension both sides | **off** |
| Include the run's width | **off** |
| Skip runs already dimensioned | **on** |

Used by [`live-model/dimensioning.md`](live-model/dimensioning.md).

## View crop

| Setting | Value |
|---|---|
| Margin around the content | **300 mm** |
| Annotation crop offset | **100 mm** |
| Include Revit links | **on** |
| Ignore hidden categories | **on** |
| Rectangular crop only | **on** |
| Include datums (grids/levels) | **OFF** |
| Apply annotation crop | **off** |
| Include coordination models | **off** |

**Datums off is the important one.** Grids and levels extend far past the building, so including them
in a crop-to-content pass gives a crop the size of the site. The Brain's
[`../scripts/actions/visibility/action-set-view-crop.cs`](../scripts/actions/visibility/action-set-view-crop.cs)
takes whatever `elements` it is handed — so **filter grids and levels out before calling it**, or expect
the same problem.

## Duct sheet-metal allowances

Fabrication uplift over bare sheet weight — full working in
[`duct-sheet-metal-takeoff.md`](duct-sheet-metal-takeoff.md):

| Allowance | Value |
|---|---|
| Seam | 3% |
| Joint | 2% |
| Flange | 4% |
| Fittings | 10% |
| Wastage | 5% |
| Reinforcement (only where the gauge band requires it) | 5% |

Totals **+24%** unreinforced, **+29%** reinforced. Write the rule source onto the element: **on**.

## Odds and ends

| Setting | Value | Tool |
|---|---|---|
| Revision cloud offset from the elements | **50 mm** | Revision Clouds by Elements |
| Default workset for links and CAD imports | **"Linked Models"** | Link Workset |
| Section-mark pass applies to the active view only | **on** | Section Mark Visibility |
| Keep all placed sections / unhide all | both **off** | Section Mark Visibility |

## Where these disagree with the Brain's own fragments

Two, both flagged above rather than silently reconciled:

1. **Openings include insulation** in his tool; `create-mep-openings.cs` measures the bare service. Ask
   which is wanted before cutting.
2. **View crop excludes datums** in his tool; `action-set-view-crop.cs` crops to whatever it is given.
   Filter first.

Neither is a bug in either place — they are different defaults for the same job, and the point of
writing them down is that the difference is now visible instead of surprising.
