# Live Model — MEP Color Data Standard sync

> Part of the live-model knowledge set. Index: [`README.md`](README.md) — go back there to route to another topic.

## Syncing an external MEP Color Data Standard (Excel) into Duct/Pipe System Types, Materials, and View Filters
Full recipe from rolling out a project's own MEP color/material standards workbook — ask the user where
theirs lives and confirm its columns before starting; the worked example here used one row per system:
Discipline_Code, Service_Code, Type, System_Name, System Classification, Main_System_Code, Sub_System_Code,
System_Code, Abbreviation, System_Flow_Type, Element Type, TrueColor RGB, HEX, Color Name, Description.
Read the Excel with openpyxl (see the `xlsx` skill) — `data_only=True` for a static reference sheet like
this one, no recalc needed if it has no formulas.

**Match rule — always by Abbreviation, never by matching whole names.** A model's *existing* Duct/Pipe
System Type names (before renaming) often don't match the standard's `Type` column format at all (e.g.
model has `AC_Return Air Duct (RAD)`, the standard's Type column says `HVAC_AC_Return Air Duct
System_RAD`) — the only reliable join key is the abbreviation, extracted from the model name's trailing
`(XXX)` parenthetical (regex `\(([^)]+)\)\s*$`) for pre-rename names, or the trailing `_XXX` suffix once
already renamed to the standard. **Don't create new System Types for spreadsheet rows that don't exist in
the model** — only edit what's already there; ask before creating (confirmed in practice: several rows had
no corresponding model type and were correctly left uncreated rather than invented).

**Both `MechanicalSystemType` (ducts) and `PipingSystemType` (pipes) already carry a matching set of
custom project parameters** mirroring the Excel columns almost exactly: `Discipline_Code`, `Service_Code`,
`System_Name`, `Main_System_Code`, `Sub_System_Code`, `System_Code`, `Abbreviation`, `System_Flow_Type` —
all plain string parameters, `LookupParameter("Name")` finds each one directly, no `BuiltInParameter`
needed. Don't assume these are pre-filled correctly just because the duct ones happen to be — in this
project the duct types already had 6 of 7 correct (only `System_Name` was blank), but the pipe types had
ALL of them blank except `Main_System_Code` (which held the wrong value — Service_Code's value had leaked
into it). Read every column fresh per system type before assuming any of them are already right.

**Renaming the Type itself**: `mechanicalSystemType.Name = newName` / `pipingSystemType.Name = newName`
just works — no special API needed, same as any other named element.

**Description**: the plain Type Name AND its assigned Material both expose a native `Description` field
via `BuiltInParameter.ALL_MODEL_DESCRIPTION` (`get_Parameter`, not `LookupParameter` — it's built-in, not
custom). Convention settled on for this project: Description = the same text as `System_Name` (e.g.
"Return Air Duct System"), not custom prose — matches how the rest of this sync reuses existing column
text rather than inventing new content.

**Material class gotcha (2020 API)**: `Material` has NO `Keywords` property or parameter at all — confirmed
by reflecting on `typeof(Material).GetProperties(...)` and by `LookupParameter("Keywords")` returning
`null`. The Identity Data "Keywords" field visible in the Revit UI is not scriptable in this Revit
version. `Class` IS scriptable — it's `Material.MaterialClass` (a plain string property, not a Parameter).

**Bug found via cross-reference, not from names/tags (Modeler mindset case)**: two different duct system
types were found sharing the exact same `Material` `ElementId` — both types' `Material` parameter resolved
to the same material asset. Renaming that shared material to match either type would have mislabeled it
for the other. Caught by comparing every type's resolved Material *ElementId* side-by-side, not by trusting
each type's own name/label. Held that one back and asked the user rather than guessing which system
"really" owned it — they created a proper dedicated material themselves and confirmed. **General rule**:
before batch-renaming N types' assigned materials, first check for `ElementId` collisions across the
whole set — a rename that looks safe type-by-type can still clobber a second type's material.

**Verifying a Material's Graphics tab (Shading/Surface Pattern/Cut Pattern colors) programmatically**:
`Material.Color` (shading), `.SurfaceForegroundPatternColor`/`Id`, `.SurfaceBackgroundPatternColor`/`Id`,
`.CutForegroundPatternColor`/`Id`, `.CutBackgroundPatternColor`/`Id` — all plain properties, `.Red/.Green/.Blue`
on the `Color` struct. No `OverrideGraphicSettings` needed here (that's for a *view's* per-element or
per-filter override, not the material asset itself).

**View Filters (`ParameterFilterElement`) can be organized into folders using `/` inside the filter's own
`Name`** — e.g. `MEP_Duct_System Type/AC_Return Air Duct (RAD)` groups under a "MEP_Duct_System Type"
folder in the Filters dialog. When renaming these to match a new standard, split on the *last* `/`,
keep the prefix (folder) untouched, and only replace the suffix (the actual old type name) — same
abbreviation-matching technique as above.

**Reusable fragments now exist for the create/apply/remove lifecycle** — don't hand-write this pattern
fresh: [`action-create-view-filter.cs`](../../scripts/actions/color-graphics/action-create-view-filter.cs)
(builds the `ParameterFilterElement` + one `FilterRule` via `ParameterFilterRuleFactory`, not yet
live-verified), [`action-apply-view-filter.cs`](../../scripts/actions/color-graphics/action-apply-view-filter.cs)
(add to a view + set its override + visibility — mirrors the already-proven pattern below), and
[`action-remove-view-filter.cs`](../../scripts/actions/color-graphics/action-remove-view-filter.cs)
(detach from a view, optionally delete the definition entirely).

**Per-view filter graphic overrides need BOTH projection AND cut set explicitly — Revit does not
default one from the other.** Found live (2026-07-16): 26 filters already applied to a floor plan view
had a fully correct Projection Line Color + Surface Pattern (Foreground) Color, but `CutLineColor` and
`CutForegroundPatternColor` were both simply unset (`IsValid == false`) on every single one — only the
"looking down on it" portion of any duct/pipe would show the standard color; anything actually sliced by
the view's cut plane would fall back to default/black. Always check and set both:
```csharp
var ogs = view.GetFilterOverrides(filterId); // or `new OverrideGraphicSettings()` for a filter not yet in this view
ogs.SetProjectionLineColor(color);
ogs.SetCutLineColor(color);
ogs.SetSurfaceForegroundPatternColor(color); ogs.SetSurfaceForegroundPatternId(solidFillId); ogs.SetSurfaceForegroundPatternVisible(true);
ogs.SetCutForegroundPatternColor(color);     ogs.SetCutForegroundPatternId(solidFillId);     ogs.SetCutForegroundPatternVisible(true);
view.SetFilterOverrides(filterId, ogs);
```
**Adding an existing filter (with its rule already defined elsewhere) to a new view**: `view.AddFilter(filterId)`
then `view.SetFilterVisibility(filterId, true)` then `SetFilterOverrides` as above — the filter's own
element-matching rule is shared/reused automatically, only the per-view override + visibility needs setting.

**"Change this filter's color" is ambiguous between line-only and line+fill — don't default to both.**
Corrected live (project 4355, 2026-08-01): asked to "change filter color to black," applied
`SetProjectionLineColor` + `SetCutLineColor` + `SetSurfaceForegroundPatternColor`/`SetCutForegroundPatternColor`
together (matching `action-apply-view-filter.cs`'s `includeFill=true` default, which is right for a full MEP
Color Data Standard sync). The user had to undo it in Revit — for a plain "change the color" request the
fill/pattern is a much bigger visual change than they wanted and they hadn't asked for it. Re-applied with
only `SetProjectionLineColor`/`SetCutLineColor` touched, `SetSurfaceForegroundPatternColor`/`SetCutForegroundPatternColor`
left completely alone (don't even call those setters — reuse the existing `ogs` object so whatever fill
state is already there survives untouched). **Rule going forward: an ad-hoc "set this filter/element to
color X" request defaults to line color only; only touch fill/pattern when the user asks for fill, asks to
"match the standard," or the request is explicitly the full MEP Color Data Standard sync workflow above.**

**Verifying enum-based classification values against the real installed API, not memory**: for systems
that don't exist in the model yet (so there's nothing to read their real classification from), get the
authoritative list first — `Enum.GetNames(typeof(Autodesk.Revit.DB.MEPSystemClassification))` — rather
than guessing names. Revit 2020's real list: `UndefinedSystemClassification, SupplyAir, ReturnAir,
ExhaustAir, OtherAir, DataCircuit, PowerCircuit, SupplyHydronic, ReturnHydronic, Telephone, Security,
FireAlarm, NurseCall, Controls, Communication, CondensateDrain, Sanitary, Vent, Storm, DomesticHotWater,
DomesticColdWater, Recirculation, OtherPipe, FireProtectWet, FireProtectDry, FireProtectPreaction,
FireProtectOther, SwitchTopology, Fitting, Global, PowerBalanced, PowerUnBalanced, CableTrayConduit`. Note
the **UI display string is not just the enum name with spaces** — confirmed several genuinely differ
(`SupplyHydronic` displays as "Hydronic Supply", word order flipped; `OtherPipe` displays as just
"Other", drops a word) — read the display string from an existing real type's own `"System Classification"`
parameter (`AsString()`, it's a plain String-storage parameter) rather than hand-formatting the enum name.
There is **no dedicated classification for fuel gas or steam** in this enum — this project's own
precedent (Refrigerant Liquid/Vapor, which also has nothing dedicated) is to fall back to `Other`; steam
supply/return was confirmed via Autodesk's own published docs to conventionally use Hydronic
Supply/Return. Several fire-suppression types (Foam, Clean Agent, Water Mist, Deluge) have no dedicated
value either and are genuine judgment calls — Autodesk's docs confirm Water Mist/Clean Agent are meant to
be "Other"; Deluge has no authoritative single answer (candidates: Dry, Pre-Action, or Other) since it
behaves like both (empty until activated like Dry, but detection-triggered like Pre-Action).

**Classifying a view/template's filters as "mechanical" vs "electrical" — go by real category, not by
filter name.** `ParameterFilterElement.GetCategories()` returns the actual `ICollection<ElementId>` of
categories a filter targets; resolve each with `Category.GetCategory(doc, id).Name`. Filter *names* in a
project can be repurposed and stop describing what they actually filter — found live (project 4355,
template `Trg_Wip_Mech_Duct_Layout`): two filters named `..._Cable Trays_Service Type_Refrigerant Pipes
Tray` sound mechanical (refrigerant is an HVAC service) but their real categories are `Cable Trays` /
`Cable Tray Fittings` — Revit classifies Cable Tray under the **Electrical** discipline regardless of what
it's being used to carry. This project routes refrigerant-pipe support trays on an Electrical-category
element, so a name-only read of "mechanical filters on this template" would have missed that 2 of the 22
were actually Electrical-category. A catch-all filter (e.g. `TRG_Grayout_All`) that lists dozens of
categories spanning every discipline is neither mechanical nor electrical — it's a cross-discipline
utility filter; don't force it into either bucket.

**Site shaft-coordination sheet set — one sheet per system, built by duplicating a single "hero" template
three times.** Built live on project 4355 (2026-08-01): 3 sheets (Duct / Piping / Electrical Cable Tray),
each with the SAME 3 floor plans + 5 sections duplicated onto it (`views.md` § Duplicating a view template
— same `ElementTransformUtils.CopyElements` technique, since these are templates), positioned at identical
`Viewport.GetBoxCenter()` coordinates on every sheet so the sheets line up when flipped between. Each
sheet gets its own Layout+Section template pair (6 templates total), all duplicated from one already-tuned
source pair, then recolored per sheet with **one system full color (the "hero"), everything else pushed to
a single neutral gray (this project used RGB 80,80,80), line-color only** (see the line-vs-fill rule
above). **The key subtlety: group by what an element actually carries, not by its Revit category or which
sheet "owns" the category.** The 2 Cable Tray-category filters here are functionally refrigerant PIPE
trays (see the classification note above) — so on the Piping sheet they got the hero treatment alongside
real pipes, not gray; only on the Electrical sheet (where Cable Tray is the actual hero) do real ducts AND
real pipes both drop to gray, cable tray stays colored. Getting this grouping right required asking the
user to restate the 3x3 (sheet × system) color matrix explicitly rather than inferring it, then verifying
the live result back against their exact wording — a plausible-looking inferred scheme is not the same as
a confirmed one when 3 systems × multiple filters are in play.

**A "system's filters" is not just its System Type filters — Accessories/Insulation are separate filters
easy to forget.** Found live same day: after coloring the 4 duct System Type filters (EA/FA/RA/SA) to
80,80,80 on the piping/electrical templates, the user reported ducts still showing colored. Root cause:
`TRG_Accessories_Duct` and `TRG_Insulation_Duct` are TWO MORE filters matching duct-category elements,
never included in the "duct filters" pass — some Duct Access Door elements (family
`TCM_DAD_T002_DuctAccessDoor_Rectangular`) were still rendering via `TRG_Accessories_Duct`'s untouched
black override. Same pattern exists for `TRG_Accessories_Pipe`/`TRG_Insulation_Pipe`. **When a task says
"color/gray out the duct filters" (or pipe/tray), audit ALL filters whose category scope includes that
system's categories, not just the ones whose name says "System_Type."**

**Diagnostic pitfall: `ParameterFilterElement.GetElementFilter()` returns `null` for a filter with no
rule** (a pure category-scope filter, like `TRG_Accessories_Duct` — no condition, matches everything in
its category). Calling `.PassesFilter(...)` directly on that null throws `NullReferenceException` — if
wrapped in a bare `try { } catch { }` (as an early version of this diagnostic was), the exception is
silently swallowed and the element reads as "no filter matched," which is wrong: a null `ElementFilter`
means the category match is unconditional, i.e. it always passes. Guard explicitly:
`ef == null ? true : ef.PassesFilter(doc, elementId)` — do not let a bare catch hide this.

