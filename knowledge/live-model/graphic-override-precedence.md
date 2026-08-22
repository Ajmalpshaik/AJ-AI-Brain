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

**It applies to line WEIGHT too, not just colour and fill** (measured 2026-08-10). Writing
`SetCutLineWeight(1)` to all 84 controllable parent categories in one view: **projection weight held on 79,
cut weight held on only 29.** Every non-cuttable category — all of Ducts, Pipes, Air Terminals, Mechanical
Equipment, Sprinklers and the electrical families — read back `-1` (still "By View") in the cut slot while
accepting the projection slot. So "set the MEP line weight" can only ever mean the *projection* weight
by category override; there is no cut weight to set on things Revit never cuts.

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

### The full-view sweep — `IsCuttable` predicts the CUT half, not the fill (measured 2026-08-10)

One grey category override (RGB 150,150,150 lines + `<Solid fill>` on surface and cut) written to **all 51
non-MEP model categories** of one Revit 2020 floor plan, then every one read back off the view:

- **24 kept the whole override.** Spot-verified individually: Walls, Floors, Ceilings, Roofs, Doors,
  Windows, Columns, Structural Columns, Structural Framing, Casework, Generic Models, Stairs, Topography,
  Curtain Panels, Mass — all four slots read back `150,150,150`.
- **27 came back partial, in four distinct shapes:**

| What Revit silently discarded | Categories | What survived |
|---|---|---|
| **everything** | Areas, Point Clouds, Raster Images, Rooms, Spaces | nothing |
| cut line + cut fill | Detail Items, Entourage, Furniture, Furniture Systems, Parking, Planting, Shaft Openings, Specialty Equipment, Structural Rebar Couplers | projection line + surface fill |
| cut line + surface fill + cut fill | Imports in Families, Lines, MEP Fabrication Containment / Ductwork / Hangers / Pipework, Structural Beam Systems, Structural Trusses | projection line only |
| surface fill + cut fill — **despite `IsCuttable == true`** | Railings, Structural Area Reinforcement, Structural Fabric Areas, Structural Fabric Reinforcement, Structural Path Reinforcement, Structural Stiffeners | both lines |

**That last row corrects the table above.** "Walls, Doors, `IsCuttable == true` → kept, kept, kept" is
accurate but does not generalise: Railings is cuttable and still loses both fills. So `IsCuttable == false`
reliably predicts the *cut* half being dropped, but **nothing predicts the *fill* being dropped** — read the
override back off the view, never infer it from the flag.

Widening the same sweep to **all 85** controllable model categories (MEP included) in the same view gave
**24 fully applied, 61 partial** — and the extra 34 MEP categories land almost entirely in the
"projection line only" shape:

| MEP category | What survived |
|---|---|
| Ducts, Duct Fittings/Accessories/Insulations/Linings/Placeholders, Flex Ducts, Air Terminals, Pipes, Pipe Fittings/Accessories/Insulations/Placeholders, Flex Pipes, Sprinklers, Cable Trays + Fittings, Conduits + Fittings, Wires, and most electrical device categories | **projection line only** |
| Mechanical Equipment, Plumbing Fixtures, Electrical Equipment/Fixtures, Lighting Fixtures | projection line + surface fill |
| Spaces | **nothing** |

**Practical consequence for a whole-view grey-out** (the "grayout for MEP" job, see
[`../glossary.md`](../glossary.md)):

- The **architectural and structural background greys completely** — lines and solid fill, in projection
  and cut. That half of the job genuinely works by category override.
- **MEP greys as lines only.** Asking for "everything grey with solid fill" and running it by category
  override gives grey *linework* on the services with their fill untouched — not a failure of the script,
  a Revit restriction. If MEP must read as solid grey too, it has to go through a **View Filter** or a
  per-**element** override (both keep the fill, per the route table above), not the category.
- **Five categories take nothing at all**: Rooms, Areas and Spaces take their fill from a **Colour Scheme**
  (or their own Interior Fill sub-category) rather than a V/G category override, and Raster Images /
  Point Clouds are raster content with no line-and-fill model to override.

### A SUB-category override holds the line colour and almost nothing else (measured 2026-08-10)

Expanding a category's `+` in Visibility/Graphics exposes its sub-categories — Doors opens to Glass, Panel,
Frame/Mullion, Trim, Plan Swing, Ironmongery, Architrave, Opening, Structural Opening, Hidden Lines,
Elevation Swing, Moulding/Architrave — and each gets its own override row. Writing the *same* full override
(line colour + `<Solid fill>` foreground pattern + transparency) to all **255** controllable sub-categories
of one Revit 2020 floor plan, then reading every one back:

| Slot written | Actually held |
|---|---|
| Projection line colour | **225 of 255** |
| Surface foreground pattern | **22 of 255** |
| Transparency | **0 of 255** |

- **30 sub-categories refuse even the line colour.** They are the ones describing a graphic *layer* rather
  than geometry — `Walls > Common Edges`, `Walls > Surface Pattern`, `Walls > Cut Pattern`.
- **Transparency never sticks on a sub-category** — not once in 255 attempts. It has to go on the parent.
- **The V/G dialog says the same thing.** On the expanded Doors rows the Patterns and Transparency columns
  are **greyed out** while the Lines columns stay editable. So this is not the API failing silently; it is
  the API matching a restriction the UI already shows. Worth checking the dialog before assuming a bug.

**Line weight splits the same way** (measured 2026-08-10, setting Walls projection + cut to pen 2). The
geometry sub-categories took both — `Wall Sweep - Cornice` and `Hidden Lines` read back 2/2. The graphic-
layer ones did not: `Surface Pattern` and `Cut Pattern` refused **both** slots, and `Common Edges` took the
cut weight but refused the projection weight. Same three sub-categories that refused a line colour, so
"describes a graphic layer rather than geometry" is the reliable predictor for both properties.

**Practical rule:** put **fill and transparency on the parent category**, and use sub-categories only to
carry the **line colour and line weight**. Attempting fill/transparency at sub-category level is wasted
work that reads back as success in the in-memory object and as nothing on the view.

## Line weights — the project table is NOT reachable from the API (checked 2026-08-10)

Two different things get called "line weight settings", and only one is scriptable:

| | Where | Scope | API |
|---|---|---|---|
| **The pen table** | Manage → Additional Settings → **Line Weights** (tabs: Model / Perspective / Annotation, pens 1–16, one column per view scale) | **Whole project, every view and sheet** | **Not exposed.** A long-standing known gap (Autodesk ref CF-3772) with an open request to surface it to the API. Do not go looking for a class — there isn't one. Ask the user to screenshot the dialog instead; that worked well for V/G. |
| **Which pen a category uses** | Object Styles, and the Weight column in V/G | project-wide (Object Styles) / per view (V/G) | Readable and writable: `Category.GetLineWeight(GraphicsStyleType.Projection\|Cut)`, and `OverrideGraphicSettings.SetProjectionLineWeight()` / `SetCutLineWeight()`. `-1` on a V/G override means "By View" — no override set. |

**ISO 128-2:2020 defines nine line weights** — 0.13, 0.18, 0.25, 0.35, 0.5, 0.7, 1.0, 1.4, 2.0 mm. A first
pass at this note said Revit's metric table "maps pens 1–9 to exactly those"; **a real table, read off the
dialog on 2026-08-10, shows that is too neat.** The mapping *slides with the scale column* — the same pen
is a different weight at 1:50 and 1:100, and the ISO ladder starts at a different pen in each column:

| Pen | 1:10 | 1:20 | 1:50 | 1:100 | 1:200 | 1:500 |
|---|---|---|---|---|---|---|
| 1 | 0.18 | 0.18 | 0.18 | **0.10** | **0.10** | **0.10** |
| 2 | 0.25 | 0.25 | 0.25 | 0.18 | **0.10** | **0.10** |
| 3 | 0.35 | 0.35 | 0.35 | 0.25 | 0.18 | **0.10** |
| 4 | **0.70** | 0.50 | 0.50 | 0.35 | 0.25 | 0.18 |
| 5 | 1.00 | 0.70 | 0.70 | 0.50 | 0.35 | 0.25 |
| 6–9 | 1.40 / 2.00 / 2.80 / 4.00 | 1.00 / 1.40 / 2.00 / 2.80 | 1.00 / 1.40 / 2.00 / 2.80 | 0.70 / 1.00 / 1.40 / 2.00 | 0.50 / 0.70 / 1.00 / 1.40 | 0.35 / 0.50 / 0.70 / 1.00 |

What that table actually says, and what to check on any project's own copy:

- **The ladder is genuinely ISO.** At 1:100, pens 2–9 are 0.18 / 0.25 / 0.35 / 0.5 / 0.7 / 1.0 / 1.4 / 2.0 —
  the ISO series exactly. Only **pen 1 is 0.10, below ISO's 0.13 minimum**, deliberately reserved as
  "thinnest possible". Cross-checked against published Revit defaults at pen 5 / 1:100 = 0.50 mm: matches,
  so this looks like the untouched metric default rather than an office edit.
- **Duplicate cells silently collapse pens at small scales.** At 1:200 pens 1–2 are both 0.10; at 1:500 pens
  1–3 are all 0.10. Three "different" weights plot identically — and that is exactly the thin end a
  greyed background lives at, so a grey-out that reads well at 1:100 can flatten at 1:500.
- **1:20 and 1:50 are identical columns**, and the **1:10 column skips 0.50** (pen 3 = 0.35 → pen 4 = 0.70).
- Editing the table is a **whole-project, every-sheet, every-scale** decision, never a per-drawing tweak —
  and there is no API to put it back (see above), so it is a hand-edit with no undo path from a script.

**For a grey-out specifically, the pen table is the wrong lever** — thinning the background there thins it
on the architectural sheets too. Use the **V/G per-category weight override**, which is per view and
reversible, and then save the whole thing as a **View Template** so it is repeatable rather than re-run by
hand. Measured on the test project at 1:100: MEP already outweighs the background out of the box (Ducts
and Pipes on pen 5, Air Terminals and Mechanical Equipment on 4, Cable Trays 3) against Walls 1/3,
Floors 1/4, Doors 1/2, Windows 1/3, Furniture 1 — so a grey-out mostly needs the background pushed *down*,
not the services pushed up.

## A line STYLE cannot be assigned in V/G — unpack it into its three ingredients (2026-08-10)

When the user names something like **`MEP_Hidden_Short_Black`**, that is a **Line Style** — a sub-category
of `OST_Lines`, listed under Manage → Additional Settings → Line Styles, carrying **colour + weight +
line pattern** as a bundle. The **Pattern** field in a V/G category override takes a **`LinePatternElement`
only**. There is no API (and no UI) route to point a category override at a line style, and a search of the
document's `LinePatternElement`s for that name returns nothing — the pattern is called something else.

**The fix is to unpack the style and set its three parts as the override:**

```csharp
// find the style among the sub-categories of OST_Lines
var linesCat = Document.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
Category style = null;
foreach (Category sc in linesCat.SubCategories) if (sc.Name == "MEP_Hidden_Short_Black") style = sc;

var colour    = style.LineColor;                                   // 0,0,0
var weight    = style.GetLineWeight(GraphicsStyleType.Projection);  // 3
var patternId = style.GetLinePatternId(GraphicsStyleType.Projection); // -> 'MEP_Hidden_Short_Dash'
```

Worked example from the user's own standard — note the names do **not** match, which is exactly the trap:

| He said | What it is | Unpacks to |
|---|---|---|
| `MEP_Hidden_Short_Black` | Line **Style** | colour `0,0,0` · weight `3` · pattern **`MEP_Hidden_Short_Dash`** |

His office library follows this shape throughout: one pattern (`MEP_Hidden_Short_Dash`) is shared by five
styles that differ only in colour — `..._Black`, `_Blue`, `_Green`, `_Red`, `_Orange`, all weight 3. So
**a colour word at the end of the name is the signal that it is a style, not a pattern.** Do not report
"pattern not found" and stop; look in the line styles before concluding anything is missing.

**The cut slot still refuses**, same non-cuttable rule as everywhere else on this page: applying this to
Duct/Pipe Insulations, the projection pattern and weight held and the cut pattern and weight both read back
empty.

## Writing an OverrideGraphicSettings correctly — the sentinels and the copy

Harvested 2026-08-22 from the add-in's Graphics Tools, which builds the full-fidelity object. These are
API facts this Brain had **no record of anywhere**, and each one produces a wrong-but-plausible result
rather than an error.

**"No override" is a specific value, not zero and not blank.** Every property has a sentinel meaning
*leave this alone*:

| Property | "no override" is | NOT |
|---|---|---|
| any colour | `Color.InvalidColorValue` | black, or `null` |
| line weight | `OverrideGraphicSettings.InvalidPenNumber` | `0` |
| a pattern | `ElementId.InvalidElementId` | `null` |

Write `0` as a line weight and you have not cleared the override — you have asked for something invalid.
Valid line weights are **1–16**; clamp above, and send anything else to `InvalidPenNumber`.

**A pattern id and its visibility flag are two separate writes.** `SetSurfaceForegroundPatternId(id)`
alone is not enough — pair it with `SetSurfaceForegroundPatternVisible(...)`, set from whether the id is
valid. The same goes for the background and both cut patterns. Set the id and forget the flag and the
pattern is there but not drawn, which reads on screen as "the override didn't work".

**Transparency is 0–100**, an integer percentage. Clamp it; out-of-range is not rejected loudly.

**`new OverrideGraphicSettings(existing)` is a copy constructor** — the correct way to duplicate an
override from one element or category to another. Do **not** read the source's properties one at a time
and re-set them: every property you forget becomes "no override" on the target, so you get a partial
copy that looks nearly right. This is what
[`../../scripts/actions/color-graphics/action-match-graphics.cs`](../../scripts/actions/color-graphics/action-match-graphics.cs)
uses.

**`view.IsCategoryOverridable(categoryId)` is the real test** for whether a category will accept an
override in a given view. Checking `CategoryType` is not enough, and `SetCategoryOverrides` on a
category that refuses throws. Note this is a *different* question from the one in the section below —
that one is about a category that accepts the call and then keeps only some of what you sent.

## Filter versus filter — order inside one view decides which colour wins

The table above ranks View Filters against the *other* mechanisms. It says nothing about **two filters
that both catch the same element**, which is the case that actually bites. Harvested 2026-08-22 from the
add-in's Filter Pro, which manages this deliberately.

**Order matters, and Revit gives you no reorder API.** In the Visibility/Graphics Filters tab the list
has an order, and for a property two filters both set, the one **higher in the list wins**. The only way
to change it:

1. **Capture** every existing filter's overrides (`view.GetFilterOverrides(id)`) and visibility
   (`view.GetFilterVisibility(id)`) — you are about to lose them.
2. **Remove them all** (`view.RemoveFilter(id)`).
3. **Re-add in the order you want** (`view.AddFilter(id)`), then **restore** each one's captured
   overrides and visibility.

Skipping step 1 is how a "reorder" quietly resets every filter in the view to default graphics. There is
no partial version of this — removing a filter drops its settings with it.

**`View.GetFilters()` order is not guaranteed on Revit 2020.** If order matters across several
operations, keep your own remembered list per view rather than re-reading it and trusting the sequence.

**Compare filter ids by VALUE, never by reference.** `View.GetFilters()` can hand back **new `ElementId`
wrapper objects** that are not reference-equal even when they point at the same filter. A
`List.Contains(id)` or an `==` against a stored id can therefore say "not there" about a filter that is
plainly there — and the code then adds it twice or fails to find it to remove. Compare the underlying
value.

## A view template blocks filter changes, and does it silently

`view.AddFilter` / `RemoveFilter` / `SetFilterOverrides` on a view that has a **view template applied**
do not do what you asked. The test is simply:

```
view.ViewTemplateId != ElementId.InvalidElementId    // and the view is not itself a template
```

Check it **before** the transaction and report the view as skipped by name. This is the same family of
trap as the scope box on a crop box (see
[`../../scripts/actions/visibility/action-set-view-crop.cs`](../../scripts/actions/visibility/action-set-view-crop.cs)):
**a template or a governing element accepts the write and then decides the value anyway.** Neither
throws. If a graphic change "worked" but nothing looks different, this is the first thing to check.

To change it for real, either edit the template, or detach the template from the view first.

## Practical use

- **"I set a category color but it's not showing on this element"** — check whether that element has a
  stronger per-element override (row 2) or is caught by a View Filter (row 4) with its own color.
  `action-report-graphic-overrides.cs` reads back current per-element overrides; there's no reverse
  lookup yet for "which View Filter(s) currently apply to this element" — a real potential gap if this
  comes up often.
- **"My View Filter isn't coloring what I expect"** — confirm the filter's category scope actually
  includes the element's category (`ParameterFilterElement.SetCategories`), that no per-element
  override (row 2, stronger) is already sitting on those elements, that **no other filter above it in
  the view's own list** sets the same property, and that **no view template is applied** — see the two
  sections above, both of which fail silently.
- **A new fragment overriding something in rows 3, 5, 6, or 8** hasn't been built yet — if one of these
  layers needs scripting (e.g. "set the MEP System's own graphic override color"), that's real, uncovered
  work, not something already handled elsewhere in this list.
