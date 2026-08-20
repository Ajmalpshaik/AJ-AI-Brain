# The method — room boundary in, head positions out

> Chunk of [`README.md`](README.md). This is the order the decisions have to be made in. Skip a step and
> the layout is not wrong-looking, it is wrong.

## Step 0 — the inputs nothing in the model can give you

Ask, and restate them in the reply. There is no safe default for any of these.

| Input | Why it decides the answer |
|---|---|
| **Hazard class** — light / ordinary I / ordinary II / extra I / extra II / storage | sets max area per head and max spacing; changes the head count by 50–100% |
| **Construction above** — unobstructed or obstructed, combustible or not, member spacing | can halve the max area per head on its own |
| **Ceiling or no ceiling** | decides pendent vs upright, and which height rule applies |
| **Head type** — standard spray / extended coverage / sidewall | different tables entirely |
| **NFPA edition adopted**, plus QCDD and the project spec | the numbers and the section pointers both move |
| **Which room(s)**, and whether the layout is drawn on the ceiling plan or the floor plan | |

If the hazard class is not available, do **not** guess one. Compute light / ordinary / extra side by side
and show the three head counts — that is genuinely useful, and it makes the dependency visible instead of
hiding it inside a single number.

## Step 1 — get the real room, not the drawn rectangle

- Resolve the Room by Id. Check `Area > 0` — an **unplaced room encloses nothing** and every downstream
  number will be silently zero.
- Take the **boundary segments**, not just the bounding box. An L-shaped or notched room laid out from
  its bounding box puts heads in the notch, outside the room, where nothing can be mounted.
- Note the level and the room height.

## Step 2 — survey what is inside it, before choosing any grid

This is the step that separates a sprinkler layout from a coverage exercise. Run
[`scripts/recipes/sprinkler-obstruction-survey.cs`](../../scripts/recipes/sprinkler-obstruction-survey.cs)
and get, for the room:

- **Ceiling** — is there one, at what level, and is it flat?
- **Beams** — plan lines, widths, and soffit levels. Is there a repeating bay module?
- **Columns** — plan footprints and sizes.
- **Services** — ducts, trays, wide pipes, lighting; width and soffit level.

Then decide, out loud, in one sentence: **is this room unobstructed or obstructed construction, and is the
grid free or is it set by bays?** Everything after this depends on that sentence.

## Step 3 — the head-count floor, from the area rule alone

```
minimum heads  =  ceil( room area  /  max area per head for this hazard + construction )
```

State it before laying anything out. **No layout may go under it**, however good the coverage looks. This
is the check a covering algorithm never makes, and the one that has already caught a real layout in this
Brain: a 20-head, zero-gap layout on a 288 m² room was legal for light hazard and four heads short for
ordinary hazard, with identical geometry.

## Step 4 — derive the grid FROM the limits (not from a radius)

**"Coverage radius" is not an NFPA concept.** Nothing in the standard says a head covers a circle. The
code limits *area per head*, *spacing* and *distance to wall* — all rectangular-grid quantities. So the
grid is found, not chosen:

> Search `nx × ny` from small upward. Take the **smallest head count** where all of these hold at once:
>
> - `A_s = S × L` ≤ max area per head — **S × L, not room area ÷ head count**. S is the spacing along
>   the branch line, L the spacing between branch lines. The two methods agree only when the grid tiles
>   the room exactly; on an irregular room `S × L` is the one the code means, and it is the larger one.
> - `S` and `L` ≤ max head-to-head spacing (light/ordinary **15 ft = 4,572 mm**, not 4,600 — convert the
>   foot value every time; a rounded metric cap is lenient and passes layouts the code fails).
> - distance to each wall ≤ half the allowable spacing (**2,286 mm** at 15 ft), and ≥ **102 mm** (4 in).
> - closest pair ≥ **1,829 mm** (6 ft) — the minimum, which a dense layout breaks while covering
>   perfectly.
> - every centre inside the room boundary.

[`scripts/recipes/sprinkler-nfpa-grid.cs`](../../scripts/recipes/sprinkler-nfpa-grid.cs) does this search
and prints the check table. If a radius has to be drawn for the drawing, it is **half the cell diagonal**
— derived from the grid, not an input to it.

**In an obstructed room, one direction of the grid is not free**: it is the bay module. Fix that spacing
first, then search only the other direction.

## Step 5 — resolve the obstructions, head by head

Now apply [`obstructions.md`](obstructions.md) to the grid from step 4, via
[`scripts/recipes/sprinkler-obstruction-check.cs`](../../scripts/recipes/sprinkler-obstruction-check.cs):

1. Ignore what may be ignored (≤ 4 ft wide *and* ≥ 18 in below the deflector).
2. For each remaining pair, try the three-times rule (columns and isolated items) or the beam table
   (continuous items).
3. Move the failing heads — smallest nudge that clears it — and **re-run step 4's checks on the moved
   layout**, because a nudge can break spacing, the wall distance or the minimum.
4. What still fails gets a head of its own underneath, or the bay layout from Rule C.

**The re-check in point 3 is not optional and it is the step most often skipped.** Moving a head to solve
an obstruction is how a compliant grid becomes a non-compliant one.

## Step 6 — set the Z

Per [`deflector-and-ceiling-height.md`](deflector-and-ceiling-height.md), and by **reading** the ceiling
or soffit above each head rather than assuming a void depth.
[`scripts/recipes/sprinkler-deflector-height.cs`](../../scripts/recipes/sprinkler-deflector-height.cs)
ray-casts up, decides which case each head is in, and reports it.

## Step 7 — place, then read back from a separate call

Place with [`scripts/recipes/sprinkler-place-heads.cs`](../../scripts/recipes/sprinkler-place-heads.cs).
Then **read the placed heads back out of the model in a different call** and re-run the audit
([`scripts/recipes/sprinkler-compliance-audit.cs`](../../scripts/recipes/sprinkler-compliance-audit.cs))
against what is really there. The Brain has caught four separate "silent success" bugs this way — a
script's own report of what it did is not evidence that it did it.

## Step 8 — report as a check table, never as a count

One row per rule: the limit, the measured value, PASS or FAIL. Plus the hazard class and construction
type the numbers came from, named explicitly. A head count without the class it depends on is the number
that ends up on a drawing, and it is meaningless on its own.

Two or more numbers that invite comparison get **drawn as a chart in the reply**, per Ajmal's standing
rule — see [`skills/ajtools-visual-report/SKILL.md`](../../skills/ajtools-visual-report/SKILL.md).

## The five ways this goes wrong

Every one of these has happened, here or in the sources:

1. **Covering the room and calling it compliant.** Zero gaps says nothing about area per head.
2. **Laying out from the bounding box** — heads land outside an L-shaped room and nothing can be mounted.
3. **Using a rounded metric cap.** 4,600 mm passes what 4,572 mm fails.
4. **Ignoring the beams until after the grid is set**, then nudging heads without re-checking spacing.
5. **Reporting a head count without the hazard class.** It is not an answer, it is a number.
