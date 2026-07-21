---
name: ajtools-family-creation
description: Build a brand-new parametric Revit family (.rfa) from scratch via the AJ AI Bridge — set the family category, add FamilyParameters, build extrusion/void geometry locked to reference planes, and verify with a real multi-value resize test. Use this whenever the user has an empty or fresh family template open in Revit (Generic Model, Generic Model Face Based, or another category template) and wants Claude to build the actual geometry and parameters — "make this parametric", "create the extrusion", "build this family", "make an air terminal/motor/[equipment] family", or any request that hands over family dimensions (a table of sizes, an IEC-style spec, a "Type A / Type B" parameter matrix) and expects a working .rfa as the result. Also covers nested sub-families (a smaller family loaded into a bigger one) and multi-family builds where several family documents are open in Revit at once. Do NOT use this for querying or modifying elements already placed in the live PROJECT model (counts, sizes, placing instances, view isolation) — that's ajtools-live-model. Do NOT use this to edit the Revit add-in's own compiled source code — that's a separate codebase this Brain doesn't cover. This is specifically Family Editor authoring: the document the user has open is a family (.rfa being edited), not a project (.rvt).
---

# AJ Tools — Family Creation

Building a Revit family (.rfa) from an open, empty template — not editing the live project model
(`ajtools-live-model`) and not the Revit add-in's own compiled source (out of scope for this Brain). Same
AJ AI Bridge, completely different job: here, `Document.IsFamilyDocument` is `true`, and the goal
is real parametric geometry (extrusions, voids, reference planes, connectors) that resizes correctly from
`FamilyParameter` values — not counting/coloring/placing things in a project.

## How to work: plan, split, then execute

Never jump from "build this family" straight to one giant script. Split into visible steps, run one at a
time, verify each before the next — same discipline as every other AJ Tools skill, and more important
here than almost anywhere else, because family geometry is genuinely hard to verify blind (no easy
screenshot access most sessions — see below). A typical build, e.g. a parametric box body with a duct
neck:

1. Confirm the right family document is open and what template it started from (see "Before starting").
2. Set the family category (if it needs one, e.g. Air Terminals) — verify by reading `FamilyCategory.Name`
   back.
3. Add `FamilyParameter`s and a default `FamilyType` with real starting values — verify by reading the
   parameter list and values back.
4. Add reference planes for anything that needs to resize — verify by reading `GetPlane().Normal` back
   immediately (see the direction-vector gotcha below; a tilted plane is silent otherwise).
5. Sketch and extrude the geometry — **always on a horizontal (Z-normal) sketch plane**, never a vertical
   one (see gotcha below) — verify with a bounding-box read-back.
6. Align faces to reference planes, add EQ-dimension chains for centered resize, associate simple
   depth/size parameters directly to element parameters — verify each call succeeded (these throw loudly
   on a bad reference, which is useful).
7. **Run an actual multi-parameter resize test** — change several parameters to genuinely different,
   non-square values in one transaction, read back bounding boxes / connector properties, confirm every
   number matches and the geometry stayed centered where it should. This is the real proof, not "the API
   call didn't error." Reset to sensible defaults afterward.
8. Repeat 2–7 for the next sub-feature (a neck, a void cut, a nested sub-family) rather than trying to
   build everything in one script.

If the request hands over an ambiguous formula or a parameter name that collides with something else in
the spec (this has already happened once — a fin-depth formula that literally used the main table's
"Shaft Height" parameter, which would have produced physically absurd geometry), stop and ask — one short
question with a recommended default — rather than building the literal-but-wrong interpretation.

## Before starting

1. **Ping the bridge**: `mcp__aj-tools-aj-ai__ping`. If it fails, Revit is closed or the AJ AI
   pane's Connect AJ AI Bridge toggle is off — say so plainly, don't guess.
2. **Find the right document, don't assume the ambient `Document` global is it.** A family-creation
   session often has more than one family document open at once (a main body plus one or more nested
   sub-families). List them with `Application.Documents` and match by `.Title` — `Document.IsFamilyDocument`
   and `Document.OwnerFamily.FamilyPlacementType` tell you which template each one started from
   (`OneLevelBased` = plain Generic Model, `WorkPlaneBased` = Face Based). Confirm with the user which
   document/template corresponds to which piece of the build before writing geometry into the wrong one.
3. **Read [`live-model/families.md`](../../knowledge/live-model/families.md)**, section "Building a parametric
   family from scratch (Family Editor, via the bridge)" and its follow-on "Second build" subsection —
   this is where the hard-won gotchas live: reference-plane 3rd-argument is a direction not a point;
   extrusion face `.Reference` only populates reliably on a horizontal sketch plane; `NewAlignment` +
   EQ-dimension chains for centered parametric resize; `AssociateElementParameterToFamilyParameter` for
   simple depth/size bindings (extrusion end, connector width/height); `SetFormula` needs a current type
   to already exist; formula parameter names with spaces do NOT get quoted; any exception later in a
   `run_csharp` call rolls back everything earlier in that same call, even committed transactions; and the
   **currently unresolved** void-form-cut problem (see below). Don't re-derive any of this from scratch.
4. **Check [`scripts/recipes/`](../../scripts/recipes/)** — in particular
   [`create-parametric-box-family-with-duct-connector.cs`](../../scripts/recipes/create-parametric-box-family-with-duct-connector.cs),
   a generalized, INPUTS-driven script for the "box body + optional rectangular neck + duct connector"
   shape. Adapt it before writing an equivalent build from scratch.
5. **Check [`glossary.md`](../../knowledge/glossary.md)** for ambiguous terms, same as any other AJ Tools
   skill.

## Known open problem — void-form cuts are unverified

`NewExtrusion(isSolid: false, ...)` (a void, used for bevels/cutouts/bolt holes) has NOT been confirmed to
actually remove material from an intersecting solid — five different verification methods all showed no
volume change, and the explicit `SolidSolidCutUtils.AddCutBetweenSolids` API refuses to run on a plain
family document (it's restricted to project docs / mass / adaptive / curtain-panel families). Full detail
in `live-model/families.md`. Until this is solved: don't trust a geometry/volume query to confirm a void cut
worked — get a human visual check (ask the user to look at the family's 3D view, or request screen access via
`request_access` if they grant it) instead. If a future session actually solves this, update that
knowledge-file entry with the fix — don't leave the "unresolved" framing stale once it's fixed.

## Screen access

Family geometry (curved profiles, bevels, fan blades, sweeps) is genuinely hard to verify from numbers
alone. If the task involves shapes beyond simple parametric boxes, consider requesting screen access via
`mcp__computer-use__request_access` (app: "Revit 2020" or whichever version is running) early, before
building complex geometry blind — but the user may decline, and that's fine; fall back to bounding-box/volume
read-backs and ask them to eyeball specific steps instead.

## Reply format

Check [`reply-style.md`](../../knowledge/reply-style.md) — a family build is "substantive work," so close
with the standing 7-point Final Report (understood-as, what was reused, split/update/create, what was
live-tested, what needs the user's decision, what got saved, next step flagged). Report honestly what's
verified vs. not — especially void cuts and anything you couldn't visually confirm.

## After finishing

- New API gotcha (a compile error, a silent behavior, a discovery-by-deliberate-error technique) →
  `live-model/families.md`, same section as the existing entries — don't fork a separate family-creation
  notes file, this is still AJ AI Bridge/live-model technical knowledge.
- A genuinely reusable shape (not a one-off) → a new or updated fragment in
  `scripts/recipes/`, following the INPUTS-block pattern in the existing box-family recipe, and
  add it to `scripts/README.md`'s recipes table.
- New ambiguous term or naming collision (like the Shaft-Height/fin-depth collision) → `glossary.md`.
- Dated line in [`knowledge/brain-log.md`](../../knowledge/brain-log.md) for anything worth a standing
  record (a new family type shipped, a technique that finally solved the void-cut problem, etc.).
