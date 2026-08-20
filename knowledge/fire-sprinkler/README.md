# Fire sprinklers — knowledge index (read this, then open ONE file)

Everything this Brain knows about **where a sprinkler head goes and why**: spacing, room coverage,
ceiling and no-ceiling cases, pendent / upright / sidewall, and what a beam or a column does to the
layout. Deliberately split into small chunks so a search returns the one that answers the question
instead of a 700-line wall.

The workflow that uses all of this is [`skills/ajtools-fire-sprinkler-layout/SKILL.md`](../../skills/ajtools-fire-sprinkler-layout/SKILL.md).
**Pipe sizing, hydraulics, pump and tank selection are not here and not in this Brain** — this folder is
about head placement only.

| The question is about… | Open |
|---|---|
| How many heads, how far apart, how far off the wall — hazard class, max area per head, max/min spacing, wall distances, the small-room rule, the worked jobs already done | [`../nfpa13-sprinkler-spacing.md`](../nfpa13-sprinkler-spacing.md) — the numbers chunk, stayed where it was so old links still work |
| Which head — pendent, upright, sidewall, concealed, recessed, flush, dry, extended coverage — and what changes in the layout when the type changes | [`sprinkler-types.md`](sprinkler-types.md) |
| **The Z dimension**: how far below the ceiling, how far below the slab when there is no ceiling, sloped ceilings, ceiling pockets, clearance above stored goods | [`deflector-and-ceiling-height.md`](deflector-and-ceiling-height.md) |
| **Beams, columns, ducts, light fittings** — the three-times rule, the beam table, wide obstructions, obstructions against a wall, what may be ignored | [`obstructions.md`](obstructions.md) |
| **The actual method** — room boundary in, head positions out, in the order the decisions have to be made | [`layout-method.md`](layout-method.md) |
| Doing it in Revit — which categories hold the beams and columns, how to read the ceiling, how head height is really controlled, which fragment to run | [`revit-modelling.md`](revit-modelling.md) |

## Before you use a single number out of this folder

Three limits apply to every file here, and they are not boilerplate — they are the reason this folder
is safe to keep.

1. **These are paraphrases with section pointers, not the standard.** NFPA 13 is copyrighted and
   edition-specific. Section numbers move between editions (obstructions were §8.6.5 in the 2016
   edition and are §10.2.7 in 2019/2022), and freely available summaries paraphrase the same table
   inconsistently — a single afternoon of searching returned light-hazard coverage as 225, 200 and
   "130–200" ft². Confirm against **the edition your project has actually adopted**.
2. **Some numbers here are marked `[UNCONFIRMED]`.** That tag means it is a value that is widely
   published in secondary sources but was **not** verified against the standard itself in the session
   that wrote it. It is a starting point to type over, not an answer to issue. The C# fragments print
   the same warning in their own reports, so an unconfirmed number can never quietly turn into a
   compliance claim on a drawing.
3. **The AHJ overrules all of it.** On Ajmal's projects that is the **Qatar Civil Defence Department
   (QCDD)**, which enforces the NFPA suite plus its own General Fire Safety Requirements, and the
   project specification sits on top of both. Never write "compliant" — write what was checked, what
   the measured value was, and against which limit.

And the one that decides whether any of this is useful at all: **a sprinkler layout is a licensed fire
protection engineer's design.** What this Brain produces is a *geometrically correct candidate* with
every limit measured and shown, so a competent person can judge it in a minute instead of an hour.

## What was actually researched, and what could not be

Written 2026-08-20, on Ajmal's ask to study sprinkler spacing and placement properly and turn it into
tools. The research channel that session was **web search snippets only** — the environment blocked
direct page fetches, so no source document could be read end to end.

- **Corroborated by two or more independent summaries**: the coverage-area and spacing families, the
  4 in minimum / half-spacing maximum wall rules, the 6 ft minimum between heads, the small-room rule
  (800 ft², light hazard, unobstructed, 9 ft), 1–12 in below ceiling for unobstructed construction,
  1–6 in below a structural member for obstructed construction, the three-times rule with its 24 in cap
  and the column exception, the "≤ 4 ft wide and ≥ 18 in below the deflector may be ignored" rule, the
  "> 4 ft wide needs heads underneath" rule, sidewall at 196 ft² / 14 ft for light hazard, extended
  coverage running out to 400 ft², and the obstructed-construction definition (solid web members at
  7 ft 6 in or closer, or wider with pockets no larger than 300 ft²).
- **Could NOT be retrieved**: the row-by-row values of the beam obstruction table (distance to the side
  of the obstruction vs how far the deflector may sit above its bottom). Every route to it was paywalled
  or blocked. It is carried in [`obstructions.md`](obstructions.md) as an editable table seeded with the
  commonly published values and tagged `[UNCONFIRMED]` throughout, and
  [`scripts/recipes/sprinkler-obstruction-check.cs`](../../scripts/recipes/sprinkler-obstruction-check.cs)
  takes it as an input rather than hardcoding it. **Typing your edition's real table into that one input
  block is the single highest-value ten minutes anyone can spend on this folder.**
