# Live Model — Graphic override precedence (what beats what, in a Revit view)

> Part of the live-model knowledge set. Index: [`README.md`](README.md) — go back there to route to another topic.

When a color/graphic change made through a script (or by hand) doesn't seem to take effect, or a
different color shows up than expected, the usual cause is a STRONGER override sitting on top of the one
just changed. This is the reference for which mechanism wins.

## The hierarchy (strongest first)

The user supplied this 9-level list from their own working knowledge. Verified against what's
independently confirmable here — the core skeleton (rows 1, 2, 4, 7, 9) matches well-established,
widely-documented Revit behavior with high confidence. The three inserted rows (3, 5, 6) are real,
distinct Revit mechanisms, but their *exact* rank relative to their neighbors isn't something to assert
from memory alone — **verify live if a specific case doesn't behave as this order predicts**, per the
standing "verify, don't trust" rule in this knowledge set. Kept as given, not silently reordered.

| # | Mechanism | Confidence | Fragment(s) that touch this layer |
|---|---|---|---|
| 1 | **Linework Tool (LW)** | High — strongest, most specific, overrides individual edges directly | Not scripted here (a manual drafting tool, not exposed as a bulk API operation worth a fragment) |
| 2 | **Override Graphics in View \| By Element** (right-click override) | High | `color-graphics/action-set-color-uniform.cs`, `action-color-by-group.cs`, `action-highlight-vs-rest.cs`, `action-set-transparency.cs`, `action-reset-graphic-overrides.cs` |
| 3 | **Halftone / Underlay Settings** | Moderate — real mechanism (an underlay view is commonly forced to halftone regardless of most other overrides), exact rank vs. neighbors not independently confirmed here | Not yet scripted |
| 4 | **View Filters (V/G)** — rule-based overrides | High | `color-graphics/action-create-view-filter.cs`, `action-create-selection-filter.cs`, `action-apply-view-filter.cs`, `action-remove-view-filter.cs` |
| 5 | **Phase Graphic Overrides** (Existing, Demolished, etc.) | Moderate — real mechanism (Settings \| Phases \| Graphic Overrides tab), exact rank vs. neighbors not independently confirmed here | Not yet scripted |
| 6 | **View Depth** (elements beyond the cut plane showing as the `<Beyond>` line style) | Moderate — real, but narrower than the others: it governs which LINE STYLE draws beyond-cut-plane geometry, not a general color override, so it may not be a strict "rank" in the same stack so much as a parallel mechanism that only applies to that specific geometry class | Not yet scripted |
| 7 | **View Category Overrides (V/G)** — standard Model Category overrides | High | `color-graphics/action-set-category-color.cs`, `action-reset-category-graphics.cs` |
| 8 | **MEP System Graphic Overrides** (colors set inside the Duct/Pipe/Electrical System's own Properties) | Moderate-high — a real, distinct Revit feature (Properties palette \| Graphic Overrides, set per system instance), commonly understood to sit below Category overrides and above Object Styles | Not yet scripted |
| 9 | **Object Styles** — project-wide defaults | High — weakest, the fallback baseline everything else overrides | Not yet scripted (`Object Styles` is a document-wide setting, not a per-view/per-element script target) |

## A category override is SILENTLY PART-DISCARDED on a non-cuttable category

Not a precedence question — a "the write half-succeeded and said nothing" question, so it belongs with
the rest of this file. Measured live 2026-08-07 on Revit 2020.

`SetCategoryOverrides` accepts every setter without complaint, and the in-memory
`OverrideGraphicSettings` really does hold all of them. Read it back off the view afterwards and some
are gone. What survives depends on `Category.IsCuttable`:

| Category | `IsCuttable` | Projection line | Cut line | Surface fill |
|---|---|---|---|---|
| Walls, Doors | **true** | kept | kept | kept |
| Mechanical Equipment | false | kept | **discarded** | kept |
| Ducts, Pipes, Air Terminals | false | kept | **discarded** | **discarded** |

The same setters applied at the **element** level (`SetElementOverrides`) keep all of it on the very
same ducts — so this is a restriction on category overrides specifically, not on the values.

**The restriction is specific to CATEGORY overrides — the other two routes are unaffected.** Measured in
the same session, on the same non-cuttable Ducts category:

| Route | Cut line + surface fill on Ducts |
|---|---|
| `SetCategoryOverrides` | **discarded** |
| `SetElementOverrides` (`action-set-color-uniform.cs`) | kept |
| `SetFilterOverrides` (`action-apply-view-filter.cs`) | kept |

**What this means in practice:** "colour the ducts blue by category" gives you blue *lines* and no fill,
however solid it looked in the script. If the user wants ducts to read as solid colour in a shaded view,
use a **View Filter** (best for a rule that should keep applying to new ducts) or a per-**element**
override — not the category.

`action-set-category-color.cs` now reads the override back after writing it and reports exactly which
parts Revit discarded, instead of claiming the fill was applied.

## Practical use

- **"I set a category color but it's not showing on this element"** — check whether that element has a
  stronger per-element override (row 2) or is caught by a View Filter (row 4) with its own color.
  `action-report-graphic-overrides.cs` reads back current per-element overrides; there's no reverse
  lookup yet for "which View Filter(s) currently apply to this element" — a real potential gap if this
  comes up often.
- **"My View Filter isn't coloring what I expect"** — confirm the filter's category scope actually
  includes the element's category (`ParameterFilterElement.SetCategories`) and that no per-element
  override (row 2, stronger) is already sitting on those elements.
- **A new fragment overriding something in rows 3, 5, 6, or 8** hasn't been built yet — if one of these
  layers needs scripting (e.g. "set the MEP System's own graphic override color"), that's real, uncovered
  work, not something already handled elsewhere in this list.
