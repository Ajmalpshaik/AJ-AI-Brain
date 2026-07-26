---
name: ajtools-fire-sprinkler-layout
description: Lay out or code-check fire sprinkler heads in a room against NFPA 13 spacing rules — head count, head-to-head spacing, distance to walls, minimum spacing, maximum area per head, hazard class. Use whenever the request is about fire fighting, fire protection, sprinklers, sprinkler heads, deluge/wet/dry sprinkler layout, "how many sprinklers does this room need", "check my sprinkler spacing", "is this layout NFPA compliant", "fire fighting layout for this room" — including broken-English/dictated versions ("fire figting", "sprinkler spacing rools", "make sprinkler in this room"). Fires for CHECKING an existing sprinkler layout as much as creating one. Do NOT use this for HVAC air terminals (that's ajtools-hvac-terminal-layout), for smoke detectors, CCTV, WiFi or lighting coverage (that's the plain coverage recipe — those have no NFPA spacing rules), or for hydraulic calculations, pipe sizing, pump selection or density/remote-area design, which this Brain does not do at all.
---

# AJ Tools — Fire Sprinkler Layout (NFPA 13)

Fire fighting follows its own rules. Every other coverage job in this Brain — diffusers, detectors, CCTV,
lighting — is satisfied by "no gaps in the floor". A sprinkler layout is not: it must satisfy **several
code limits at once**, and geometric coverage is not one of them. A layout can cover every square metre of
a room and still be non-compliant by four heads.

All the numbers live in [`knowledge/nfpa13-sprinkler-spacing.md`](../../knowledge/nfpa13-sprinkler-spacing.md).
**Read that file before doing anything here** — do not work from remembered figures, and read its "hard
boundary" section before making any statement about compliance.

## What this skill will not do

- **It does not decide hazard class, density, remote area, or hydraulics.** Those are a licensed fire
  protection engineer's calls. This produces a geometrically correct candidate layout and reports which
  code limits it meets or breaks, with the numbers visible, for a competent person to judge.
- **It never says "compliant".** Say what was checked and what the measured values were. NFPA is not the
  whole picture: for the user's projects the AHJ is **QCDD**, whose own requirements and the project
  specification sit on top and can be stricter.
- Pipe sizing, pump/tank selection, and hydraulic calculation are out of scope for this Brain entirely.

## Step 1 — get the inputs that decide every number (never assume these)

Ask, then restate what was used in the reply. Nothing here is derivable from the model:

1. **Hazard class** — light / ordinary I / ordinary II / extra I / extra II / high-piled storage.
   Every spacing and area limit changes with it. There is no safe default.
2. **Construction type above the ceiling** — unobstructed or obstructed, combustible or noncombustible,
   and whether structural members are under 3 ft (914 mm) apart. This alone can cut the light-hazard area
   limit from 225 ft² to 130 ft², nearly doubling the head count.
3. **Sprinkler type** — standard spray pendent/upright, sidewall, or listed extended coverage. Sidewall
   and EC have their own limits; carrying the 15 ft standard-spray figure across is a real error.
4. **NFPA edition adopted** and any QCDD or project-specification requirement that overrides it.
5. **The room(s)**, and whether the layout goes in the ceiling plan or the floor plan.

If the user cannot give the hazard class, say plainly that the head count cannot be produced without it,
and offer to compute the options side by side (light / ordinary / extra) so they can see the difference —
that is useful; guessing one and presenting it as the answer is not.

## Step 2 — plan, then run one step at a time

Same discipline as every other skill here: a short numbered plan, one step, check the real result, next.

1. Resolve the room fresh — Id, real area, bounding box, whether it is actually placed (Area > 0).
   An unplaced room silently covers nothing.
2. Compute the **minimum head count from the area rule alone** (room area ÷ max area per head for that
   hazard class, rounded up). State it before laying anything out — it is the floor no layout may go under.
3. Generate the candidate layout with
   [`scripts/recipes/generate-room-coverage-layout.cs`](../../scripts/recipes/generate-room-coverage-layout.cs)
   in **`layoutMode = "inset"`** — centres inside the room, off the walls. Feed it the real code limits:
   `maxAllowedSpacingMm`, `maxWallDistanceMm`, `minSpacingMm` and `maxAreaPerDeviceM2` from the knowledge
   file, converted from the foot values.
   **Set the radius from the spacing rule, not from a "coverage radius"** — see the trap below.
4. Read the recipe's report and relay every check with its measured number: head count, area per head,
   head-to-head spacing, distance to the worst wall, minimum spacing, and how many centres are inside the
   room. A FAIL is reported, never quietly rounded away.
5. Only then place real families, if asked — `creators/create-point-based-element.cs` at the reported
   centres, with the mounting height the user gives.

## The trap that produces a plausible, wrong layout

**"Coverage radius" is not an NFPA concept.** Nothing in NFPA 13 says a sprinkler covers a circle of
radius r. The code limits *area per head*, *spacing*, and *distance to wall* — all rectangular-grid
quantities. If a radius is used to drive the drawing (because the coverage recipe works that way), it is
only a drafting device, and the radius must be derived so the resulting grid satisfies the real limits —
not chosen because someone said "3 metres". A 3 m radius on a 288 m² room produced a 20-head layout whose
155 ft² per head is legal for light hazard and illegal for ordinary hazard, and whose circles said nothing
about either. Report the grid numbers as the answer; the circles are just how it is shown.

Two more traps, both proven on real runs (details in the knowledge file):

- **Convert the foot value every time.** 15 ft is **4,572 mm**, not 4,600 — a rounded metric cap is
  lenient and will pass layouts the code fails.
- **Check the minimum spacing too.** 6 ft (1,829 mm) between heads is a code limit in the other direction;
  a tight layout can break it while covering perfectly.

## Reply format

Follow [`knowledge/reply-style.md`](../../knowledge/reply-style.md). For this skill specifically, the
substantive reply is a **check table** — one row per code rule, with the limit, the measured value, and
PASS/FAIL — plus the head centres, plus an explicit line naming the hazard class and construction type the
numbers were based on. Never present a bare head count without the class it depends on; it is meaningless
and it is the number that ends up on a drawing.

## After finishing

New gotcha, or a code figure confirmed against the real adopted edition →
[`knowledge/nfpa13-sprinkler-spacing.md`](../../knowledge/nfpa13-sprinkler-spacing.md), in the one place it
belongs. If the user states their project's own standard (hazard class, QCDD requirement, an office default),
that is a durable project fact — record it, since it is exactly the kind of number that must not be
re-derived or re-guessed next session.
