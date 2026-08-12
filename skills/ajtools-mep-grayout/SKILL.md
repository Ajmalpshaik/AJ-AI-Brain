---
name: ajtools-mep-grayout
description: Apply Ajmal's "grayout for MEP" drawing standard to a Revit view — the architectural and structural background flattened to grey, services brought forward in black at weight 3, insulation as a quiet dashed wrapper, rebar switched off. Use whenever he says "do the grayout", "do the grayout for MEP", "grayout this view", or asks for the same treatment on another model or another view — both phrasings mean the identical job and he should never have to re-dictate the values. Also use when he asks to change part of it ("make the floor lighter", "duct black", "insulation 100") — the settled values live here, so edit against them rather than starting from the current view. Do NOT use for a one-off colour change to a handful of elements (that is action-set-color-uniform.cs), for a rule-based colour by parameter (that is a View Filter), or for the project-wide Line Weights table, which is not reachable from the API at all.
---

# AJ Tools — Grayout for MEP

The single command he wants on any new model: **"do the grayout for MEP"** and the whole view comes back
set up for coordination. He taught this step by step on 2026-08-10 and asked that it never be re-derived —
*"if i need to do in anothor model i will tell you only that grayout for mep so the all same work need
todo."* The values below are the standard, not suggestions.

Everything runs through one fragment: [`../../scripts/recipes/mep-grayout.cs`](../../scripts/recipes/mep-grayout.cs).

## The scheme

| Layer | Line | Fill | Weight | Transparency |
|---|---|---|---|---|
| Background (everything not named below) | `150,150,150` | `200,200,200` solid | 1 | 0% |
| **Walls** | `150,150,150` | `200,200,200` solid | **2** | 0% |
| **Floors** | `240,240,240` | `240,240,240` solid | 1 | 0% |
| **Doors** | `200,200,200` | `200,200,200` | 1 | **100%** |
| **Windows** | `150,150,150` | `200,200,200` | 1 | **100%** |
| **Services** — ducts, pipes, cable tray + their fittings and accessories, flex | `0,0,0` | *(discarded)* | **3** | **80%** |
| **Insulation** — duct + pipe | `80,80,80` | *(discarded)* | 1 | **100%** |
| **Mechanical Equipment** | `0,0,0` | `128,128,128` solid | **3** | 0% |
| **OFF** | Structural Rebar · Structural Rebar Couplers | | | |

Insulation also takes the line pattern **`MEP_Hidden_Short_Dash`** — the pattern behind his office line
*style* `MEP_Hidden_Short_Black`. Services and background take a solid line pattern.

## The three values that look wrong and are not

1. **Windows are 150 while doors are 200.** A window sits *inside* a wall whose fill is already 200, so a
   200 line disappears into it; a door breaks the wall with its opening and swing, so a light line still
   reads. He caught this himself after the first pass. The general rule worth carrying: **anything sitting
   inside a greyed host must not take the host's fill colour as its line colour.**
2. **Floors are lighter than walls** (240 against 200). Floors are the biggest surface in a plan; dropping
   them to near-white puts them behind the walls instead of competing. A side effect is that a slab edge
   drawn on its own floor stops reading — that is wanted, the aim is a flat field, not outlined slabs.
3. **Everything drops to weight 1 before the services come back up to 3.** Flatten first, then rebuild the
   hierarchy. Do not "helpfully" leave the services heavy during the flatten.

## How to work

1. **Confirm the view.** This is per-view, not project-wide. State which view is being changed before
   changing it, and check for a **View Template** — if one controls V/G model categories, these overrides
   are locked out or reverted, and the job silently does nothing.
2. **Run the recipe once**, then **report what actually stuck**, not what was written. It reads every
   category back and lists the losses.
3. **Never report "all grey with solid fill".** It is not true and the reasons are structural — see below.
4. **To reuse across views**, apply it to one view and save that view as a **View Template** by hand.
   There is no supported API for creating a template from a view's current graphics.

## What Revit throws away, every time

Not bugs — measured and documented in
[`../../knowledge/live-model/graphic-override-precedence.md`](../../knowledge/live-model/graphic-override-precedence.md).

- **Non-cuttable categories discard the cut line, cut weight and cut fill.** That is all of Ducts, Pipes,
  Air Terminals, Sprinklers, Mechanical Equipment and the electrical families. Ducts and pipes discard the
  **surface fill** too, so the services end up as coloured lines with no fill however solid the script looks.
- **Rooms, Areas, Spaces, Raster Images and Point Clouds take no category override at all.** Rooms and
  Areas colour from a Colour Scheme instead.
- **Sub-categories hold line colour and line weight only** — never fill, never transparency. The ones named
  for a graphic layer rather than geometry (`Surface Pattern`, `Cut Pattern`, `Common Edges`) refuse even
  those.
- **Transparency shows only in Shaded / Consistent Colours / Realistic.** In a Hidden Line plan every
  transparency value above is stored correctly and displays nothing. Say so rather than reporting success
  on something invisible where he is looking.

## Known inconsistency — decide, don't silently fix

Because the services went black *after* the sub-categories were matched to their parents, **service
sub-categories (Ducts > Rise / Drop / Center line, and the equivalents) are still background grey while
their parent outline is black.** The recipe reproduces that faithfully by default, since he said to keep
what was built. Flipping `subCategoriesFollowFinalParent = true` makes them follow their parent to black.
Ask which he wants the first time it comes up on a real model; do not change it unprompted.

## Still undecided, carried forward

- **Duct Linings** — grey `150,150,150` at weight 1 with 80% transparency. It is a wrapper like insulation,
  not a carrier, so it plausibly belongs on the insulation spec. He has been asked twice and not answered.
- **Conduits, Conduit Fittings, Wires** — still background grey. Same carrier-plus-fittings shape as cable
  tray, so they are the obvious next extension if electrical should read at mechanical's level.
- **Air Terminals and Sprinklers** — background grey, and note they **cannot hold a fill** by category
  override, so making diffusers read solid needs a View Filter or an element-level override.

## After finishing

If he changes a value, update the table above **and** the INPUTS block in the recipe in the same turn —
they must not drift apart. Log anything structural in
[`../../knowledge/brain-log.md`](../../knowledge/brain-log.md). His own wording for anything new goes in
[`../../knowledge/glossary.md`](../../knowledge/glossary.md), which holds the short pointer to this skill.
