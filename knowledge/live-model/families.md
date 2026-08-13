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
document's parameter *and geometry* state untrusted, not just "that one call failed."** After a failure
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
