# Beams, columns and everything else in the way

> Chunk of [`README.md`](README.md). Heights are [`deflector-and-ceiling-height.md`](deflector-and-ceiling-height.md);
> the order to work in is [`layout-method.md`](layout-method.md).

Ajmal's actual ask: *"if any beem or colom is there in the room as per that need to place"*. This is the
file for that, and it is the part of a sprinkler layout that a grid generator cannot do on its own — a
grid knows the room outline; only the model knows what hangs inside it.

**An obstruction is anything at or below the level of the deflector that gets in the way of the spray.**
Structure (beams, joists, columns), services (ducts, trays, pipes, lights), and fittings (bulkheads,
curtains, shelving). Whether it matters depends on **how wide it is, how far it is from the head, and how
far below the deflector it sits** — three numbers, always.

## The three questions, in order

For every obstruction near a head:

1. **Can it be ignored?** — it is narrow enough *and* far enough below the deflector.
2. **If not, can the head move?** — sideways far enough that the spray clears it (three-times rule), or
   down far enough that it starts below it (the beam table).
3. **If not, does it need its own head underneath?** — wide obstructions do.

Nothing else is available. "It looks fine" is not one of the three.

## Rule A — the three-times rule (isolated obstructions)

For an obstruction **under 24 in (610 mm) wide** that is not continuous — a column, a hanging light, a
single pipe, a beam seen end-on:

> Keep the head **at least three times the obstruction's maximum dimension** away from it, horizontally.

- A 100 mm pipe → at least 300 mm clear.
- A 200 mm downstand → at least 600 mm clear.
- The requirement is **capped at 24 in (610 mm)** — beyond that you never have to go further
  `[UNCONFIRMED cap value, corroborated in two summaries]`.
- **The cap does NOT apply to vertical obstructions such as columns.** A 600 mm square column needs the
  full 3 × 600 = 1,800 mm, not 610 mm. This is the trap: a column is the most common obstruction in a
  car park and it is the one exempt from the relief.
- Some sources describe a **four-times** variant for larger vertical obstructions. If your edition has
  it, it is the one that governs a big column — check before applying 3× to anything substantial
  `[UNCONFIRMED]`.

Working the other way: if the head cannot move, the obstruction defines a **keep-out circle** around it,
and the grid has to be nudged out of that circle — which is what
[`scripts/recipes/sprinkler-adjust-for-obstructions.cs`](../../scripts/recipes/sprinkler-adjust-for-obstructions.cs) does.

## Rule B — the beam table (continuous obstructions)

For a **continuous** obstruction — a downstand beam, a girder, a continuous duct run — the rule is not a
single distance. It is a trade: **the further the head is from the side of the beam, the further its
deflector may sit above the beam's bottom.** Close to the beam, the deflector must be level with the
bottom of it; far away, it can be well above.

`[UNCONFIRMED — the whole table below.]` The values are the ones commonly published for standard spray
pendent/upright. Every route to the standard's own table was blocked in the session that wrote this file.
**Type your adopted edition's values over these before issuing anything**, and note that editions
renumber it (§8.6.5.1.2 in older editions, §10.2.7.1.2 / Table 10.2.7.2(a) in 2019/2022).

| Horizontal distance, head to the SIDE of the beam | Max the deflector may sit ABOVE the beam's bottom |
|---|---|
| less than 1 ft (305 mm) | **0 in** — level with the bottom |
| 1 ft to < 1 ft 6 in (305–457 mm) | 2½ in (64 mm) |
| 1 ft 6 in to < 2 ft (457–610 mm) | 3½ in (89 mm) |
| 2 ft to < 2 ft 6 in (610–762 mm) | 5½ in (140 mm) |
| 2 ft 6 in to < 3 ft (762–914 mm) | 7½ in (191 mm) |
| 3 ft to < 3 ft 6 in (914–1,067 mm) | 9½ in (241 mm) |
| 3 ft 6 in to < 4 ft (1,067–1,219 mm) | 12 in (305 mm) |
| 4 ft to < 4 ft 6 in (1,219–1,372 mm) | 14 in (356 mm) |
| 4 ft 6 in to < 5 ft (1,372–1,524 mm) | 16½ in (419 mm) |
| 5 ft to < 5 ft 6 in (1,524–1,676 mm) | 18 in (457 mm) |
| 5 ft 6 in to < 6 ft (1,676–1,829 mm) | 20 in (508 mm) |
| 6 ft (1,829 mm) and beyond | 24 in (610 mm) |

How to read it as a modeller: measure the two dimensions off the model — plan distance from head to the
face of the beam, and the vertical gap between the deflector and the beam soffit — then look up whether
that pair is allowed. Both numbers come out of the model; neither is a choice.

**And the rule that overrides the table**: the head must be **at least 1 ft from the beam** if the
deflector is above the beam's bottom at all. Inside 1 ft there is no allowance to trade.

### There is more than one of these tables, and they get conflated

Worth knowing before someone "corrects" the table above. NFPA carries **separate** obstruction tables for
different geometries — the one above (a beam or similar in the open, sprinklers on both sides of it) and a
different, more forgiving one for an **obstruction against a wall**, where only one side matters. They are
easy to mix up because both are laid out as "distance across" against "distance up".

The tell is how fast they climb: the beam table reaches its 24 in maximum at about **6 ft** away, while the
against-a-wall table is still around 22 in at **10 ft**. This Brain's own earlier note quoted the second
one's numbers under the heading of the first, for weeks. Conflating them is lenient in one direction and
strict in the other, so it is not a harmless slip.

Standard spray, extended coverage, sidewall, CMSA and ESFR each also have their own obstruction rules.
**Match the table to the head type as well as to the geometry.**

## Rule C — a head in every bay

When the beams are deep enough that no position satisfies Rule B, the layout changes shape: **put a head
in each bay** formed by the beams, rather than trying to throw across them.

- Commonly cited trigger: beams deeper than about **18 in (457 mm)** `[UNCONFIRMED]`.
- Members shallower than about **4 in (102 mm)** are generally ignorable unless the head is within about
  1 ft of them `[UNCONFIRMED]`.
- With heads in bays, the deflector rule becomes the obstructed-construction one — **1–6 in below the
  beam soffit, and no more than the deck limit below the slab** (see
  [`deflector-and-ceiling-height.md`](deflector-and-ceiling-height.md)).

**This is the single biggest thing a beam does to a head count.** A room whose plan grid says 12 heads
can need 20 once the bays decide the layout, because the bay module — not the code's maximum spacing —
now sets the spacing in one direction. Say this before generating a grid in an exposed-structure room,
not after.

## Rule D — wide obstructions need heads underneath

| Situation | What happens |
|---|---|
| Obstruction **up to 4 ft (1,219 mm) wide** *and* **18 in (457 mm) or more below the deflector** | may be **ignored** — the spray has room to develop and close over behind it |
| Obstruction **wider than 4 ft (1,219 mm)** | needs **its own sprinklers underneath**, deflector within about **12 in (305 mm)** of its underside `[UNCONFIRMED]` |
| Continuous obstruction **wider than 30 in (762 mm) against a wall** | needs a head beneath it `[UNCONFIRMED]` |

This is the rule that catches wide duct runs, cable-tray banks and bulkheads. In a coordinated MEP model
it is checkable directly: any horizontal element whose plan width exceeds the threshold and whose soffit
sits within 18 in of the deflector plane is a candidate.

## Rule E — the shadow nobody models

Two more that the model will not tell you and the drawing will not show:

- **The head must be able to see the floor it protects.** A layout that satisfies every table above and
  still leaves a whole area shadowed behind a bulkhead is a failed layout.
- **The obstruction has to still be there when it is built.** Beams and columns are; ducts, trays and
  lights move constantly. Re-run the check after every coordination round, not once at the start — which
  is why the check exists as a script rather than a one-off manual exercise.

## What each of these is, in the Revit model

| Rule | Reads | Category |
|---|---|---|
| A — three times | columns | `OST_StructuralColumns`, `OST_Columns` |
| B/C — beam table, bays | beams, joists, trusses | `OST_StructuralFraming` |
| D — wide obstructions | ducts, trays, conduit, pipes, lighting | `OST_DuctCurves`, `OST_CableTray`, `OST_Conduit`, `OST_PipeCurves`, `OST_LightingFixtures` |
| all | the deflector plane | the head's own Z, from the family |

[`scripts/recipes/sprinkler-obstruction-survey.cs`](../../scripts/recipes/sprinkler-obstruction-survey.cs)
collects them with their real plan footprints and soffit levels;
[`scripts/recipes/sprinkler-obstruction-check.cs`](../../scripts/recipes/sprinkler-obstruction-check.cs)
applies Rules A–D head by head and prints a pass/fail line per pair. Neither decides anything — they
measure, and name the rule that each measurement was tested against.
