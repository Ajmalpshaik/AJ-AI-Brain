# Which sprinkler, and what changes in the layout when the type changes

> Chunk of [`README.md`](README.md). The spacing numbers live in
> [`../nfpa13-sprinkler-spacing.md`](../nfpa13-sprinkler-spacing.md); the heights live in
> [`deflector-and-ceiling-height.md`](deflector-and-ceiling-height.md).

**The type is an input, never a deduction.** It is decided by the ceiling (is there one?), the room use,
and the project specification — and it changes the spacing table, the height rule and the obstruction
rule all at once. Getting it wrong does not produce a slightly-off layout; it produces a layout checked
against the wrong table.

## The orientation families

| Type | Points | Used when | The layout consequence |
|---|---|---|---|
| **Pendent** | down, hangs below the ceiling | there is a finished ceiling and the pipework is hidden above it | the normal case. Standard-spray tables apply; head sits in the ceiling grid |
| **Upright** | up, sits on top of the branch pipe | **no ceiling** — exposed slab, soffit, car park, plant room, warehouse — or above a ceiling protecting the void | same spacing tables as pendent, **different height rule**: the deck and the beams are now the reference, not a ceiling plane |
| **Sidewall** (horizontal or vertical) | sideways, off a wall | no room for pipe above — corridors, small rooms, under a beam, existing buildings, wherever a ceiling cannot be drilled | **its own, tighter table.** Throws one way, so the room is covered from one side; it is not a pendent turned sideways |
| **Concealed** | down, behind a flat cover plate | architecturally sensitive ceilings | a pendent for spacing purposes, but the **cover plate drops at a rated temperature** and the deflector position is fixed by the listing, not by the 1–12 in rule |
| **Recessed / flush** | down, partly inside the ceiling | a tidier ceiling than a plain pendent | same — deflector position comes from the manufacturer's listing |
| **Dry pendent / dry upright** | either | the pipe is in a heated space and the head is in an unheated one (cold store, canopy, car park in a cold climate) | a long dry barrel; the **barrel length is a family/type property that must match the real distance**, so this is a modelling detail that has to be right, not a graphic |
| **Institutional** | down | detention, mental health | tamper-resistant, breakaway; spacing per its listing |

Upright and pendent share the same standard-spray protection-area and spacing tables. Everything else on
this list has its own numbers or takes them from a listing.

## Standard spray vs extended coverage — do not mix the tables

- **Standard spray** is what the NFPA tables in [`../nfpa13-sprinkler-spacing.md`](../nfpa13-sprinkler-spacing.md)
  describe: light hazard out to 225 ft², 15 ft head to head.
- **Extended coverage (EC)** is a listed head that legitimately covers more — commonly out to
  **400 ft² and 16–20 ft** depending on the specific listing `[UNCONFIRMED]`. Its permitted area and
  spacing come from **its own listing**, not from the standard-spray table.

The error that matters: **applying an EC spacing to a standard head thins the layout below code, and
applying a standard-spray spacing to an EC head over-fits it.** If the head schedule says EC, get the
listing sheet; if there is no listing sheet, it is a standard head.

An EC head also has stricter obstruction treatment than a standard one — its longer throw means more
things sit in the way of it. Do not carry a standard-spray obstruction judgement onto an EC head.

## Response speed — quick vs standard

Quick-response (QR) and standard-response (SR) heads are the same shape and, for pure geometry, use the
same spacing tables. Response speed matters to the *hydraulic* design (design-area reduction) and to
some ceiling-pocket allowances, not to where the head lands in plan. It is out of this Brain's scope,
but it belongs on the head schedule, so record it when the user states it.

## Sidewall — the one that gets carried across wrongly

Sidewall heads have their **own** protection area and spacing table, and it is tighter than pendent:

| Hazard class | Max protection area | Max spacing along the wall | |
|---|---|---|---|
| Light | **196 ft²** (≈ 18.2 m²) | **14 ft = 4,267 mm** | `[UNCONFIRMED — cross-checked in two summaries]` |
| Ordinary | commonly quoted as **100 ft²** with **10 ft = 3,048 mm** | | `[UNCONFIRMED — one source instead gave the pendent figure of 130 ft², so this is exactly the number to confirm]` |

Plus the rules that come with the orientation:

- **Light hazard**: permitted with smooth ceilings that may be flat *or sloped*.
- **Ordinary hazard**: permitted only with **smooth, flat** ceilings, and only where the head is
  **specifically listed** for ordinary-hazard use. This is a real gate, not a footnote — an ordinary
  hazard room is not a free choice of sidewall.
- **Deflector 4 in to 6 in below the ceiling** — a narrower window than a pendent's 1–12 in
  `[UNCONFIRMED]`.
- **Deflector no more than 6 in out from the wall or soffit it is mounted on**, and the 4 in minimum
  off a wall still applies `[UNCONFIRMED]`.
- The 6 ft minimum between heads still applies. Heads on the **same wall** facing each other within that
  distance need a **baffle** — solid, at least 8 in long and 6 in high, its top 2–3 in above the
  deflectors `[UNCONFIRMED]`.
- **Throw is one-directional.** A room wider than the listed throw cannot be covered from one wall no
  matter how many heads go on it. This is the check a spacing-only tool never makes: sidewall spacing
  along the wall says nothing about whether the far side of the room is reached.

## Choosing, in the order the decision actually happens

1. **Is there a finished ceiling in this room?** No → upright (or sidewall). Yes → pendent, concealed or
   recessed depending on how the ceiling is meant to look.
2. **Is there a void above the ceiling that itself needs protection?** Then there are *two* layers —
   pendent below and upright above — each laid out on its own, each with its own construction type.
   **Whether the void needs heads at all is a standards question with two different answers** — NFPA tests
   what the void is made of, BS EN 12845 tests whether it is deeper than 800 mm. Settle that first:
   [`concealed-spaces.md`](concealed-spaces.md).
3. **Can pipe get above this space at all?** No → sidewall, and now check the throw across the room,
   not just the spacing along the wall.
4. **Is the space unheated, or is the pipe in a different thermal zone?** → dry barrel, and the barrel
   length is a real dimension to get right.
5. **Only then** open the spacing table for the type chosen.

## Recorded for the model, every time

The head schedule is what survives the session. Whatever is placed, record: type (pendent/upright/
sidewall/concealed), standard vs extended coverage, response speed, K-factor, temperature rating, finish,
and the mounting height rule that was used. Ajmal's standing rule is that a bare count is meaningless —
so is a bare head position without the type it was computed for.
