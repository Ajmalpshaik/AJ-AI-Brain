# Live Model — Building parametric families (Family Editor)

> Part of the live-model knowledge set. Index: [`README.md`](README.md) — go back there to route to another topic.

> **split-review: kept whole** (reviewed 2026-08-13, at 456 lines). This file is past the ~300-line rule
> and stays that way on purpose. It is not four topics — it is **one method plus a chronological record of
> the four builds that produced it**, and each build is written as what the previous one got wrong: the
> second corrects the first's connector assumptions, the third documents a `ReplaceParameter` rollback that
> corrupts the family, the fourth proves pipe and electrical connectors against a real manufacturer
> datasheet. Split by build and the corrections lose the thing they correct; split method from builds and
> the method loses its evidence. The earlier `core.md` split worked because the viewport material was a
> genuinely different subject — there is no such seam here. **Re-open this decision only if a fifth build
> adds a section that stands alone.**

## Building a parametric family from scratch (Family Editor, via the bridge)
First done 2026-07-16: built a square ceiling air terminal (Generic Model template → category switched
to Air Terminals) with a fully parametric box body, a rectangular duct neck, and a working duct
connector — entirely via `run_csharp` against the open family document (the bridge works the same way
against a family document as a project document; `Document.IsFamilyDocument` confirms which). See
`scripts/recipes/create-parametric-box-family-with-duct-connector.cs` for the working, reusable
version of everything below.

**Switching a family's category**: `Document.OwnerFamily.FamilyCategory = Document.Settings.Categories
.get_Item(BuiltInCategory.OST_DuctTerminal)` inside a `Transaction` — `OST_DuctTerminal` is the
BuiltInCategory for the "Air Terminals" display name. Switching category immediately adds that
category's standard built-in type parameters (Max Flow, Min Flow, Cost, Description, Manufacturer, etc.)
with no extra step needed.

**`ReferencePlane`'s 3rd constructor argument is a direction vector, not a point.**
`Document.FamilyCreate.NewReferencePlane(XYZ bubbleEnd, XYZ freeEnd, XYZ cutVec, View view)` — passing
an actual coordinate (e.g. offset to match the line's X position) for `cutVec` instead of a pure unit
vector (`XYZ.BasisZ`) produces a visibly tilted plane (`Normal` comes out non-axis-aligned, e.g.
`(-0.95, 0, -0.31)` instead of `(1,0,0)`) — caught by reading `plane.GetPlane().Normal` back immediately
after creation, which is now the standard verification step for any new reference plane. Always pass a
plain unit vector (`XYZ.BasisZ` worked for both the X-normal and Y-normal planes needed here).

**Making extruded geometry actually track a reference plane requires an explicit `NewAlignment`, not just
coincident coordinates at creation time.** Sketching a profile at the same XYZ coordinates as a reference
plane does NOT bind them — changing the plane's position later (via a labeled dimension) leaves the
geometry exactly where it was. The real link: get the solid's planar side `Face` (via
`extrusion.get_Geometry(new Options{ComputeReferences=true})`, matched by `PlanarFace.FaceNormal`,
`.Reference` on the match), then `Document.FamilyCreate.NewAlignment(view, referencePlane.GetReference(),
face.Reference)` inside a `Transaction`. `view` must be a 2D view (a `ViewPlan`, e.g. the template's own
"Ref. Level" floor plan) — a 3D view doesn't work for `NewAlignment`/`NewDimension`.

**Symmetric parametric resize about a center reference plane needs an EQ dimension chain, not just a
labeled overall dimension.** A single 2-reference dimension between Left and Right planes, labeled to a
Length parameter, does resize on parameter change — but Revit's regen has no reason to keep it centered
(one plane can end up doing all the moving, drifting the whole solid off the family's own insertion
origin). The correct pattern (confirmed working, resize test showed the box staying exactly centered on
origin through repeated non-square parameter changes):
1. A 3-reference `Dimension` (Left plane, the template's own `Center (Left/Right)` plane, Right plane) —
   `dimension.AreSegmentsEqual = true` forces the two segments equal, keeping Left/Right symmetric about
   Center automatically.
2. A separate 2-reference `Dimension` (Left, Right) on a different offset dimension line, with
   `dimension.FamilyLabel = lengthParam` — this is what actually drives the overall size from the
   parameter; combined with the EQ constraint above, Revit resolves both planes moving symmetrically.
3. Repeat for the other axis using the template's `Center (Front/Back)` plane and a Width parameter.
Both `NewDimension` calls need a `Line` argument purely for where the annotation draws — offset it well
outside the geometry's own footprint (e.g. body-half-extent + 150mm/300mm) so dimension lines for
different axes/parts (body vs. neck) don't overlap and become unreadable.

**Driving extrusion depth (or any element's own double parameter) from a family parameter is simpler
than geometry dimensioning — use `FamilyManager.AssociateElementParameterToFamilyParameter`, not a
dimension at all.** `extrusion.get_Parameter(BuiltInParameter.EXTRUSION_END_PARAM)` gives the element's
own depth parameter; `familyManager.AssociateElementParameterToFamilyParameter(thatParam, familyParam)`
inside a `Transaction` links them permanently — changing the family parameter's value regenerates the
extrusion's depth directly, no reference planes or dimensions involved. Same technique works for a duct
connector's own `CONNECTOR_WIDTH`/`CONNECTOR_HEIGHT` built-in parameters (see below) — any writable
element `Parameter` can be associated this way, it isn't specific to extrusions.

**A second solid extrusion stacked in Z doesn't need its own sketch plane positioned at the first
extrusion's top — just extend its own End value past the first one's, on the SAME base sketch plane
(Z=0).** For the neck sitting on top of the body: rather than working out where to place a new
`SketchPlane` at "the body's current top" (awkward to keep parametric, since that height itself is a
parameter), the neck extrusion sketches on the exact same Z=0 reference plane as the body, and its own
End value is bound to a formula parameter `"Neck Top" = Height + Neck Depth` (see next point) — so its
total Z-extent is 0→(Height+NeckDepth), which overlaps the body's 0→Height a little at the bottom. This
overlap is invisible (interior solid mass fully inside the already-solid body) since **multiple `Solid`
extrusion forms in one family are unioned automatically** — no explicit boolean/join step needed, unlike
adding a *Void* form (which does need an explicit cut).

**Family parameter formulas: spaced parameter names do NOT need quoting — quoting them is what breaks
the formula.** Tried `Height + "Neck Depth"` and `"Neck Depth" + Height` first (guessing at Revit's
usual spaced-name convention) — both throw `"It is an invalid formula string."` with an unhelpful
generic message. The bare, unquoted name works fine: `fm.SetFormula(param, "Height + Neck Depth")`.
Confirmed by isolating single-parameter formulas first (`"Height"` alone worked; `"Neck Depth"` alone
without quotes worked; `"\"Neck Depth\""` with quotes failed) before combining — worth doing that
isolation step again for any future formula that keeps failing with this same generic error, rather than
guessing at syntax variations blind.

**`ConnectorElement.CreateDuctConnector` exists on the static class, not `FamilyItemFactory`/
`Document.FamilyCreate` where the "New..." naming pattern would suggest.** Real signature (discovered
live via a deliberate wrong-arg compile error, not memory — see method below):
`Autodesk.Revit.DB.ConnectorElement.CreateDuctConnector(Document document, DuctSystemType systemType,
ConnectorProfileType profileType, Reference planarFace)` — `planarFace` must be a `Reference` to an
existing planar `Face` on the family's own solid geometry (e.g. the neck stub's outward-facing end); the
connector is created exactly on that face, position and orientation inherited from it (origin = face
center, direction = face normal). Must run inside a `Transaction`.

**Discovering an unknown Revit API method signature without guessing from memory — deliberately trigger
a compile error and read it, don't reflect.** `Document.FamilyCreate.NewDuctConnector()` and
`.NewConnector()` both don't exist (`CS1061`) — that ruled out the `FamilyItemFactory` guess entirely.
`ConnectorElement.CreateDuctConnector()` with zero args gave `CS1501` ("no overload... takes 0
arguments") — confirming the method exists, just needs args. Passing a bogus named argument
(`bogusName: Document`) gave `CS1739` naming the *actual* best-matching overload, but not full param
names. What actually nailed the exact signature: calling it with a plausible arg list and typed nulls
where a `Reference` was expected — the call **compiled successfully** and instead threw a runtime
`ArgumentNullException` naming the real parameter (`"planarFace"`) — compiling at all confirms the
signature is right, even though the call itself fails at runtime for being null. This is a reliable,
fast, three-step technique for verified (not memorized) API discovery via the bridge, worth reusing
whenever a method name/signature is genuinely unknown: (1) zero-arg call to confirm existence vs. typo,
(2) bogus named-arg call to get Roslyn's best-overload guess, (3) plausible-typed-args-with-null call —
compiling confirms the shape, the runtime exception's parameter name confirms the last unclear detail.

**A newly created `ConnectorElement`'s Width/Height do NOT inherit the planar face's actual size —
they default to a generic 1 foot (304.8mm) placeholder, exactly the same class of bug as `Duct.Create`
not inheriting its source connector's size (see the "Drawing a duct" section above).** Confirmed by
reading `conn.Width`/`conn.Height` right after `CreateDuctConnector` against a 200×200mm face — came
back 304.8×304.8mm. Fix: `conn.Width`/`.Height` are themselves **read-only properties** (same
`MEPCurve.Width` pattern) — set the real parameters instead, `conn.get_Parameter(BuiltInParameter
.CONNECTOR_WIDTH)` / `CONNECTOR_HEIGHT`, and for a genuinely parametric connector (not just a one-time
fix), associate those two parameters to the family's own Neck Width/Neck Height parameters via
`AssociateElementParameterToFamilyParameter` — same technique as the extrusion end above — so resizing
the neck automatically keeps the connector's port size in sync, verified by the full resize test below.

**`Document.Regenerate()` outside an open `Transaction` throws "Modification of the document is
forbidden... no open transaction" in a family document — AND, critically, an unhandled exception
anywhere later in the same `run_csharp` call rolls back every change made earlier in that call, even
changes from `Transaction`s that already called `.Commit()`.** First extrusion-creation attempt
committed its transaction successfully, then called a bare `Document.Regenerate()` afterward "just to be
safe" — that line threw, and the *entire* extrusion silently vanished (`FilteredElementCollector` found
nothing on the next call) even though the commit had already happened moments earlier within the same
script. Lesson: never add a speculative `Regenerate()`/verification line after a transaction commits
within the same call unless it's also wrapped in its own transaction (Commit() already regenerates on
its own) — and more generally, treat every `run_csharp` call as all-or-nothing: if anything after a
commit can throw, the commit's effects are not actually safe until the whole call returns successfully.

**Full worked resize test (the actual verification, not just "it compiled")**: after wiring up all of
the above, changed Length/Width/Height/Neck Width/Neck Height/Neck Depth to six different non-square
values in one transaction, regenerated, and read back both extrusions' bounding boxes plus the
connector's origin/width/height — every single value matched the new parameters exactly, box stayed
centered on origin, neck stayed centered on the box, connector tracked the neck's new top Z and new
W/H. This is the standard of proof to hold future family-authoring work to — "the API calls didn't
error" is not sufficient, a real parameter change + geometry read-back is.

### Second build (2026-07-16, electric motor Cooling Bar sub-family) — new findings

Building a nested face-based "cooling fin" sub-family (part of a larger electric-motor family with
6 nested sub-families) surfaced several NEW gotchas beyond the air terminal build above, plus one still
genuinely **unresolved** problem (the void-cut issue below) — read this whole subsection before
attempting another multi-solid family with void cuts, since the last item is a real, currently-unsolved
blocker, not just a solved gotcha.

**`FamilyManager.SetFormula` throws `"There is no valid family type."` if called before ANY family type
exists yet — even though `AddParameter` itself works fine with no type present.** Original order was
`AddParameter` ×N → `SetFormula` on one of them → `NewType` + `Set` values. The `SetFormula` call is
what threw, rolling back the whole transaction (including the 5 just-added parameters — same
all-or-nothing lesson as the `Regenerate()` gotcha above). Fix: always call `fm.NewType(...)` and
`fm.CurrentType = ft` **before** the first `SetFormula` call, even if the type's actual values get set
afterward — `SetFormula` needs a live current type to exist, `AddParameter` does not.

**Extrusion face `.Reference` is only reliably populated when the sketch plane is HORIZONTAL (Z-normal)
— a VERTICAL sketch plane (e.g. the template's own Y-normal "Center (Front/Back)" plane) gives a
reference on only ONE face out of six, silently.** Built a fin-bar profile on "Center (Front/Back)"
(normal `(0,-1,0)`) exactly the same way as every horizontal-plane extrusion before it — the solid came
out geometrically correct (right bounding box) but `get_Geometry(new Options{ComputeReferences=true})`
returned `Reference == null` for 5 of 6 `PlanarFace`s (only the one with normal `(0,0,1)` had a
reference). Confirmed this is about **sketch plane orientation, not the face-based document type**: a
second test extrusion built in the *same* document on the horizontal "Reference Plane" got all 6 face
references back correctly, and switching the `Options.View` between the floor plan, both elevations, and
the 3D view made no difference at all (ruled out view-dependence as the cause). **Fix: always sketch
extrusion profiles on a horizontal (Z-normal) plane, then choose which world axis is "length" vs
"width" vs "depth" by how you orient the RECTANGLE within that flat profile, rather than standing the
sketch plane itself up vertically** — e.g. for a fin that needs to stick radially outward once
face-hosted, draw the width×length footprint flat in X/Y and let Z (the extrude direction) be the radial
"sticking-out" depth, instead of drawing a width×height cross-section on a vertical plane and extruding
along length. This produces the same physical shape and is fully reference-safe. If a solid extrusion's
faces will ever need `NewAlignment`/`NewDimension` locking (i.e. anything beyond a one-off void cut),
build it on a horizontal sketch plane — full stop, don't risk the vertical-plane path even if it seems
more "natural" for the shape being modeled.

**UNRESOLVED as of 2026-07-16: void-form cuts (`NewExtrusion(isSolid: false, ...)`) do not appear to
actually remove material from an intersecting solid, checked five different ways, none showing any
volume change.** Built two triangular-wedge void extrusions (correct bounding boxes, confirmed
overlapping the solid fin bar's full cross-section) meant to bevel/taper both ends. Checked for the cut
taking effect via: (1) `Extrusion.get_Geometry(new Options())` on the solid directly — volume unchanged;
(2) an explicit `Document.Regenerate()` inside its own transaction before re-checking — no change; (3)
geometry query through the 3D view (`Options.View` = the ThreeD view) — no change; (4) a much larger,
unambiguously-overlapping test void (a 100mm cube fully enclosing the fin bar) — still zero volume
change, ruling out "my wedge just doesn't actually overlap" as the explanation; (5)
`SolidSolidCutUtils.AddCutBetweenSolids(doc, solidElement, voidElement)` — this is the *explicit*
solid-void cut registration API, and it exists (confirmed live, correct parameter order is
`(document, elementToBeCut, cuttingElement)`), but it **actively refused** to run on this plain Generic
Model family document: `"The element must be in a project document or in a conceptual model, pattern
based curtain panel, or adaptive component family."` — meaning this API is specifically for document
types where auto-cut do NOT already happen, which by its own wording implies a plain family like this one
*should* auto-cut without it. Also ruled out: same-transaction vs. separate-transaction creation of the
solid and void made no difference either. **Net: genuinely unresolved** — either (a) void auto-cut
really isn't happening here for API-created geometry the way it does for UI-drawn "Void Extrusion"
tool use, or (b) it IS cutting correctly at the true Revit engine/render level and every one of these
five query methods simply fails to reflect it, which would mean `get_Geometry()` is not a reliable way to
verify a void cut at all. Whichever it is, don't assume a void form has cut anything based on a
geometry/volume query the way this file's earlier "verify with a resize test" standard would normally
require — for void cuts specifically, get a human visual check (screenshot or the user looking at the
family's 3D view) instead, until this is resolved one way or the other. If a future session solves this,
update this entry with the fix rather than leaving the "unresolved" framing stale.

### Third build (2026-07-19, bifurcation duct fitting) — ReplaceParameter/RenameParameter rollback corruption

**`FamilyManager.ReplaceParameter`/`RenameParameter`, used together to change an existing parameter's
group while keeping its name, produced real data corruption from a transaction that never committed —
the "uncommitted `Transaction` rolls back cleanly" assumption relied on elsewhere in this file does NOT
reliably hold for this specific API sequence.** Calling `ReplaceParameter(currentParameter, sameName,
newGroup, isInstance)` directly always throws `"Cannot replace a family parameter with another family
parameter, use RenameParameter() instead."` whenever `parameterName` matches the parameter's own current
name (Revit treats it as colliding with itself). The two-step fix implied by that message —
`RenameParameter(p, name+"__tmp")` then `ReplaceParameter(p, name, newGroup, isInstance)` — was tried
inside one transaction, looped over ~10 parameters. It threw the same error again partway through the
loop (plausibly a same-transaction stale-read issue, the same family of bug as this file's own
`Regenerate()`-outside-transaction lesson above), and the `Transaction` was never `Commit()`ed — it
should have rolled back everything. **It did not.** Live re-query after the exception showed: one
parameter split into two garbage duplicates with wrong values (a "Branch Spacing" parameter meant to
hold 550mm came back as two separately-named parameters holding 1120mm and 0mm), a completely different
parameter silently deleted outright (`get_Parameter` returned null, no trace), and — found later by
re-checking specifically because the above raised suspicion — two more parameters' VALUES quietly
changed (a width parameter and the main body's own driving Length parameter), even though nothing in the
failed script ever called `fm.Set()` on them. Separately, 3 `Extrusion` elements turned up in the
document where exactly 1 had ever been created — no script in the session explicitly duplicated one.

**Net effect: treat any `ReplaceParameter`/`RenameParameter` sequence that throws as leaving the entire
document's parameter *and geometry* state untrusted, not just "that one call failed."**

#### What the add-in does about the same clash, and how to reconcile the two (2026-08-22)

Harvested from the add-in's **Shared to Family** tool, which converts a shared family parameter into a
plain family parameter. It hits the identical wall and handles it in the mirror-image order:

1. `ReplaceParameter(source, targetName, group, isInstance)` — the direct attempt.
2. Only if that throws **"name already in use"**: `ReplaceParameter(source, TEMP_NAME, ...)` first, then
   `RenameParameter(converted, targetName)`.

The Brain's 2026-07-19 attempt was the other way round — **rename to a temp name first, then replace** —
looped over ~10 parameters in one transaction, and that is what corrupted the document. Replace-to-temp
**then** rename touches the parameter in a different order and is what the add-in has been shipping.

**Neither ordering is proven safe here, and the difference is not the point.** What the add-in adds is
the observation that the direct call fails *specifically* with a name-in-use error, so the detour is
worth attempting only on that exact exception — not as a standing pattern. What this Brain adds, and the
add-in has no equivalent of, is the **measured corruption**: a failure mid-loop left garbage behind that
an uncommitted transaction did not roll back.

**So the rule stands, unchanged and now better argued:**

- **Catch the name-in-use error specifically** and try the temp-name detour once, replace-to-temp first.
- **One parameter per transaction**, never a loop of ten inside one — the corruption was found in a loop.
- **After ANY throw, re-query names, groups, values and geometry counts** before doing anything else.
- The add-in's tool is a human driving one family at a time with Revit's own undo behind them. A script
  batching parameters has neither, which is why the caution here is stricter than the tool's.

 After a failure
here, don't assume "the transaction didn't commit, so nothing changed" — re-query every parameter's name,
group, AND value (not just existence), and re-count geometry elements, before continuing. Same standard
of proof this file already requires for resize tests, just triggered by a failure instead of a success.

**Working, corruption-free alternative — but only for parameters with NO existing geometry
association** (no `Dimension.FamilyLabel`, no `AssociateElementParameterToFamilyParameter` link):
`FamilyManager.RemoveParameter(param)` followed immediately by a fresh `FamilyManager.AddParameter(sameName,
newGroup, sameType, sameIsInstance)` + `fm.Set()` to restore the value, all in one transaction. Worked
cleanly across 7 parameters, no corruption. **NOT attempted, and NOT safe, for a parameter that already
drives geometry** — removing and re-adding creates a new parameter with a new `Id`, which would orphan
any dimension label or association built in earlier steps. No verified-safe technique for changing an
already-geometry-linked parameter's group was found this session. If this comes up again: test on a
throwaway/duplicate family first, and re-verify the geometry's bounding box afterward before trusting it.

### Fourth build (2026-08-10, Condair EL 20 steam humidifier from a manufacturer datasheet) — pipe + electrical connectors proven

Built a Mechanical Equipment family from a PDF datasheet: 530×406×780 cabinet, five connectors (steam,
supply water, drain, condensate, 3-phase power), a toggleable clearance zone, and 63 parameters. Recipe:
[`create-equipment-family-from-datasheet.cs`](../../scripts/recipes/create-equipment-family-from-datasheet.cs).
Everything below is live-verified in this build unless it says otherwise.

**Reflection beats the deliberate-compile-error technique for discovering an unknown signature.** The
three-step compile-error method documented in the first build above still works, but
`typeof(ConnectorElement).GetMethods(BindingFlags.Public | BindingFlags.Static)` printed every overload's
real parameter names and types in ONE read-only call — no transaction, no failed calls, no guessing.
Same trick enumerates enum values (`Enum.GetNames(typeof(PipeSystemType))`) and confirms whether a
`BuiltInParameter`/`ParameterType`/`DisplayUnitType` member exists on this Revit version before writing
code that names it. Reach for reflection first; keep the compile-error method for things reflection
can't answer (which overload Revit actually resolves, what a null argument does at runtime).

**`ConnectorElement.CreatePipeConnector` and `CreateElectricalConnector` are PROVEN, same shape as the
duct one.** Signatures (Revit 2020, read by reflection, then run):
`CreatePipeConnector(Document, Plumbing.PipeSystemType, Reference planarFace)` and
`CreateElectricalConnector(Document, Electrical.ElectricalSystemType, Reference planarFace)`; each also
has a 4-arg overload taking a trailing `Edge`. Both need a `Reference` to a planar face on the family's
own solid, both must run in a `Transaction`, and both put the connector at the face centre with the face
normal as its direction — exactly like `CreateDuctConnector`. **Revit has no Steam pipe system type** —
`PipeSystemType.OtherPipe` is the correct choice for steam. `ElectricalSystemType.PowerBalanced` is the
one for a balanced 3-phase load.

**Pipe connectors have the SAME "size is not inherited from the face" bug as duct connectors** — a
connector created on a 45 mm circular face came back `Radius` 304.8 mm / `Diameter` 609.6 mm (1 ft and
2 ft, generic placeholders). Unlike `Connector.Width`/`.Height`, both `CONNECTOR_RADIUS` and
`CONNECTOR_DIAMETER` are directly writable here and stay linked to each other. Since manufacturer
datasheets quote OD, **associate `CONNECTOR_DIAMETER` to an OD family parameter** via
`AssociateElementParameterToFamilyParameter` — no half-value helper parameter needed. Proven by driving
the family parameter to 80 mm and reading the connector back at 80 mm. `RBS_PIPE_DIAMETER_PARAM` and
`CONNECTOR_PROFILE_TYPE` are **null** on a pipe connector — don't reach for them. Flow direction is
`RBS_PIPE_FLOW_DIRECTION_PARAM`, an int taking `FlowDirectionType` (Bidirectional=0, In=1, Out=2).
There is no `BuiltInParameter.CONNECTOR_DESCRIPTION`; the field is reached with
`LookupParameter("Connector Description")`.

**Electrical connector load data**: `RBS_ELEC_NUMBER_OF_POLES` (int), `RBS_ELEC_VOLTAGE`,
`RBS_ELEC_APPARENT_LOAD` (`DUT_VOLT_AMPERES` exists and converts), plus `Power Factor` by name. Set the
apparent load from the datasheet's own current rather than its kW figure — `sqrt(3) x 400 V x 21.7 A` =
15034 VA reproduces the quoted 21.7 A MCA exactly, where a rounded 15000 W would not.

**Family TYPE names reject `\ : { } [ ] | ; < > ? ` ~` — parameter names accept `/`.** A type named
after the datasheet model `"EL 20-400V/3~"` throws `ArgumentException: The name cannot contain these
prohibited chars` on the tilde (the slash is fine). Parameter names, tested separately, DO accept `/`,
so `"Nominal Capacity (kg/h)"` is legal — worth knowing because hyphenated workarounds look wrong on a
manufacturer schedule. Element *values* are unrestricted (`G 3/4"` stored fine).

**`NewExtrusion` accepts a NEGATIVE end value and extrudes downward** — no need to create upward and
then juggle `EXTRUSION_START_PARAM`. Four stubs built with `NewExtrusion(true, arr, sp, -length)` landed
at Z −60…0 first time. `EXTRUSION_START_PARAM` is separately writable and takes a negative too.

**A Length family parameter accepts a NEGATIVE formula result and can drive an extrusion's start.**
`SetFormula(p, "-Floor Clearance")` resolved to −600 mm and associated cleanly to
`EXTRUSION_START_PARAM`, making the underside of a clearance box parametric. This was expected to be
rejected (Revit refuses negative lengths typed into many UI fields) and was written with a fallback —
the fallback was not needed.

**Unconstrained extrusions sketched on the shared horizontal plane get auto-constrained by Revit and
track the nearest body face on resize.** Four stubs were placed at absolute coordinates with no
`NewAlignment` and no labelled dimension. Changing Width 530→700 and Depth 406→500 moved them: each kept
its original 105 mm offset from the nearest side face and 106 mm from the front face, and every one
returned to its exact original coordinate on reset. Revit's own sketch dimensions did this (the finished
family reports 48 `Dimension` elements against 6 created explicitly). **Useful, but implicit** — if a
stub's position genuinely matters, dimension it on purpose rather than relying on this.

**Clearance-zone pattern that works**: `Document.Settings.Categories.NewSubcategory(mechCategory,
"Clearance")` → `Material.Create` + `material.Transparency = 80` → `subcategory.Material = m` →
`extrusion.Subcategory = sub` → `AssociateElementParameterToFamilyParameter(
extrusion.get_Parameter(BuiltInParameter.IS_VISIBLE_PARAM), yesNoInstanceParam)` for the on/off toggle.
Formula-driven parameters CAN be used as a `Dimension.FamilyLabel`, so an asymmetric box
(`Width / 2 + Left Clearance` one side, `Width / 2 + Right Clearance` the other) stays fully parametric.

**Where the numbers come from matters as much as the API.** This build was done twice: first from a
screenshot of one data-sheet page, then corrected against the full 107-page submittal. The data sheet
gives connection SIZES; only the **shop drawing** gives their positions, how many there are, and which
face they are on — the first pass put four connections on the wrong face at invented coordinates and
missed a second port and a second electrical supply entirely. Full lesson, plus the PDF tool chain that
works on this machine and a silent `pdftotext` row-offset trap, in
[`../reading-manufacturer-datasheets.md`](../reading-manufacturer-datasheets.md). Two API facts from the
correction pass: **unit suffixes are legal in formulas** (`"Height + 60 mm"` resolves fine, so a stub can
track the body without a separate offset parameter), and `EXTRUSION_START_PARAM` + `EXTRUSION_END_PARAM`
can BOTH be associated to family parameters, which is how a top-face stub keeps a 5 mm overlap into the
body through a resize instead of being extruded from Z=0 through the whole cabinet.

**`SketchPlane.Create(doc, plane.GetPlane())` silently produces an UNHOSTED work plane — the element's
"Work Plane" property reads `<not associated>`.** `GetPlane()` hands back a bare geometric `Plane`, so
Revit makes an anonymous sketch plane with no link to the reference-plane *element*. Use the datum
overload instead: **`SketchPlane.Create(Document, ElementId datumId)`**, passing the ReferencePlane's
`.Id`. The three overloads are `Create(Document, ElementId datumId)`, `Create(Document, Reference
planarFaceReference)` and `Create(Document, Plane)` — only the first two host. **This cannot be repaired
after the fact**: `Sketch.SketchPlane` has no setter (`CanWrite == false`) and `SKETCH_PLANE_PARAM` is a
read-only string, so a wrongly-hosted extrusion has to be deleted and rebuilt. Get it right at creation.

**Unused `SketchPlane` elements are auto-purged between `run_csharp` calls.** A sketch plane created in
one call and not yet consumed by geometry is gone by the next call — `doc.GetElement(thatId)` returns
null and `NewExtrusion` throws `"Value cannot be null. Parameter name: sketchPlane"`. **Create the sketch
plane inside the same transaction as the extrusion that uses it**, from the datum's `ElementId`.

**Dimensions that reference only reference planes SURVIVE deleting all the geometry.** A `Width`
dimension between `Unit Left` and `Unit Right` touches no solid, so a "delete every extrusion and
rebuild" pass leaves it behind — and the rebuild then adds a second one, giving two labelled dimensions
driving the same parameter. Harmless until something moves, then it is a constraint fight. **After
clearing geometry for a rebuild, list `Dimension` elements with a non-null `FamilyLabel` and delete the
stale ones explicitly** (alignments die with the geometry; plane-to-plane dimensions do not).

**A horizontal reference plane's normal direction follows the endpoint order — swap `bubbleEnd`/
`freeEnd` to flip it.** `NewReferencePlane(new XYZ(-3,0,z), new XYZ(3,0,z), XYZ.BasisY, elevation)` came
out normal `(0,0,-1)`; swapping the two endpoints gave `(0,0,+1)`. This matters because
`EXTRUSION_START_PARAM`/`END_PARAM` are measured along the host plane's normal — on a down-facing plane
a positive end value extrudes downward. Always read `GetPlane().Normal` back and flip before building.

**A single full circle is accepted but Revit normalises it to two arcs anyway.**
`Arc.Create(centre, r, 0, 2*Math.PI, XYZ.BasisX, XYZ.BasisY)` returns a valid unbound curve
(`IsBound == false`, length `2*pi*r`) and `NewExtrusion` takes it — but the resulting
`Sketch.Profile` reports **2 curves** and the solid has **2 cylindrical faces**, exactly as if two
half-arcs had been passed. So the two-arc idiom is not a workaround for a missing feature; it is what
Revit stores either way. Passing one closed circle is still the clearer expression — prefer it.

**DO NOT put a `NewDiameterDimension` on an extruded cylinder's face and label it — it does not drive
the sketch, and it plants a delayed "Constraints are not satisfied" error.** Tried on all seven round
stubs: every call succeeded, `FamilyLabel` accepted the OD parameter, and the dimension read the right
value — but changing that parameter left the circle at its original size (parameter and connector both
moved to 90 mm, geometry stayed 45 mm), and the *next* regeneration threw a modal
`Constraints are not satisfied` error that cannot be ignored. The parameters were NOT flagged reporting
(`IsReporting == false`), so this is not the reporting-parameter trap — the dimension simply attaches to
the derived solid face, not the sketch curve that would have to move. **In Revit 2020 there is no API
route to dimension a sketch curve after the sketch is created** (`SketchEditScope` arrives in 2022).
Net: for an API-built round stub, let the OD parameter drive the **connector**
(`CONNECTOR_DIAMETER` association — proven, and it survives) and accept that the visible cylinder is
fixed. If the drawn circle must resize too, that dimension has to be added by hand in the Family
Editor, inside sketch-edit mode, where it binds to the curve.

**`FamilyManager.MakeType(param)` / `MakeInstance(param)` flip a parameter's scope safely — use these,
NOT `ReplaceParameter`.** The third-build entry above documents `ReplaceParameter`/`RenameParameter`
corrupting a document. That whole trap is avoidable for a scope change: `MakeType` and `MakeInstance`
are single-argument methods that do exactly this job. Converted three instance parameters to type in one
transaction with no corruption and **no loss of value or association** — including one that was driving
`IS_VISIBLE_PARAM` on the clearance solid, which kept working. Only reach for `ReplaceParameter` when the
parameter GROUP has to change too, and re-read the third-build warning first.

**A HORIZONTAL reference plane has to be created in an ELEVATION view, and its cut vector is
`XYZ.BasisY`, not `BasisZ`.** For a top-of-unit plane at Z=780:
`NewReferencePlane(new XYZ(-3,0,LFt(780)), new XYZ(3,0,LFt(780)), XYZ.BasisY, frontElevationView)` —
the line runs along X, and normal = lineDirection x cutVec = (1,0,0) x (0,1,0) = (0,0,1). Passing
`BasisZ` (which is right for the vertical side planes) would be degenerate here. **The matching
`NewAlignment` (top face to that plane) and the vertical `NewDimension` labelled `Height` also need the
elevation view** — a `ViewPlan` cannot host either, because in plan a horizontal plane and a horizontal
face are edge-on. The family template ships `Front`/`Back`/`Left`/`Right` elevations; any of them works.

**Driving Height by an aligned top plane and a labelled dimension is an ALTERNATIVE to associating
`EXTRUSION_END_PARAM` — do one or the other, never both.** Aligning the body's top face to a "Unit Top"
reference plane and then dimensioning `Reference Plane` -> `Unit Top` with `FamilyLabel = Height` drives
the extrusion just as well, and it gives the modeller a real plane to snap and dimension to in the
Family Editor. Adding the parameter association on top of that alignment would over-constrain it.

**EQ-centre BOTH plan axes unless there is a reason not to.** The first pass pinned the back face to
`Center (Front/Back)` and dimensioned only forward, so the family was symmetric left/right but one-sided
front/back — the insertion point sat on the back face. That is defensible for wall-mounted kit (it snaps
to a wall), but a modeller reasonably expects the origin at the centre of the box, and the asymmetry is
invisible until someone places one. Build the second axis the same way as the first: a 3-reference EQ
dimension (`Unit Front`, `Center (Front/Back)`, `Unit Back`, `AreSegmentsEqual = true`) plus a
2-reference dimension labelled `Depth`. Verified centred through two non-square resizes and a reset.

**Give every extrusion its own subcategory.** `extrusion.Subcategory = someSubcategory` costs one line
and makes the family readable — each part can be switched off, coloured, and identified in the Family
Editor and in the project. Cheap at build time, painful to retrofit once geometry is dimension-locked.

**Unlock formula-locked driving dimensions before rebuilding geometry that they constrain.**
`SetFormula(p, null)` restores the last value and makes the parameter editable; re-apply the constant
formula after the geometry is back. Building new dimensions against a locked parameter risks the
constraint solver fighting a value it cannot change.

**Locking a manufacturer family to its real product size: give the driving dimensions a constant
formula.** `SetFormula(widthParam, "530 mm")` greys the parameter out in the UI, so nobody can drag the
cabinet to a size the manufacturer doesn't make — which would silently misrepresent a purchased unit in
a coordination model. **A formula-locked parameter still works as a `Dimension.FamilyLabel` and still
works as the target of `AssociateElementParameterToFamilyParameter`** (verified: after locking
Width/Depth/Height on the humidifier, the body stayed 530×406×780, all nine dependent formulas —
`Width / 2 + Left Clearance`, `Height + 60 mm`, etc. — still resolved, and all six connectors held
position). Do this once the family is a specific product; leave the parameters free only while the
geometry is still being proved by resize tests. Reversing it is one `SetFormula(p, null)`.

**Saving: blocked for the document open in Revit's UI, but WORKS for a document the API created itself.**
Two different situations, and the 2026-08-10 note that said "saving is blocked, full stop" was too broad —
corrected 2026-08-11.

- **The active/UI document**: `Document.SaveAs(...)` throws `"Operation is not permitted when there is
  any open transaction phase started by API client."` even though `Document.IsModifiable` reads **False**
  at the time — the bridge holds a `TransactionGroup`, not a `Transaction`, so `IsModifiable` is not a
  reliable way to test for it. `Document.Save()` is blocked by the **same** error once the document has
  a path (tested 2026-08-11 on an already-saved family); before it has one it fails earlier with
  `"File path must be already set"`. So there is no way in — hand the user the folder and filename and
  ask for File → Save As, or Ctrl+S if the file already exists.
- **A document created by `Application.NewFamilyDocument(templatePath)`**: `SaveAs` **succeeds**, and so
  does `Save` afterwards. This is the route to building a family end-to-end with no user interaction.

**Full pattern for authoring a family from scratch through the bridge** (proven 2026-08-11, built the
Condair EL 8 this way — 68 parameters, 2 types, 9 extrusions, 6 connectors, saved, 40/40 verification):

1. `var nd = app.NewFamilyDocument(@"C:\ProgramData\Autodesk\RVT 2020\Family Templates\English\Metric Mechanical Equipment.rft");`
2. `nd.SaveAs(path, new SaveAsOptions{ OverwriteExistingFile = false });` — do this FIRST, so the file
   exists and later `Save()` calls are cheap.
3. Build into `nd`, **never the ambient `Document` global** — that still points at whatever is open in
   the UI. Re-find it each call with
   `app.Documents.Cast<Document>().First(d => d.Title == "<name>")` and guard with
   `if (doc.Title == Document.Title) return "refusing to write into the active document";`
4. `nd.Save(new SaveOptions{ Compact = true })` at the end.

**`nd.ActiveView` is null — the new document has no UI window**, so the user cannot see it while it is
being built and could not have saved it by hand. That is exactly why step 2 matters: the file lands on
disk regardless, and the user just opens it afterwards. Views inside it (`Ref. Level`, `Front`, …) are
all present and work fine for `NewAlignment`/`NewDimension` via a `FilteredElementCollector` on `nd`.

**`FamilyManager.CurrentType = someType` is a document modification — it needs an open `Transaction`.**
Outside one it throws `"A sub-transaction can only be active inside an open Transaction."`. To *read*
another type's values without switching, use `familyType.AsDouble(param)` / `.AsString(param)` /
`.AsInteger(param)` directly — no transaction, no side effects. Only switch `CurrentType` when you
actually need to write.

## Reference-plane subcategories, and his house style for centre lines (2026-08-24)

A fresh Mechanical Equipment template arrives with **3 reference planes and zero subcategories** under
`Reference Planes`. Ajmal's standing preference, given while starting
`TRG_MECH_EQP_Fan Coil Unit_FCU_R0`: put the centre lines in **their own subcategory so they are
switchable on their own**, and make them read as centre lines at a glance.

| Setting | Value | His words |
|---|---|---|
| Subcategory name | `Center Lines` under `Reference Planes` | *"add sub categery for this all… this is on center lines"* |
| Line colour | **red**, RGB(255,0,0) | *"color make it red"* |
| Line pattern | **`Aligning Line`** | *"line patern same as before Aligning Line"* |
| Line weight | 1 (left alone) | not mentioned |

**"same as before" meant the line pattern, not the colour.** He named `Aligning Line` himself in the
same sentence, which is what settles it — an older family on disk happened to use the same pattern in
green, but that file is not a standard (see the note at the end of this document) and was not the
reason. **Red is his choice. Do not "correct" it to green.**

### How to do it

- Category is **`BuiltInCategory.OST_CLines`** — named `Reference Planes`, id −2000530.
- Create with `doc.Settings.Categories.NewSubcategory(parent, "Center Lines")` inside a transaction.
- Assign per plane via **`BuiltInParameter.CLINE_SUBCATEGORY`** — it is an `ElementId` parameter, it is
  **writable** on a `ReferencePlane`, and it reads back as **−2000530 (the parent category id) when no
  subcategory is set**, not as `InvalidElementId`. Test for `> 0`, or an unassigned plane looks assigned.
- Style it with `cat.LineColor = new Color(255,0,0)` and
  `cat.SetLinePatternId(id, GraphicsStyleType.Projection)`.
- **`GraphicsStyleType.Cut` throws on this category** — reference planes are never cut. Wrap the Cut call
  in its own try/catch, or a correct Projection change gets rolled back by the Cut failure beside it.
- Resolve `Aligning Line` **by name** off a `LinePatternElement` collector, never by a remembered id —
  it was 276 in this family, and that number is per-document.

**A plane added later defaults back to the plain category**, so set `CLINE_SUBCATEGORY` as each new
casing/neck/stub plane is created rather than sweeping at the end.

Watch the stock horizontal plane: it comes through unnamed and set to **"Not a Reference"**, unlike the
two named `Center (Front/Back)` / `Center (Left/Right)` verticals. If anything gets dimensioned to it, it
has to become a real reference first or the family cannot be locked to from a project.

### `ELEM_REFERENCE_NAME` ("Is Reference") integers are NOT the dropdown order (2026-08-24)

The obvious guess — that the integer follows the Family Editor's dropdown, `Not a Reference` first at 0 —
is **wrong**, and wrong silently: a bad integer still sets *something*, so the plane ends up a reference
of the wrong face and nothing errors. Calibrated against a stock Mechanical Equipment template and then
proven by setting each value and reading `AsValueString()` back:

| int | Reads as | How known |
|---|---|---|
| 0 | Left | **set + read back** |
| 1 | Center (Left/Right) | template's own plane |
| 2 | Right | **set + read back** |
| 3 | Front | **set + read back** |
| 4 | Center (Front/Back) | template's own plane |
| 5 | Back | **set + read back** |
| 8 | Top | **set + read back** |
| 12 | Not a Reference | template's own plane |

**Completed and fully proven later the same day** by setting every integer 0-14 and reading
`AsValueString()` back inside a rolled-back transaction:

| int | Reads as | int | Reads as |
|---|---|---|---|
| 0 | Left | 6 | **Bottom** |
| 1 | Center (Left/Right) | 7 | **Center (Elevation)** |
| 2 | Right | 8 | Top |
| 3 | Front | 12 | Not a Reference |
| 4 | Center (Front/Back) | 13 | **Strong Reference** |
| 5 | Back | 14 | **Weak Reference** |

**9, 10 and 11 resolve to nothing** — the enum is not contiguous, so never walk it assuming every value
in range is valid.

**Always read `AsValueString()` back after setting it.** The integer is meaningless to look at, the UI
name is the thing you actually meant, and this parameter fails quietly.

### Measuring where a reference plane sits — do not use `Origin.DotProduct(Normal)`

That returns the signed distance along **the plane's own normal**, and two planes on opposite faces of
the same box can be created with the **same** normal direction, so the sign tells you nothing about
which side they are on. Observed the same day: `Casing Left` reported `+500` and `Casing Right` `-500`
while both were correctly placed at X −500 / +500, because both came out with normal (−1,0,0).

Read the actual coordinate instead — pick the axis off the normal, then report `Origin.X`/`.Y`/`.Z`:

```csharp
var pl = rp.GetPlane(); var n = pl.Normal; var o = pl.Origin;
double mm = Math.Abs(Math.Abs(n.X)-1) < 0.001 ? o.X*304.8
          : Math.Abs(Math.Abs(n.Y)-1) < 0.001 ? o.Y*304.8
          : o.Z*304.8;
```

`Origin` also carries junk in the two axes the plane does not control (the free end of the drawn line —
e.g. `(1500, -350, 0)` for a plane that only fixes `Y = -350`). Only the controlled axis means anything.

### Creating the planes

`doc.FamilyCreate.NewReferencePlane(bubbleEnd, freeEnd, cutVector, view)` — the third argument is a
**direction, not a point** (already noted above; it keeps catching people). Normal comes out as
`(freeEnd - bubbleEnd) x cutVector`:

| Plane wanted | bubble / free | cutVector | Create in |
|---|---|---|---|
| Controls left-right (normal X) | vary Y at fixed X | `XYZ.BasisZ` | the **plan** view |
| Controls front-back (normal Y) | vary X at fixed Y | `XYZ.BasisZ` | the **plan** view |
| Controls height (normal Z) | vary X at fixed Z | `(0,-1,0)` | an **elevation** view — not plan |

A horizontal plane cannot be created in a plan view. Family orientation, confirmed from the template's
own elevation `ViewDirection`s: **Front is −Y, Back +Y, Left −X, Right +X.**

### Resizing a reference plane: `BubbleEnd`/`FreeEnd` are writable, but only within the plane (2026-08-24)

Both properties report `CanWrite == true` and both really are settable — but the endpoints must stay
**in the plane the reference plane already defines**. Move one endpoint out of that plane and Revit
throws:

> `The vector from BubbleEnd -> FreeEnd is not perpendicular to the normal vector.`

The trap is that this fires on the **intermediate** state. Setting `BubbleEnd` and `FreeEnd` in sequence
means that after the first assignment the line runs from the *new* first endpoint to the *old* second
one, and if the two differ in the axis the plane does not control, that intermediate line tilts out of
the plane. Both orders fail, so trying `FreeEnd` first is not a workaround. Measured: copying the stock
centre planes' endpoint Z (−1700.8 / −1426.4) onto casing planes drawn at Z 0 was refused on all five,
in both orders.

| Change wanted | Works? |
|---|---|
| Shorten / lengthen along the line, same plane | **yes** — set both, no sub-transaction gymnastics needed |
| Slide the endpoints within the plane | **yes** |
| Move endpoints to a different Z (plane's own offset unchanged) | **no** — delete and recreate |

So **trimming a plane's drawn extent is cheap and non-destructive**; **relocating where the line is
drawn in the perpendicular axis is not** — that needs `NewReferencePlane` again, plus re-applying name,
`CLINE_SUBCATEGORY` and `ELEM_REFERENCE_NAME`, because a fresh plane carries none of them.

Trimming to a unit's own footprint plus a margin is the tidy default — for a 1000 x 700 casing with a
150 mm margin, lines along X run −650..650 and lines along Y run −500..500. Pick the axis off the
normal: an **X-normal** plane's line runs along **Y**, a **Y-normal** or **Z-normal** plane's line runs
along **X**.

A stock Mechanical Equipment template draws its own centre planes **2731–3346 mm long, at Z −1426 and
−1700** — far below the unit and far past it. They stay long after the casing planes are trimmed, so
they become the longest lines in the family. Expect to be asked to trim those too.

#### Recreating a plane at a different endpoint Z — it works, and plan-view creation preserves Z

The delete-and-recreate that the section above says is required does work, and the part worth recording
is that **`NewReferencePlane` called with a plan view keeps a non-zero endpoint Z** — it does not flatten
the endpoints to the view's own elevation, which was the obvious worry. Passing bubble/free at
Z −1700.8 with the plan view as the `view` argument produced planes whose endpoints read back at exactly
−1700.8.

To make a new plane draw exactly like one of the template's stock centre planes, copy that plane's
`BubbleEnd`/`FreeEnd` and substitute only the coordinate the new plane controls. The cut vectors that
reproduce the stock normals:

| New plane | Copies | cutVector | Create in |
|---|---|---|---|
| X-normal (left/right face) | `Center (Left/Right)` | `(0,0,-1)` | plan |
| Y-normal (front/back face) | `Center (Front/Back)` | `(0,0,-1)` | plan |
| Z-normal (top face) | the stock horizontal | `(0,-1,0)` | **elevation** |

**Capture before deleting.** A recreated plane is a new element with none of the old one's settings —
name, `CLINE_SUBCATEGORY` and `ELEM_REFERENCE_NAME` all come back as defaults, so read all three off
each plane into a list first, delete, recreate, then write them back.

**Why the template's own planes look different from freshly-made ones:** the stock centre planes are
drawn down at Z −1426.4 and −1700.8, *below* the unit, while anything created at the working level lands
at Z 0. Same length, same position, completely different appearance in elevation — and the length
comparison that looks like the obvious culprit is a dead end, because the lengths already match to the
decimal. **Check endpoint Z before believing a length problem.**

This only stays free while nothing is locked to the planes. Once extrusions are aligned or dimensioned
to them, delete-and-recreate breaks those references and the extent is effectively frozen — so settle
how the planes should be drawn *before* building geometry against them.

#### Do NOT change reference-plane extents through the bridge — Ajmal does that by hand (2026-08-24)

Two different API approaches were tried on the same set of casing planes and **he undid both**, then
said plainly: *"no its worng so n need this i will do it manually"* / *"i already undo"*. First attempt
trimmed them short; second rebuilt them to copy the stock centre planes' endpoints exactly. Neither was
what he wanted.

The reason is structural, not a matter of picking better numbers. **A reference plane's extent is one
model property, not a per-view graphic** — his own words on the first undo were
*"becouse the plan view and in the elevations also"*. Dragging a plane's end in Revit changes how it
reads in **every** view at once, and judging that trade-off is a visual call made while looking at the
model. It is a two-second drag for him and a guess for the bridge, which cannot see the screen.

**How to apply:** build the planes at the right *positions* — that is the part worth automating, and it
was accepted without complaint. Then stop. Do not trim, lengthen, or relocate the drawn line, and do not
offer to; if the extents look wrong, say so and leave it to him. The API detail in the sections above
stays recorded because it is true and was expensive to learn, **not** as a licence to use it here.

Related and still true: once geometry is locked to these planes, delete-and-recreate stops being free —
so this is his call to make early, by hand, before anything depends on them.

## "The classification is blank" — there are TWO fields with that name (2026-08-24)

Family Category and Parameters shows two unrelated things a modeller reads as "classification", and they
fail differently. Established on a Mechanical Equipment FCU family.

| Field | BuiltInParameter | What it is | Blank looks like |
|---|---|---|---|
| **OmniClass Number** | `OMNICLASS_CODE` | the real classification | a *generic category* code, not an empty string |
| Classification | `MEP_EQUIPMENT_CLASSIFICATION` | hydronic systems-analysis flag | `None` — usually correct |
| Assembly Code | type parameter | Uniformat | genuinely empty, and **unfillable with 0 family types** |

**OmniClass never looks empty, so it hides.** A stock Mechanical Equipment template ships
`23.75.00.00` = *Climate Control (HVAC)* — the whole category. It reads as filled in while classifying
the family as nothing in particular. **Check the value, not whether it is blank.**

**`OMNICLASS_DESCRIPTION` ("OmniClass Title") is read-only and derived by Revit from the number.** That
makes it a free self-check: set the number, read the title back, and if it changes to the thing you meant
the code is real. Setting `23.75.70.17.27` turned the title into *Fan Coil Units* on its own — proof, not
assertion. **Fan coil unit = `23.75.70.17.27`.**

**`MEP_EQUIPMENT_CLASSIFICATION` accepts only two values in Revit 2020** — `0 = None`, `5 = Pump`.
Nothing else is accepted. It is a hydronic-analysis flag, not a general classifier, so `None` is the
right answer for an FCU and for most equipment. Do not go looking for a fan-coil entry in it.

### Discovering a hidden enum without leaving a mark

For any integer parameter whose valid values are undocumented: open a transaction, loop the integers,
`Set(i)`, keep the ones where `AsInteger() == i` and `AsValueString()` is non-empty, then **`RollBack()`**.
The list comes back exact and the document is untouched — verify by re-reading the value afterwards.
Cheaper and safer than guessing an enum and writing the wrong one.

### Assembly Code needs a family type to exist first

`Assembly Code`, `Keynote`, `Description`, `Model`, `Manufacturer` and `Type Comments` are all **type**
parameters. A family template arrives with **zero types**, so all of them are unreadable and unsettable —
`FamilyManager.CurrentType` is null and `AsString(fp)` has nothing to read from. The Uniformat table is
loaded and fine; the missing piece is the type. Create one (`FamilyManager.NewType`) before trying to
fill any of them, and do not report them as "blank" without saying why they cannot be filled.

**Do not invent the Uniformat/Uniclass code.** Ajmal's own finished FCU has Assembly Code empty, so there
is no house precedent to copy — it comes from the project BEP/EIR. OmniClass was different and safe to
set because the code was read off his existing family and Revit confirmed it.

### The verified OmniClass branch for terminal units, read out of Revit's own table

Probed level by level and rolled back, so these are Revit's strings, not remembered ones:

```
23.75.70.17      Water Heated and Cooled Terminal Heating and Cooling Units
  .11            Radiators
  .17            Radiation Panels
  .21            Embedded Water Heating Terminals
  .27            Fan Coil Units
  .31            Induction Units
```

**Levels 23, 23.75 and 23.75.70 resolve to nothing** — Revit's table only carries entries at the 4th and
5th level, so there is no browsable parent chain to walk up. Do not try to "check the parent code" as a
sanity test; it comes back blank for a perfectly valid code.

**OmniClass Number is free text — Revit accepts any string.** A wrong code is stored silently and looks
filled in. The control test that settles it: write `99.99.99.99.99` and read the read-only Title, which
goes **blank**; a genuine code makes it resolve to a name. Roll back afterwards. Use that whenever a code
comes from anywhere other than a family that already works.

Note the branch is **water** heated/cooled — correct for a chilled-water FCU, worth a second look for a
DX/refrigerant unit, though `.27` is still the closest entry.

## Assembly Code: no self-check inside a family, so read Revit's table off disk (2026-08-24)

**`Assembly Description` does not exist in a family document** — only in a project. So the
set-it-and-read-the-title trick that verifies OmniClass has no equivalent here: `Assembly Code` is plain
text, Revit accepts anything, and nothing tells you the code is real. Probing candidate codes inside the
family is pointless — every one comes back with no description.

**Read the shipped table instead.** Plain tab-separated ASCII, `code · description · level · categoryId`:

```
C:\ProgramData\Autodesk\RVT <ver>\Libraries\US Metric\UniformatClassifications.txt
```

(`US Imperial` alongside it; RVT 2024 nests it under `Libraries\English-Imperial\US\`.) It is **not**
UTF-16 — running `iconv` over it silently yields nothing and looks like an empty branch. `grep -a` it
directly. The 4th column is a BuiltInCategory id: **−2001140 = Mechanical Equipment**, so filtering on it
gives only the codes valid for that category.

### There is no fan coil unit code

The entire HVAC branch is 3 levels deep and stops well short of equipment types:

```
D30        HVAC
  D3040    Distribution Systems
  D3050    Terminal & Package Units          <- the closest home for an FCU
    D3050100  Terminal Self-Contained Units
    D3050200  Package Units
  D3060    Controls & Instrumentation
```

Used **D3050**, not `D3050100` — "self-contained" implies an on-board refrigeration circuit, which a
chilled-water fan coil does not have. Level 4 only if a BEP demands the depth.

**Assembly Code is UNIFORMAT; Uniclass 2015 is a different system and will not fit in it.** ISO 19650
and the office naming guide both point at Uniclass, and Revit has no built-in field for it — it needs a
**shared parameter** named `Classification`. Do not quietly put a Uniclass code in Assembly Code; the
two numbering systems do not overlap and a schedule reading Assembly Code will show nonsense.

## Identity Data that must NOT be auto-filled

Asked to "fill all of this" on a family type's Identity Data, fill only what is derivable and leave the
rest empty:

| Fill | From |
|---|---|
| Description | his `<Device in words> - <Shape>` convention, off the family name |
| Assembly Code | Revit's Uniformat table, above |
| Default Elevation | whatever he states — **2400 mm** for a ceiling-level FCU, 2026-08-24 |

**Never invent Manufacturer, Model, Cost or URL.** They are claims about a real product, they flow into
schedules and submittals, and a wrong value there reads as authoritative in a way an empty one never
does. Ask for them — it is one line for him. Same for Type Image (needs a file) and Keynote (needs a
project keynote table). Say plainly which fields were left and why, rather than reporting "filled".

## The office shared-parameter file: where it is, and what it did not contain (2026-08-24)

`Application.SharedParametersFilename` points at:

```
D:\Ajmal\BIM Resources\NEW\Modeling\06_Shared_Parameters\Shared_Parameters.txt
```

Same `NEW\Modeling\` tree as the families — the numbered folders (`02_Families`, `06_Shared_Parameters`)
are the live library. **Ask Revit for the path; never assume.** Three stale copies named
`Shared Parameter*.txt` sit in the old flat folder and are not the file in use.

**It is UTF-16LE with a BOM, and its own header says "Do not edit manually."** Consequences:

- `grep` finds nothing and reports zero groups — which reads as an empty file rather than an encoding
  problem. Decode with `iconv -f UTF-16LE`. (The Uniformat file in the same workflow is plain ASCII and
  breaks under the *same* `iconv`, so the two need opposite handling — check the first bytes.)
- Add parameters through `DefinitionGroup.Definitions.Create(ExternalDefinitionCreationOptions)`, never
  by writing text. **Back the file up first** — it is office-wide and other people's models resolve
  GUIDs against it.
- `PARAM` line layout: `GUID · NAME · DATATYPE · DATACATEGORY · GROUP · VISIBLE · …` — the group id is
  **column 6**, not 5. Verify additions by reading it back off disk, not from the API's return value.

### It had no hydronic content at all

146 parameters in 17 groups, strong on air, ductwork, tagging and project identity — and **zero
`PipeSize`, zero `PipingFlow`, zero `PipingPressure`, zero `HVACPower` parameters anywhere in it.**
Chilled-water equipment had never been added. Anything hydronic has to be created before it can be used.

Added group **`Mechanical_Equipment`** (id 25) with 15: the four `Supply_/Return_Air_Width/Height`
(HVACDuctSize), `CHW_Supply_/CHW_Return_/Condensate_Drain_Diameter` (PipeSize), `Chilled_Water_Flow`,
`Water_Pressure_Drop`, `Total_`/`Sensible_Cooling_Capacity`, `Voltage`, `Power_Consumption`,
`Full_Load_Ampere`, `Phase`.

**Do not reuse `Neck_Width`/`Neck_Height` from `Air_terminals` for equipment duct connections** even
though the type matches — a shared parameter is the same column everywhere, so FCU sizes would appear in
air-terminal schedules. Matching data type is not the same as matching meaning.

**Naming in this file is `Title_Case_With_Underscores`** — match it, or the new ones sort and read as
foreign.

### Instance vs type, for equipment

What varies per placed unit is instance; what describes the product is type.

| Instance | Type |
|---|---|
| the air flows (designed + actual), `External_Static_Pressure`, `Air_Change_Rate` | dimensions, clearances, duct/pipe sizes |
| `Chilled_Water_Flow`, `Water_Pressure_Drop` | cooling capacities, electrical ratings |
| **`Serial_Number`** — every physical unit has its own | `Model_Number`, family attributes, `Material` |

**Older equipment families here use ZERO shared parameters** — everything family-local or built-in, so
none of that data schedules across families or exports to COBie. Build new families shared-parameter
first.

## Family metadata: what to fill without asking, and what to refuse (2026-08-24)

Settled on `TRG_MECH_EQP_Fan Coil Unit_FCU_R0`. These need no submittal and no question:

| Parameter | Value | Derived from |
|---|---|---|
| `Family_Created_By` | **`Ajmal PS`** | his own words — matches his git author name |
| `Family_Revision` | `R0` | the `_R<n>` suffix already in the family name |
| `Family_Created_Date` | `2026-08-24` | **ISO order** — `24/08/2026` sorts wrongly in a schedule |
| `Family_Status` | `WIP` | ISO 19650 CDE stage (WIP / Shared / Published / Archive) |
| `Family_Source` | `In-House` | built here, not manufacturer-supplied |
| `Abbreviations` | `FCU` | the abbreviation field of the family name |
| `Filter_By` | `FCU` | matches `Abbreviations`; drives view filters |

**A stock Mechanical Equipment template carries no usable material** — only Revit's system materials
(Default, Glass, Poche, Ceilings…). A casing material has to be created:
`Material.Create(doc, "Galvanised Steel Sheet")`, mid-grey `Color(168,170,173)`, then assign the id to
the `Material` parameter. Do not point a casing at `Default`.

### The three-bucket rule for "fill all the parameters"

1. **Fill** — family metadata above, `Description` (his `<Device in words>` convention), `Assembly Code`,
   `Default Elevation`.
2. **Ask** — regional/office standards he answers in one line: `Voltage` and `Phase` (Qatar: 240 V single
   phase small, 415 V three phase larger), and the five `Clearance_*` maintenance distances.
3. **Refuse** — `Manufacturer`, `Model`, `Model_Number`, `Cost`, `URL`, every dimension, capacity and
   connection size. Product facts; inventing them puts false data into schedules and submittals.

**Leave `Type Comments` empty rather than pad it.** Restating `Description` is noise, and the only
content that would add anything (service type, 2-pipe vs 4-pipe) is unconfirmed.

### Instance parameters must stay empty in the family

Whatever is typed into an instance parameter in the Family Editor becomes the **default for every unit
placed in every project**, and is wrong for nearly all of them. On this family that is all six air flows,
`Air_Change_Rate`, `Chilled_Water_Flow`, `Water_Pressure_Drop`, `External_Static_Pressure` and
`Serial_Number`. When asked to "fill everything", say why these are excluded rather than filling them.

## Making an existing plane skeleton parametric — working order (2026-08-24)

Proven end to end on `TRG_MECH_EQP_Fan Coil Unit_FCU_R0`: named reference planes and shared parameters
already existed, geometry did not. Six-size flex test passed on the solid's own bounding box.

**1. Set the parameter values BEFORE labelling any dimension.** `Unit_Length` etc. arrive at **0** when
freshly added. Label a 1000 mm dimension while the parameter reads 0 and Revit obeys the parameter —
**the planes collapse to zero and the skeleton is destroyed**. Write the values, then dimension.

**2. EQ chains before the overall dimension.** A three-reference dimension (Left · Centre · Right) with
`Dimension.AreSegmentsEqual = true` is what keeps the box centred as it grows. Without it the box
resizes off one edge. One per axis, in the **plan** view.

**3. Overall dimensions, then `Dimension.FamilyLabel = familyParameter`.** Left↔Right in plan,
Front↔Back in plan, base↔top in an **elevation** — a height dimension cannot be drawn in plan.
`ReferencePlane.GetReference()` supplies each end.

**4. Extrusion on the HORIZONTAL base plane.** `SketchPlane.Create(doc, horizontalRefPlane.Id)` hosts the
sketch on the plane itself. A **vertical** sketch plane works equally well — face
`.Reference` values populate on both (proven 2026-08-25, 6 of 6 faces on a side-sketched spigot).
Sketch on whichever face the solid actually grows from.

**5. Lock the four side faces with `NewAlignment(planView, refPlane.GetReference(), face.Reference)`.**
Get faces via `ext.get_Geometry(new Options { ComputeReferences = true })` and match on `FaceNormal`.
Without this the planes move on a parameter change and **the box stays put** — it looks parametric right
up until it silently is not.

**6. Height: associate, do not align.** `fm.AssociateElementParameterToFamilyParameter(
ext.get_Parameter(BuiltInParameter.EXTRUSION_END_PARAM), unitHeightParam)`. Aligning the top face to
`Casing Top` **as well** double-drives the height and risks an over-constraint. Pick one — the
association is simpler and it held across all six test sizes.

**7. Material the same way** — associate `MATERIAL_ID_PARAM` to the family's `Material` parameter rather
than setting an id, so the type controls it. A stock template has no metal material; create one first.

### The flex test that actually proves it

Read the **solid's `get_BoundingBox(null)`**, not the reference planes — planes moving proves the
dimensions work, not that the geometry followed. Check three things per size: the measured size matches,
`Min.X + Max.X == 0` and `Min.Y + Max.Y == 0` (still centred), and `Min.Z == 0` (base did not drift).
Use genuinely different non-square sizes plus one square one — a square test alone hides a length/width
swap. Reset to the starting size afterwards.

## The template's own horizontal plane is a defect waiting to happen (2026-08-24)

A stock Mechanical Equipment template ships its horizontal Z=0 plane as:

- **`Name` parameter empty** — the "Reference Plane" shown in the UI is Revit's fallback label, not a
  name. `rp.Name` returns it anyway, so a name check must read `BuiltInParameter.DATUM_TEXT`, not
  `rp.Name`, or every unnamed plane looks named.
- **`Is Reference` = Not a Reference.**

That second one matters because **this plane is the underside of the equipment** — a body extrusion
sketched at Z=0 sits on it. Left as shipped, a project cannot dimension or lock to the base of the unit,
which for ceiling-suspended equipment is the face most often constrained. It is invisible until someone
tries, long after the family ships.

Fix while building, not later: name it `Casing Bottom`, set `Is Reference` to **Bottom (6)**, and move it
into the casing subcategory rather than leaving it grouped with the centre lines — it is a face of the
box, not a centre line.

**Renaming and re-referencing it is safe even with geometry already hosted on it.** The extrusion's
sketch plane, the four face alignments and the labelled dimensions all survived; a 1600x850x320 flex
straight afterwards came back exact with the base still at Z=0. Constraints bind to element ids, not
names. **Re-flex after the change anyway** — cheap, and it is the only thing that proves it.

Leaving the body extrusion with **no subcategory** is normal Revit practice and not a defect — but
**Ajmal wants one anyway**, asked for unprompted the same day: *"now can you add this box it menas fcu
subcategory"*. Give equipment geometry a named subcategory under its own category by default; do not
report "no subcategory" as acceptable and leave it. See the note below for the shape.

## Materials in a family: four traps, all hit in one session (2026-08-24)

`Material.Create(doc, name)` gives a **stub**, not a material. Fresh out of the call it has a colour and
nothing else: `MaterialClass` and `MaterialCategory` read **`Unassigned`**, there is **no appearance
asset** (so it renders flat, no metal), no surface or cut pattern, and every identity field is empty. It
looks finished in a shaded view and is not. Always set class, description and a cut pattern too.

### 1. You cannot copy anything between a family and a project

`ElementTransformUtils.CopyElements` from a project document into a family document fails with a modal
**"Can't copy between Family and Project."** It is a hard Revit rule, so the tempting shortcut of
borrowing a good material or appearance asset out of an open project **cannot work** — do not plan
around it.

### 2. A CAUGHT exception still poisons the whole transaction

The failed copy above was wrapped in try/catch, the catch ran, five further `SET` operations reported
success and `t.Commit()` was called with no error — **and none of it persisted.** The read-back showed
the old name and the old class. Revit had already marked the transaction failed.

**Catching an exception does not make a transaction safe.** Never continue writing after a caught Revit
exception inside the same transaction — abandon it, start a fresh one, and *verify by reading back*.
"Commit did not throw" is not evidence anything was written.

### 3. `AppearanceAssetEditScope` needs an explicit Transaction around it

Used as documented — on its own, outside a transaction — every commit throws
`InvalidOperationException: EditScope cannot be closed, there is no opened transaction`, twice, including
in complete isolation. The property writes appear to work and are silently discarded.

Wrap it and it works:

```csharp
using (var t = new Transaction(doc, "tint")) {
    t.Start();
    using (var scope = new Autodesk.Revit.DB.Visual.AppearanceAssetEditScope(doc)) {
        Asset editable = scope.Start(assetElemId);
        (editable.FindByName("metal_color") as Autodesk.Revit.DB.Visual.AssetPropertyDoubleArray4d)
            .SetValueAsColor(new Color(168,170,173));
        (editable.FindByName("metal_finish") as Autodesk.Revit.DB.Visual.AssetPropertyInteger).Value = 1;
        scope.Commit(true);
    }
    t.Commit();
}
```

Everything in `Autodesk.Revit.DB.Visual` must be **fully qualified** — `AssetType`,
`AssetPropertyDoubleArray4d`, `AssetPropertyInteger`, `AssetPropertyBoolean` are all outside the
namespaces the bridge supplies, and the compiler rejects the bare names.

### 4. The shipped asset library has no friendly names

`Application.GetAssets(Autodesk.Revit.DB.Visual.AssetType.Appearance)` returns ~3,088 assets, but names
like *Stainless - Satin* or *Semi-Polished* are **not** in it — those are appearance asset *elements*
already instantiated inside a project, not library assets. The library ships generic schemas:
**`Metal`**, `MetallicPaint`, `PrismMetal`, plus hundreds of `Metal-001`-style raw FBX entries.

Build from the base schema: `AppearanceAssetElement.Create(doc, name, metalAsset)`, then tint it via the
edit scope above. The `Metal` default is RGB(153,153,153) with `metal_finish` 0.

### Ajmal's material naming decision — do not "correct" it

Offered `Metal - Galvanised Steel Sheet` (matching a broad-family-first pattern:
`Insulation - Duct Wrap`, `Lining - Textile`) with the reasoning that materials are project-wide and an
equipment suffix fragments takeoffs into `- FCU` / `- AHU` / `- Duct` duplicates. **He chose
`Galvanised Steel Sheet - FCU` anyway.** His call, made with the trade-off stated. Follow the same shape
for the next equipment material rather than re-arguing it.

## Never cite an open project as evidence of his standards (2026-08-24)

A document called `Project1`/`Project2` with ~98 materials and a full spread of `Insulation - Duct Wrap`,
`Lining - Textile`, `Steel, Carbon` **is Revit's default template content, not the office library.**
Ajmal's own words when it was quoted back at him: *"do not look at the project 1 that a empy project i
jst open"* — a blank project opened as scratch while working on a family.

It was used twice in one session as though it were house standard: once to argue a material naming
convention, once to claim "7 of 7 metals have no surface pattern". Both readings were of Autodesk's
out-of-the-box template. The naming recommendation he then declined had been built on it.

**Ask what a document is before treating it as evidence.** A project is only a standards source if he
says it is one, or the path shows it is a real job. Distinguishing marks of a scratch file: title
`ProjectN`, **`PathName` empty (never saved)**, and content that matches the default template exactly.
`Project2` had all three and was quoted anyway.

Genuine sources for his standards, in order: **what he says**, the shared-parameter file, the family
library folder, `Revit_Family_Naming_Guide.docx`, and this Brain. A blank project is none of them.

## `UseRenderAppearanceForShading` makes the shaded colour uncontrollable from the API (2026-08-24)

Turning it **on** hands the shaded colour to Revit, which derives it from the appearance asset — and that
derived value is **cached and never recomputed from an API-side asset edit**. Measured on a `Metal`
shader: the shaded colour sat at RGB(244,244,244) and did not move for **any** of

- `metal_color` swept 168 -> 28 (nine values)
- `common_Tint_color` + `common_Tint_toggle` on
- `metal_type` across its whole range 0-5

each inside a committed `AppearanceAssetEditScope` with `doc.Regenerate()`. The shader properties do
change and read back correctly; only the derived shading colour is frozen. It presumably recomputes when
the Material Browser is opened, which the bridge cannot do.

**So the two are mutually exclusive from here:**

| Want | Setting |
|---|---|
| Control the **Shaded** view colour | `UseRenderAppearanceForShading = false`, then set `Material.Color` |
| Accurate metal in **Realistic/Rendered** | leave the appearance asset assigned — it works either way |

The appearance asset still drives Realistic and Rendered views with the flag off, so **off is the better
default**: full control of the working view, no loss anywhere else. A metal shader derives to near-white
(244) because metals are highly reflective, so switching the flag on makes equipment casing read almost
white in Shaded — rarely what anyone wants.

Settled on this family: shaded `Material.Color` RGB(150,152,155), shader `metal_color` RGB(140,142,145),
`metal_finish` 1 (brushed), `metal_type` 0, tint off.

**Clean up after probing.** Sweeping `metal_type` and toggling the tint to find the driver left the
shader on `metal_type` 5 with a tint enabled. A probe that writes must restore, or the "diagnosis" leaves
the model worse than it found it.

## Give the equipment solid its own subcategory (2026-08-24)

His preference, stated directly. On `TRG_MECH_EQP_Fan Coil Unit_FCU_R0` the casing extrusion went into a
subcategory named **`FCU`** — the equipment abbreviation, matching the `_FCU_` field of the family name
and the `Abbreviations` parameter.

```csharp
var parent = doc.Settings.Categories.get_Item(BuiltInCategory.OST_MechanicalEquipment);
var sub    = doc.Settings.Categories.NewSubcategory(parent, "FCU");
ext.get_Parameter(BuiltInParameter.FAMILY_ELEM_SUBCATEGORY).Set(sub.Id);
```

Note it is **`FAMILY_ELEM_SUBCATEGORY`** for geometry, not `CLINE_SUBCATEGORY` — that one is reference
planes only. A stock Mechanical Equipment template ships exactly one subcategory, `Hidden Lines`.

Why it is worth doing: a project can then switch, recolour or reweight the casing on its own line in
V/G without touching other equipment, and when the coil, fan, filter and drain pan arrive as separate
solids each can take its own subcategory and be controlled independently. Assigning the subcategory does
**not** disturb an existing material association — verified, the parameter link survived.

New subcategories arrive on defaults (black, weight 1, solid). **Leave the graphics to him** — same rule
as reference-plane extents: how it reads on screen is his call, not something to set unasked.

### Renaming a subcategory: `Category.Name` is read-only, the backing element is not

`subcategory.Name = "..."` fails to compile in Revit 2020 — `CS0200: Property or indexer 'Category.Name'
cannot be assigned to`. The workaround is not delete-and-recreate:

```csharp
fdoc.GetElement(sub.Id).Name = "Fan Coil Unit";   // inside a transaction
```

A subcategory is backed by a real `Element` carrying the same `ElementId`. Renaming through it **keeps
that id**, so every element already assigned to the subcategory stays assigned — nothing needs
re-pointing, and material associations survive untouched. Verified on the FCU casing: `FCU` ->
`Fan Coil Unit`, id 4490 throughout, box still on it and still parameter-driven.

Delete-and-recreate would work too but changes the id and orphans every assignment — only reach for it
if the element rename genuinely fails.

**His naming preference: spell subcategories out in full.** `FCU` was created at his request and then
immediately corrected to **`Fan Coil Unit`** — *"FCU make it full nale fan coil unit"*. The abbreviation
belongs in the family filename and the `Abbreviations` parameter; the subcategory gets the full words.

## Settled graphics for an equipment family (2026-08-24)

Worked out on `TRG_MECH_EQP_Fan Coil Unit_FCU_R0` by his direction. Reuse these on the next one rather
than re-deriving.

| Where | Value |
|---|---|
| Reference planes -> `Center Lines` | red RGB(255,0,0), pattern `Aligning Line`, weight 1 |
| Reference planes -> `Casing Outside` | green RGB(0,127,0), pattern `Aligning Line`, weight 1 |
| Mechanical Equipment -> `Fan Coil Unit` | **RGB(90,92,95)**, **weight 3** |
| Material shaded colour | **RGB(90,92,95)** — same as the subcategory line |
| Shader `metal_color` | **RGB(90,92,95)** — so Realistic agrees with Shaded |
| Material cut pattern | `<Solid fill>` RGB(110,112,115) — deliberately *lighter* |

**The subcategory line colour and the material colour are kept identical** — his instruction: *"take frm
the materical color to this subcategrory color"*. Read it live off `Material.Color` rather than retyping
the numbers, so they cannot drift apart.

**Weight 3 is his standing maximum** for MEP line work, so an equipment outline sits at the top of that
range, not above it.

**Keep the cut pattern one step lighter than the outline.** The cut fill sits behind the outline; a
lighter fill under a darker, heavier line keeps the edge reading as the strong element in section. Do not
"tidy" them to the same value.

His first instinct was the material colour straight onto the line — RGB(150,152,155) at weight 1 — which
draws faint and prints faint. Flagging that led to the darker/heavier values above. **Worth saying out
loud when a light material colour is about to become a line colour.**

**`SetLineWeight(w, GraphicsStyleType.Cut)` throws on an equipment subcategory** — projection only. Wrap
it, do not let it kill the transaction, and do not report a cut weight as set.

### A new reference plane cannot be dimensioned in the transaction that created it

`NewDimension` against a plane made moments earlier in the same transaction throws:

> `The references are not geometric references. Parameter name: references`

`ReferencePlane.GetReference()` returns something, but it is not yet a *geometric* reference — the plane
has to be regenerated into the document first. `doc.Regenerate()` inside the same transaction is **not**
enough; the transaction has to close.

**Split it in two:** transaction 1 creates the plane and sets its name, `Is Reference` and subcategory;
transaction 2 creates the dimension. Re-fetch the plane by id in the second one. The same applies to
EQ chains and to labelling.

This is why the working order for a parametric skeleton is planes first, dimensions second — not a style
preference, a hard constraint.

### Centre planes: one shared, never two coincident

Supply and return air connectors on a concealed FCU sit at the **same** height, so they take **one**
horizontal `Center (Elevation)` plane between them, EQ-chained Bottom = Centre = Top so it tracks
mid-height as `Unit_Height` changes. Verified at 245 / 400 / 180 mm.

Building "one plane for supply, one for return" puts two planes at the same Z. Coincident planes are
nearly impossible to select and ambiguous to dimension to. **Only split them when the two connectors are
genuinely at different heights** — and say why before doing it.

`Is Reference` for it is **7 = Center (Elevation)**.

### His plane subcategory scheme

Three groups, each its own colour, all on `Aligning Line`:

| Subcategory | Colour | Holds |
|---|---|---|
| `Center Lines` | red RGB(255,0,0) | Center (Front/Back), Center (Left/Right) |
| `Casing Outside` | green RGB(0,127,0) | the six casing faces |
| `Connectors` | blue RGB(0,0,255) | Center (Elevation), and connector-positioning planes |

He asks for a **new colour per new group** — *"use for this difrent colors also"*. When adding planes for
a new purpose, make a new subcategory rather than reusing an existing one.

## Duct spigots and air connectors on an equipment family (2026-08-24)

Ajmal's requirement, in his words: *"we need to add slmall pice of box and from thaere we need to add the
connector"* — **do not put a duct connector straight on the casing face.** A duct connects to a spigot,
and his existing FCU is built that way (25 mm necks front and back). Front = supply, back = return.

### Sketch the spigot on the casing side face and extrude sideways

**Corrected 2026-08-25.** This first said to build the spigot as a Z-extrusion off the horizontal base
plane, on the belief that face references needed a horizontal sketch plane. **That was wrong.** Ajmal
corrected it: *"the workplance sarting from the casing bottom not like that need to stanr from the main
box side and extrude to side ways"*.

Sketch on `Casing Front` / `Casing Back` and extrude along the plane normal. **6 of 6 planar faces carry
usable references either way**, so use the plane the solid actually grows from — the sketch profile is
then the duct opening itself, and the family reads correctly to anyone who opens it.

### Planes it needs (all in the blue `Connectors` subcategory)

| Plane | Driven by |
|---|---|
| `Air Opening Left` / `Air Opening Right` | `Supply_Air_Width`, EQ about `Center (Left/Right)` |
| `Air Opening Bottom` / `Air Opening Top` | `Supply_Air_Height`, EQ about `Center (Elevation)` |
| `Supply Spigot Face` / `Return Spigot Face` | `Duct_Spigot_Depth` off `Casing Front` / `Casing Back` |

Then lock **all six** faces of each spigot — left, right, bottom, top, outer to the spigot face, inner to
the casing face. 12 of 12 locked on this family. Vertical faces align in the **plan** view, horizontal
faces in an **elevation**.

### Connector sizing

`ConnectorElement.CreateDuctConnector(doc, DuctSystemType, ConnectorProfileType.Rectangular,
face.Reference)`, then **associate** its `Width` and `Height` parameters to the family parameters. Do not
set the numbers directly — associate, and they follow.

**Flow Direction defaults to `Bidirectional` (0) and that is wrong for equipment.** Set it explicitly:
**1 = In, 2 = Out** (probed and confirmed). Supply air = **Out**, return air = **In**.

**A connector cannot be re-hosted.** Moving one from the casing face to a spigot face means delete and
recreate, which loses the parameter associations and flow direction — rebuild both and verify.

### Where the opening size comes from when there is no submittal

Derived from the casing:

```
Air_Width  = Unit_Length - 150     (75 mm casing flange each side)
Air_Height = Unit_Height -  50     (25 mm top and bottom)
```

On a 1000 x 700 x 245 casing that gives 850 x 195. Set as plain values,
**not formulas**, so real submittal numbers can be typed straight over them.

## Supply and return get SEPARATE planes, dimensions and colours (2026-08-24)

His correction after the air side was first built with one shared opening: *"we need to separat and
dimsion also for this back and frent that supply and return also and color also difrent"*.

**Do not share opening planes between supply and return.** One set of `Air Opening Left/Right/Bottom/Top`
serving both spigots means the two can never differ in size — and real units very often have a larger
return than supply. Build two sets from the start:

| Subcategory | Colour | Contents |
|---|---|---|
| `Center Lines` | red RGB(255,0,0) | Center (Front/Back), Center (Left/Right) |
| `Casing Outside` | green RGB(0,127,0) | the six casing faces |
| `Connectors` | blue RGB(0,0,255) | `Center (Elevation)` — the one genuinely shared datum |
| `Supply Air` | cyan RGB(0,176,240) | 4 opening planes + `Supply Spigot Face` |
| `Return Air` | magenta RGB(214,0,147) | 4 opening planes + `Return Spigot Face` |

Each side gets its **own** EQ chain about the centre planes and its **own** labelled dimensions —
`Supply_Air_Width/Height` and `Return_Air_Width/Height`. Only `Center (Elevation)` stays shared, because
both connectors genuinely sit at the same height.

**A new subcategory per new purpose, with a new colour.** That is now three separate requests in one
session — he wants to tell the groups apart at a glance in the Family Editor.

### Renaming a plane does NOT re-point what is locked to it

Renaming `Air Opening Left` to `Supply Air Opening Left` left the **return** spigot still constrained to
it — alignments bind to element ids, and the id did not change. The return spigot had to be **deleted and
rebuilt** against the new return planes, connector included.

So: **decide whether two things share a plane BEFORE locking geometry to it.** Splitting afterwards costs
a full rebuild of everything on the wrong side. Cheap here (six faces and one connector), expensive once
pipe stubs and an electrical connector are also hanging off it.

### Side-extruded spigot: two API details

**`NewExtrusion` needs a positive, non-zero depth at creation.** Passing `0.0`, or a negative value to
extrude "backwards", fails with the unhelpful *"One of the conditions for the inputs was not satisfied"*.
Create with `Math.Abs(depth)`, then flip direction afterwards via the parameters:

```csharp
var e = doc.FamilyCreate.NewExtrusion(true, profile, sketchPlane, depth);   // positive
// to grow the OTHER way along the plane normal:
e.get_Parameter(BuiltInParameter.EXTRUSION_START_PARAM).Set(-depth);
e.get_Parameter(BuiltInParameter.EXTRUSION_END_PARAM).Set(0.0);
```

Both `Casing Front` and `Casing Back` come out with normal **(0,1,0)** — the *front* plane's normal does
not point forwards. Read the normal; do not assume it faces outward.

**Aligning a face can push the whole parametric chain and change your driving parameters.** Locking the
spigot's top face propagated up through the opening plane, the EQ'd `Center (Elevation)`, and `Casing
Top` — leaving the family **internally consistent but 30 mm taller than intended** (`Unit_Height` 245 ->
275, air heights 195 -> 225). Nothing errored.

**So after any batch of alignments, read the driving parameters back and reset them.** The family was not
over-constrained — setting the values back held, and it re-flexed cleanly at 245 / 320 / 200. But a
"12 of 12 locked, 0 failed" result is not evidence the numbers survived.

## Clearance zone with a working visibility switch (2026-08-25)

His request: *"can we add the crearance also for that referance plane… and i need to contril that
visibility also"*. Two separate things, and the distinction matters:

**Reference planes are Family-Editor-only.** They never appear in a project, so "control the visibility"
cannot mean the planes. It means a **solid** the project can switch on and off. Build both:

1. **Six clearance planes** — Left/Right/Front/Rear/Top/Bottom, offset from the casing faces, each
   dimensioned to its `Clearance_*` shared parameter. Own subcategory `Clearance`, **orange
   RGB(255,153,0)**. Set them as **Weak Reference (14)** — they are datums, not faces anything should
   snap to from a project.
2. **A clearance box** locked to those six planes, on its own `Clearance` subcategory under Mechanical
   Equipment, with a transparent material.

**The switch:** associate the extrusion's `BuiltInParameter.IS_VISIBLE_PARAM` to a **Yes/No family
parameter** — `Show_Clearance`, added as an **INSTANCE** parameter so each placed unit can show or hide
its own zone. Verified toggling Yes → No → Yes.

**Set the clearance values before creating the planes.** They arrive at 0, and a plane at zero offset is
coincident with the casing face — unselectable and ambiguous, the same trap as the bottom plane.

Declared starting values for a ceiling-concealed FCU (his standard not yet stated):
**front 300, rear 300, side 300, top 100, bottom 450 mm** — the bottom is largest because a concealed
unit is serviced from below through the ceiling.

**Make the material genuinely transparent** (80%) or the box hides the unit it surrounds.

Two consequences of it being a real solid, both worth telling him: it **will clash-detect**, which is the
point — anything intruding on the maintenance zone shows up in interference detection; and it **will
appear in quantity schedules** unless filtered out by subcategory.

### The family's six plane subcategories

| Subcategory | Colour |
|---|---|
| `Center Lines` | red RGB(255,0,0) |
| `Casing Outside` | green RGB(0,127,0) |
| `Connectors` | blue RGB(0,0,255) |
| `Supply Air` | cyan RGB(0,176,240) |
| `Return Air` | magenta RGB(214,0,147) |
| `Clearance` | orange RGB(255,153,0) |

A new purpose gets a new subcategory and a new colour, every time — that is now four separate requests
from him in one session.

## Pipe connectors and stubs (2026-08-25)

### `AssociateElementParameterToFamilyParameter` fails SILENTLY on a wrong parameter name

The worst bug of the session. Code looked up the connector's `"Radius"` parameter, found nothing, left
the variable null, and skipped the association **without raising anything**. The run logged "connector
created, flow In" and looked completely successful. All three pipe connectors sat at Revit's default
**609.6 mm (2 ft)** diameter, associated to nothing, and only a raw read of the parameter found it.

**A Revit pipe connector exposes `Diameter`, not `Radius`.** (Older families may show `Radius`, so it
varies — read the connector's actual parameter list instead of assuming either.)

**Always verify an association by reading `fm.GetAssociatedFamilyParameter(param)` back.** "The call did
not throw" proves nothing when the guard was `if (p != null)`. And do not read connector sizes with
`AsValueString()` — it returns **empty string** for these; use `AsDouble()`.

Rectangular duct connectors also carry an unused `Diameter` field reading 610 mm. Harmless, but it will
mislead a size dump — read `Width`/`Height` for those.

### Height-safe pipe positions

His requirement: *"lock it so it will not be issue if adjuset the unit heaght"*. The answer is **which
face each pipe is anchored to**:

| Pipe | Anchored to | Parameter |
|---|---|---|
| CHW supply, condensate drain | **Casing Bottom**, offset up | `Pipe_Low_Offset` |
| CHW return | **Casing Top**, offset down | `Pipe_High_Offset` |

Anchor the low pipes to the bottom and the high pipe to the top and they stay inside the casing at any
height — verified 180 / 200 / 245 / 320 / 400 mm, never escaping, never crossing. Anchoring everything to
one face fails as soon as the unit gets short.

Same idea across the width: supply measured from `Casing Front`, drain from `Casing Back`, return on
`Center (Front/Back)` — all positive dimensions, all following their own face.

### Round stubs

Sketch a circle on the casing side plane as **two arcs** (`Arc.Create(ctr, r, 0, PI, ...)` and
`PI..2*PI`) with xAxis `(0,1,0)`, yAxis `(0,0,1)` for a Y-Z plane circle. Extrude along the normal.

**`Casing Right` has normal (−1,0,0)** — like `Casing Front`, it does not point outward. A positive
extrusion goes **into** the casing, and the subsequent alignment then fails with *"The two references are
not geometrically aligned"*, which reads as a locking problem but is really a direction problem. Create
with a positive depth, then set `start = -len, end = 0`.

Only the stub's **flat outer end** can be locked to a plane; the curved side has no planar face. The
stub's position follows because its sketch plane is a reference plane, and its diameter follows through
the connector, not the geometry.

## Name planes after the connector they serve, one pair each (2026-08-25)

His correction: *"Pipe Low Centre hight nit like that nale like the connectrrs same nale and add for the
drainage also same and vertical line also need"*.

**Do not build one generic plane shared by several connectors.** `Pipe Low Centre` serving both the CHW
supply and the drain meant the two could never sit at different heights. Every pipe connector gets its
own **pair** of planes, named after it:

| Connector | Height plane (horizontal) | Centre plane (vertical) |
|---|---|---|
| CHW supply | `CHW Supply Height` — up from **Casing Bottom** | `CHW Supply Centre` — in from **Casing Front** |
| CHW return | `CHW Return Height` — down from **Casing Top** | `CHW Return Centre` — in from **Casing Front** |
| Condensate drain | `Drain Height` — up from **Casing Bottom** | `Drain Centre` — in from **Casing Back** |

Six parameters to match: `CHW_Supply_Height/Offset`, `CHW_Return_Height/Offset`, `Drain_Height/Offset`.
Every one dimensioned off a **`Casing Outside`** plane, never off another pipe plane — that is what makes
length, width and height changes all safe. Verified across 800x550x180 to 1800x1100x320.

**Watch for a coincident plane when choosing offsets.** `CHW_Return_Offset` of 350 on a 700-wide casing
lands exactly on `Center (Front/Back)`. Moved it to 300.

### Renaming and re-labelling instead of rebuilding

A plane can be renamed (`rp.Name = ...`) and a dimension re-pointed at a different parameter
(`dim.FamilyLabel = otherParam`) **without touching the geometry** — the constraint survives because it
binds to element ids. That turned "rename these planes and split one parameter into three" into a
metadata edit rather than a rebuild. Find the dimension to re-label by reading `dim.FamilyLabel` and,
where two share a parameter, by checking which planes `dim.References` touches.

### Read the plane, never hardcode the coordinate

The bug that survived a full flex test: when rebuilding the three stubs, the CHW return was created with
a literal `0.0` for its Y instead of `AT("CHW Return Centre")`. It sat on nothing, stayed at Y=0 at every
size, **and the other two tracked perfectly** — so the run looked right until the expected values were
computed and compared per size.

**A flex test only catches this if it asserts against expected numbers.** Printing positions and eyeballing
them would have passed: 0 looks like a plausible Y. Compute what each value *should* be from the input and
compare.

## Formulas: where they belong, and why `IsDeterminedByFormula` lies (2026-08-25)

**`FamilyParameter.IsDeterminedByFormula` returns `False` even when a formula is genuinely in force.**
Measured on five formula-driven parameters — every one reported `False` while behaving correctly. Two
tests that do work:

- `fp.Formula` returns the formula string (empty/null when there is none)
- **try to overwrite it** — `fm.Set(param, x)` on a formula-driven parameter is silently ignored and the
  value stays put. Setting `CHW_Return_Height` to 999 left it at 100.

Never report "formula applied" off the `SetFormula` call not throwing, and never off
`IsDeterminedByFormula`. Read the formula back and try to break it.

### The rule for which parameters get a formula

**Formula = protection. No formula = a number he types.** A formula makes the parameter **read-only** in
the Family Types dialog, so putting one on anything that comes off a manufacturer's submittal locks him
out of his own data.

| Formula | Leave typeable |
|---|---|
| positions that must stay inside the casing | `Unit_Length` / `Width` / `Height` |
| `CHW_Return_Height = Unit_Height - 50` | all four air duct sizes |
| `Drain_Offset = Unit_Width - 200` | all three pipe diameters |
| radius = diameter / 2 for pipe connectors | the other pipe offsets, which are already safe |

Asked to "add the formula for what ever posible", the right answer is **not** every parameter — say which
ones you are leaving free and why.

### The trade-off measuring everything from one datum

He wanted all six pipe offsets read from `Casing Bottom` and `Casing Front` for consistency
(*"return heaight mention from the top can you mention from the bottom"*, then the same for the drain).
That reads better but **removes the self-protection**: a height measured down from the top can never
exceed the casing, while the same height measured up from the bottom escapes as soon as the unit is
shorter than the value.

Formulas restore both — the value still *reads* from the bottom, and still tracks the top. That is the
resolution when consistency and safety conflict: keep his datum, add the formula.

Units in a formula string need the unit suffix: `Unit_Height - 50 mm`, not `Unit_Height - 50`.

## `STI_ME_FCU_Fan Coil Unit.rfa` is NOT a reference standard (2026-08-25)

His words: *"STI FAMILY IS NOT THAT YOU CAN REFER THATS ONLY BASIC ONE THAT I CREATED BEFORE SO DONT
MENTION THAT"*.

It is an early family he built himself, not an office standard and not a worked example. Over one
session it got used to justify the air-opening derivation, the pipe diameters, the spigot arrangement,
the `Preset` flow configuration and the connector-description format — all presented to him as "matching
your existing unit", which gave the choices an authority they never had.

**Do not cite it, and do not open it to copy from.** If a value is needed and he has not given one, say it
is a placeholder chosen on engineering grounds and name those grounds — do not borrow legitimacy from a
file that has none.

This is the **second** source in one session mistaken for a standard, after a blank `Project2` whose
Revit-default content was read as his office library. Same failure both times: **a file being open in
Revit says nothing about its authority.** Ask, or check whether he ever named it as a standard.

Genuine sources, in order: **what he says**, the shared-parameter file, the family library folder,
`Revit_Family_Naming_Guide.docx`, and this Brain.

## Connector Loss Method and Pressure Drop (2026-08-25)

**`Pressure Drop` is read-only while `Loss Method = Not Defined`,** and becomes writable — and
associable to a family parameter — the moment Loss Method is set to **Specific Loss**. Proven by
switching it inside a rolled-back transaction and watching the writable-parameter set gain exactly
`Pressure Drop`, then associating it to `Water_Pressure_Drop` successfully.

So "I cannot drive Pressure Drop from a parameter" is a symptom of Loss Method, not a limitation.

Options measured in Revit 2020:

| Air connector | Pipe connector |
|---|---|
| 0 Not Defined · 4 Specific Loss · 6 Coefficient | 0 Not Defined · 1 K Coefficient from Table · 2 K Coefficient · 3 Cv Coefficient · 4 Specific Loss · 5 Use Definition on Type |

### Leave it Not Defined until real figures exist

`Not Defined` means **unknown**. `Specific Loss` with a value of 0 means **"this equipment has zero
pressure drop"** — a false statement that silently corrupts any system pressure calculation downstream.
An honest blank beats a confident wrong number.

Turn it on only when the coil pressure drop / fan external static are actually known, then: Specific
Loss + associate `Pressure Drop` to `Water_Pressure_Drop` or `External_Static_Pressure`.

**The air side may never need it** — an FCU's fan is a pressure *source*, not a duct loss, so external
static belongs on the equipment schedule rather than on the connector.

## Connector setup checklist for equipment

What actually needs setting on each connector, beyond creating it:

| Setting | Notes |
|---|---|
| `System Classification` | set by the create call |
| `Flow Direction` | **defaults to Bidirectional (0), which is wrong** — 1 In, 2 Out |
| size | **associate** Width/Height, or Diameter, to family parameters — never set the number |
| `Connector Description` | `<Service>,<Flow>` — e.g. `Chilled Water,In`, `Supply Air,Out`, `Drain,Out` |
| `Flow Configuration` | 0 Calculated · 1 Preset · 2 System · 3 Fixture Units. **Preset** for equipment with a scheduled flow; Calculated where the system works it out |
| `Flow` | associate to the family flow parameter, otherwise the unit declares **zero demand** to every system it joins |
| `Loss Method` | leave `Not Defined` unless real figures exist — see above |
| `Utility`, `Allow Slope Adjustments` | defaults are fine |

**The `Flow` association is the one most easily missed.** Everything looks correct — right size, right
system, right direction — and the equipment still reports 0 L/s to the system because nothing is wired
to it.

## Electrical control box and connector (2026-08-25)

The last service on an FCU. Built on the **left** casing face — the right is crowded with three pipe
stubs, the left is clear.

**`Casing Left` has normal (−1,0,0), which for the LEFT face points OUTWARD** — so a positive extrusion
grows away from the unit and needs **no flip**. This is the opposite of `Casing Right` and `Casing Front`,
which also carry (−1,0,0) / (0,1,0) and *do* need flipping. **Always read the normal and work out which
way positive goes; the plane's name tells you nothing.**

Four parameters, all dimensioned off `Casing Outside` and `Center (Elevation)`:

| Parameter | Measured |
|---|---|
| `Control_Box_Depth` | `Control Box Face` → `Casing Left` |
| `Control_Box_Offset` | `Casing Front` → `Control Box Front` |
| `Control_Box_Width` | `Control Box Front` → `Control Box Back` |
| `Control_Box_Height` | box bottom → top, **EQ-chained about `Center (Elevation)`** so it stays centred |

Five planes in a yellow `Electrical` subcategory, all six box faces locked.

### The connector

```csharp
ConnectorElement.CreateElectricalConnector(doc,
    Autodesk.Revit.DB.Electrical.ElectricalSystemType.PowerBalanced, face.Reference);
```

Then: `Number of Poles` = 1, `Connector Description` = `Power,In`, and **associate `Voltage` →
`Voltage`** and **`Apparent Load` → `Power_Consumption`**. Both associations take cleanly — unlike the
pipe connectors, the electrical one uses the parameter names you would expect.

Note it has **no size** to associate — an electrical connector carries load and voltage, not a diameter.

### The finished family, for reference

`TRG_MECH_EQP_Fan Coil Unit_FCU_R0`: 8 solids (casing, 2 air spigots, 3 pipe stubs, control box,
clearance zone), **6 connectors** (supply air, return air, CHW supply, CHW return, condensate drain,
power), 37 reference planes in **8 coloured subcategories**, 66 parameters of which 56 shared, 2
geometry-protecting formulas. Verified 600x400x150 through 2000x1200x500.

## Electrical connector: the wiring that is easy to get wrong (2026-08-25)

Ajmal caught this himself: *"POWER FACTOR IS NOT CONNECTED AND NUMBER OF POLES ... AM A MECHANICAL NOT
ELECTRICAL SO CAN YOU CHEK THAT"*. He was right on every point, and there was a worse error underneath.

### Apparent Load is VA. Power is W. They are NOT interchangeable

`Apparent Load` on the connector is `ElectricalApparentPower` (**VA**). A `Power_Consumption` parameter is
`ElectricalPower` (**W**). **Revit accepts the association between them without complaint** and the family
looks correct.

`W = VA x power factor`. Wiring a watts value into the VA field under-declares the supply demand by
(1 - PF) — at PF 0.85 that is **15% low**, and the electrical engineer sizes the circuit accordingly.

**Create a separate `Apparent_Load` (ElectricalApparentPower) parameter for the connector**, and keep the
watts parameter as a schedule field only. Check `p.Definition.ParameterType` on both sides before
associating anything electrical.

### Defaults that are wrong for equipment

| Parameter | Revit default | Should be |
|---|---|---|
| `Power Factor` | **1.0** | ~0.8 for a motor — nothing achieves 1.0 |
| `Load Classification` | `Other` | **HVAC** (options: HVAC, Lighting, Motor, Other, Power, Spare) |
| `Load Sub-Classification Motor` | `No` | **Yes** for fan/pump equipment |
| `Number of Poles` | 1, hardcoded | **associate to a parameter** — 1 single phase, 3 three phase |

An electrical connector has **no size** to associate; it carries voltage, load, power factor and poles.

### Revit's internal electrical units are NOT volts and VA

`UnitUtils.ConvertToInternalUnits(1.0, DisplayUnitType.DUT_VOLTS)` returns **10.76391042** — the ft²/m²
factor. Setting a raw `240.0` produced **22 V** on the connector.

**Always convert:**

```csharp
fm.Set(PAR("Voltage"),       UnitUtils.ConvertToInternalUnits(240.0, DisplayUnitType.DUT_VOLTS));
fm.Set(PAR("Apparent_Load"), UnitUtils.ConvertToInternalUnits(450.0, DisplayUnitType.DUT_VOLT_AMPERES));
```

Unitless parameters (`Power Factor`, `Number of Poles`) take raw values and are the reason the first bad
test looked half-right — those two matched while the two unit-bearing ones did not. **A propagation test
that only checks the unitless values proves nothing.**

## Named `Is Reference` values are EXCLUSIVE — assigning one twice silently demotes the first

Only one plane per family can hold **Left**, **Right**, **Front**, **Back**, **Top**, **Bottom** or any
of the three **Center** values. Give a second plane the same value and Revit moves it silently — the
original drops to **Not a Reference**, with no error and no warning.

Found on a final audit: `Casing Left` read *Not a Reference* although it had been created as **Left** and
verified as such. The cause was a copy-paste in a completely different plane — `Air Opening Left` was
created with reference value **0 (Left)** instead of **13 (Strong Reference)**, and it had quietly taken
the name. Setting `Casing Left` back to Left just pushed the problem onto the other plane.

**The symptom appears on a different element from the cause**, so chasing the plane that reads wrong
never finds it.

### The rule

| Plane role | Is Reference |
|---|---|
| The six casing faces | the matching **named** value — Left / Right / Front / Back / Top / Bottom |
| The three centre planes | Center (Left/Right) / Center (Front/Back) / Center (Elevation) |
| **Everything else** — openings, spigot faces, pipe and control-box planes | **Strong Reference (13)** |
| Clearance / datum-only planes | **Weak Reference (14)** |

Named values are a scarce resource — nine of them for the whole family. **Never spend one on an internal
plane.**

### Audit it before shipping

Count planes reading `Not a Reference`; the answer should be **0** on a finished family. If it is not,
do not fix the plane that reads wrong — find which other plane has stolen its name. And **re-check after
any rebuild**: this only surfaced on the last audit of the session, long after the plane was created.
