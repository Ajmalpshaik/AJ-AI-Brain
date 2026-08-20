# The Z dimension — how high the head sits, with a ceiling and without one

> Chunk of [`README.md`](README.md). Plan spacing is [`../nfpa13-sprinkler-spacing.md`](../nfpa13-sprinkler-spacing.md);
> what a beam does to this number is [`obstructions.md`](obstructions.md).

Plan position is half a sprinkler layout. The other half is **how far the deflector sits below the thing
above it** — and that number is decided by what is above it, which is a different question in a room with
a ceiling and a room without one. Ajmal asked this directly: *"with celling how, without celling how"*.

**The deflector, not the head.** Every dimension on this page is measured to the flat plate the water
hits, not to the pipe, the boss, the escutcheon or the family's insertion point. In a Revit family those
can be several centimetres apart, so the model's Z is only right if you know which of them the family's
origin actually is — see [`revit-modelling.md`](revit-modelling.md).

## Case 1 — there is a ceiling (pendent, concealed, recessed)

The ceiling is the reference plane.

| | Number | |
|---|---|---|
| Deflector below a smooth flat ceiling, **unobstructed construction** | **1 in to 12 in = 25 mm to 305 mm** | corroborated in several summaries |
| Why not less than 1 in | closer than that and the spray has no room to form — it washes along the ceiling instead of throwing out | |
| Why not more than 12 in | too low and the hot gas layer passes over the head; it operates late, and a dead band opens under the ceiling | |
| Concealed, recessed and flush heads | **whatever the listing says**, not the 1–12 in rule | the cover plate and the recess are part of the tested assembly |
| Sidewall | **4 in to 6 in = 102 mm to 152 mm** below the ceiling `[UNCONFIRMED]` | tighter window than a pendent |

In practice on a suspended grid ceiling the number is not chosen freely — the head is set in the tile and
the deflector lands where the family and the ceiling thickness put it. **Check it rather than assume it**;
that is exactly the number the model can be silently wrong about.

## Case 2 — there is NO ceiling (upright under a slab, soffit, deck)

Now the reference is the **underside of the structure**, and the first question is what kind of structure.

### 2a. Unobstructed construction — a flat soffit with nothing hanging below it

Same window as a ceiling: **deflector 1 in to 12 in (25–305 mm) below the underside of the slab.**
A flat concrete soffit with the services run tight to it behaves like a smooth ceiling.

### 2b. Obstructed construction — beams, joists, purlins hanging below the deck

This is the normal case in a car park, plant room, warehouse or any exposed-structure area, and it has
**two** limits at once, not one:

| | Number | |
|---|---|---|
| Deflector below the **bottom of the structural member** | **1 in to 6 in = 25 mm to 152 mm** | corroborated |
| Deflector below the **deck / slab soffit**, total | commonly published as a maximum of **22 in = 559 mm** `[UNCONFIRMED]` | this is the one to confirm against your edition — one source gave 14 in, which is the NFPA 13R balcony rule, not this |

Read them together: the head drops just under the beams so the spray clears them, **but only so far**,
because past that limit it is too far off the deck to see the fire early. A deep beam can make both
impossible to satisfy at once — and when they conflict, the answer is not to split the difference, it is
to **put a head in each bay** (see [`obstructions.md`](obstructions.md)).

### 2c. Which one is it? — the obstructed-construction definition

Not a judgement call; it has a test `[UNCONFIRMED, but consistent across sources]`:

- solid-web structural members — **excluding** fire-resistant bar joists and deep-chord open-web joists —
- spaced **7 ft 6 in (2,286 mm) or closer**,
- **or** spaced wider than that where the pocket the members and their girders form is **no larger than
  300 ft² (27.9 m²)**.

Consequences worth stating plainly, because they are where head counts come from:

- Obstructed construction can **cut the maximum area per head** — light hazard with combustible members
  closer than 3 ft drops from 225 ft² to 130 ft², which is nearly double the heads in the same room.
- Open-web bar joists that let heat and water through are treated as *unobstructed* — so a steel-joist
  roof and a downstand-beam soffit are opposite answers, in rooms that look similar on a plan.

## Case 3 — a sloped ceiling or roof

- Where the pitch is **steeper than 2 in 12**, deflectors are installed **parallel to the slope**, not
  level `[UNCONFIRMED]`. A head hung level under a sloping soffit throws short on the uphill side.
- A head **directly under the peak** is installed **horizontal** (parallel to the floor) even though the
  rest are parallel to the slope.
- Heads near the peak are held **within about 3 ft (914 mm) of it** so the space that collects the hot
  gas is not left unprotected `[UNCONFIRMED — mainly cited for residential heads; confirm for standard
  spray]`.
- Spacing along a slope is measured **along the slope**, not on its plan projection. A plan-only tool
  under-measures every distance on a pitched roof — say so rather than reporting the plan number.

## Case 4 — ceiling pockets, coffers, bulkheads

A small drop or coffer does not automatically need its own head. For standard pendent/upright heads the
commonly published allowance is a pocket **under 1,000 ft³ in volume and less than 36 in deep**, with the
area below already protected, and pockets within 10 ft of each other counted together against that same
1,000 ft³ `[UNCONFIRMED]`. (Residential heads have a much smaller allowance — around 100 ft³ and 12 in.)

Modelling consequence: a ceiling that is not one flat plane is **not** one layout. Split it by level and
check each pocket against the allowance before deciding it can be ignored.

## Case 5 — clearance below the deflector

The spray needs vertical room to open up before it reaches anything:

- **18 in (457 mm) clear below the deflector** to the top of storage, and nothing may be stacked into it.
- The same 18 in is what makes a duct or a light fitting ignorable — see [`obstructions.md`](obstructions.md).
- This is a **3D clash question, not a plan question.** It cannot be answered from a ceiling plan, and it
  is the most common way a layout that looks perfect in plan fails on site.

## Putting a real number on the model

The height a fragment writes is built up, not looked up:

```
head Z  =  reference plane Z  −  deflector offset  ±  family origin correction
```

- **Ceiling room**: reference = the finished ceiling underside. Offset = 25–305 mm (pendent) or 102–152 mm
  (sidewall), unless the head is concealed/recessed, in which case the listing fixes it.
- **Exposed room, flat soffit**: reference = slab underside. Offset = 25–305 mm.
- **Exposed room, beams below**: reference = the **bottom of the beam in that bay**, offset 25–152 mm —
  then check the total below the deck against the second limit and report if it busts.
- **Family origin correction**: whatever the difference is between the family's insertion point and its
  deflector. Measure it once per family; do not assume it is zero.

Every one of those reference planes is a real element in the model, so the honest way to get it is to
**read it** — ray-cast up from the head position and take what is actually hit — never to work from a
level's elevation plus a remembered ceiling void.
[`scripts/recipes/sprinkler-deflector-height.cs`](../../scripts/recipes/sprinkler-deflector-height.cs)
does exactly that, and reports the case it decided it was in.
