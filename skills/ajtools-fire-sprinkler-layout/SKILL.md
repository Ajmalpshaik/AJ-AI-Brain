---
name: ajtools-fire-sprinkler-layout
description: Lay out or code-check fire sprinkler heads in a room against NFPA 13 — head count, spacing, distance to walls, minimum spacing, maximum area per head, hazard class, deflector height, and what a beam or a column does to the layout. Use whenever the request is about fire fighting, fire protection, sprinklers, sprinkler heads, deluge/wet/dry sprinkler layout, "how many sprinklers does this room need", "check my sprinkler spacing", "is this layout NFPA compliant", "fire fighting layout for this room", "how far below the ceiling", "there is no ceiling, upright or pendent", "there is a beam/column in the room" — including broken-English/dictated versions ("fire figting", "sprinkler spacing rools", "make sprinkler in this room", "pendend or upraght", "beem", "colom", "celling"). Fires for CHECKING an existing sprinkler layout as much as creating one. Do NOT use this for HVAC air terminals (that is ajtools-hvac-terminal-layout), for smoke detectors, CCTV, WiFi or lighting coverage (that is the plain coverage recipe — those have no NFPA spacing rules), or for hydraulic calculations, pump selection or density/remote-area design, which this Brain does not do at all. Pipe SIZING by the pipe-schedule method IS covered ("size the sprinkler pipe", "how many heads on this pipe", "is this pipe big enough") — sizing only, never routing, and it checks whether the schedule method is even permitted before it sizes anything.
---

# AJ Tools — Fire Sprinkler Layout (NFPA 13)

Fire fighting follows its own rules. Every other coverage job in this Brain — diffusers, detectors, CCTV,
lighting — is satisfied by "no gaps in the floor". A sprinkler layout is not: it must satisfy **several
code limits at once**, and geometric coverage is not one of them. A layout can cover every square metre of
a room and still be non-compliant by four heads.

The rules live in [`knowledge/fire-sprinkler/README.md`](../../knowledge/fire-sprinkler/README.md) —
**open the one chunk that matches the question, not the whole folder**:

| The question | The chunk |
|---|---|
| **which hazard class, and how to decide it** | [`knowledge/fire-sprinkler/hazard-classification.md`](../../knowledge/fire-sprinkler/hazard-classification.md) |
| how many heads, how far apart, how far off the wall | [`knowledge/nfpa13-sprinkler-spacing.md`](../../knowledge/nfpa13-sprinkler-spacing.md) |
| pendent / upright / sidewall / concealed / extended coverage | [`knowledge/fire-sprinkler/sprinkler-types.md`](../../knowledge/fire-sprinkler/sprinkler-types.md) |
| how far below the ceiling — or below the slab where there is none | [`knowledge/fire-sprinkler/deflector-and-ceiling-height.md`](../../knowledge/fire-sprinkler/deflector-and-ceiling-height.md) |
| a beam, a column, a wide duct in the room | [`knowledge/fire-sprinkler/obstructions.md`](../../knowledge/fire-sprinkler/obstructions.md) |
| **does the ceiling void need its own heads** — upright above, pendent below, and the 800 mm figure that is not NFPA | [`knowledge/fire-sprinkler/concealed-spaces.md`](../../knowledge/fire-sprinkler/concealed-spaces.md) |
| the spec says **BS EN 12845**, not NFPA | [`knowledge/fire-sprinkler/nfpa-vs-en12845.md`](../../knowledge/fire-sprinkler/nfpa-vs-en12845.md) |
| does this space need heads at all, and what temperature rating | [`knowledge/fire-sprinkler/where-sprinklers-are-required.md`](../../knowledge/fire-sprinkler/where-sprinklers-are-required.md) |
| what is still missing for a complete design, and why pipe sizing is gated | [`knowledge/fire-sprinkler/roadmap-zero-to-finish.md`](../../knowledge/fire-sprinkler/roadmap-zero-to-finish.md) |
| **sizing the pipe** — schedule method, and whether it is even permitted | [`knowledge/fire-sprinkler/pipe-sizing.md`](../../knowledge/fire-sprinkler/pipe-sizing.md) |
| the whole method, in order | [`knowledge/fire-sprinkler/layout-method.md`](../../knowledge/fire-sprinkler/layout-method.md) |
| which category, which API call, which fragment | [`knowledge/fire-sprinkler/revit-modelling.md`](../../knowledge/fire-sprinkler/revit-modelling.md) |

**Read the chunk before doing anything here** — do not work from remembered figures, and read the folder
README's "before you use a single number" section before making any statement about compliance. Some
numbers there carry an `[UNCONFIRMED]` tag, which means exactly what it says: widely published, not
verified against the standard. Say so when one of them drives an answer.

## What this skill will not do

- **It does not decide hazard class, density, remote area, or hydraulics.** Those are a licensed fire
  protection engineer's calls. This produces a geometrically correct candidate layout and reports which
  code limits it meets or breaks, with the numbers visible, for a competent person to judge.
- **It never says "compliant".** Say what was checked and what the measured values were. NFPA is not the
  whole picture: for the user's projects the AHJ is **QCDD**, whose own requirements and the project
  specification sit on top and can be stricter.
- **Pipe SIZING by the schedule method is in scope** (added 2026-08-20) —
  [`scripts/recipes/sprinkler-pipe-schedule-size.cs`](../../scripts/recipes/sprinkler-pipe-schedule-size.cs).
  It checks first whether the schedule method is permitted at all, and on most real projects the honest
  answer is that it is not. **Hydraulic calculation, pump/tank selection and water-supply analysis stay
  out entirely**, and so does routing — sizing works on pipe that already exists in the model.

## Step 1 — get the inputs that decide every number (never assume these)

Ask, then restate what was used in the reply. Nothing here is derivable from the model:

1. **Hazard class** — light / ordinary I / ordinary II / extra I / extra II / high-piled storage.
   Every spacing and area limit changes with it. There is no safe default.
2. **Construction type above the ceiling** — unobstructed or obstructed, combustible or noncombustible,
   and whether structural members are under 3 ft (914 mm) apart. This alone can cut the light-hazard area
   limit from 225 ft² to 130 ft², nearly doubling the head count.
3. **Sprinkler type** — standard spray pendent/upright, sidewall, or listed extended coverage. Sidewall
   and EC have their own limits; carrying the 15 ft standard-spray figure across is a real error.
4. **Which standard, and which edition.** NFPA 13 is the default under QCDD, but a specification can
   call up **BS EN 12845** instead and Gulf projects do. The two agree on area per head and differ on the
   deflector window, the minimum spacing, the ceiling-void rule and the hazard classes — so this is not a
   formality: [`knowledge/fire-sprinkler/nfpa-vs-en12845.md`](../../knowledge/fire-sprinkler/nfpa-vs-en12845.md).
5. **The room(s)**, and whether the layout goes in the ceiling plan or the floor plan.

If the user cannot give the hazard class, say plainly that the head count cannot be produced without it,
and offer to compute the options side by side (light / ordinary / extra) so they can see the difference —
that is useful; guessing one and presenting it as the answer is not.

## Step 2 — run the chain, one step at a time, checking each real result

Eight fragments, written for this job, that run in this order. Same discipline as every other skill here:
a short numbered plan, one step, check the real result, then the next. Never one script that does it all.

**Start at the scope he named.** All three routes join the same chain; only the entry point differs:

| He says | Start with |
|---|---|
| *"the whole plan"* / *"this floor"* | [`scripts/recipes/sprinkler-floor-scope.cs`](../../scripts/recipes/sprinkler-floor-scope.cs) — sorts every room into needs-heads / special / ASK, then work the list room by room |
| *"room one"* / one named room | step 1 below |
| *"give me another layout"* / *"I don't like this one"* | [`scripts/recipes/sprinkler-layout-options.cs`](../../scripts/recipes/sprinkler-layout-options.cs) — several compliant layouts, ranked, with a check line each |

| | Step | Fragment |
|---|---|---|
| 1 | **Look inside the room first** — ceiling or no ceiling, the deck, every beam and column, the bay module, wide services | [`scripts/recipes/sprinkler-obstruction-survey.cs`](../../scripts/recipes/sprinkler-obstruction-survey.cs) |
| 2 | Say out loud whether this is **unobstructed or obstructed construction**, and why. Everything downstream hangs on that word | — |
| 3 | State the **head-count floor** from the area rule alone (room area ÷ max area per head, rounded up) before laying anything out | — |
| 4 | **Derive the grid from the limits** — smallest nx × ny satisfying area per head, spacing, wall distances and minimum separation at once | [`scripts/recipes/sprinkler-nfpa-grid.cs`](../../scripts/recipes/sprinkler-nfpa-grid.cs) |
| 4b | **Or show him the field** — every compliant layout, ranked, so he can choose or ask for another. Use this whenever he might want a say, which is most of the time | [`scripts/recipes/sprinkler-layout-options.cs`](../../scripts/recipes/sprinkler-layout-options.cs) |
| 5 | **Test those positions against the beams and columns** — four rules, in code order | [`scripts/recipes/sprinkler-obstruction-check.cs`](../../scripts/recipes/sprinkler-obstruction-check.cs) |
| 6 | Move what fails, by the smallest amount that does not break something else — **then re-check step 4** | [`scripts/recipes/sprinkler-adjust-for-obstructions.cs`](../../scripts/recipes/sprinkler-adjust-for-obstructions.cs) |
| 7 | **Set the height by reading what is really above each head**, not from a remembered ceiling void | [`scripts/recipes/sprinkler-deflector-height.cs`](../../scripts/recipes/sprinkler-deflector-height.cs) |
| 8 | Place the families — after showing Ajmal the count and getting a clear go-ahead | [`scripts/recipes/sprinkler-place-heads.cs`](../../scripts/recipes/sprinkler-place-heads.cs) |
| 9 | **Audit what is actually in the model**, from a separate bridge call | [`scripts/recipes/sprinkler-compliance-audit.cs`](../../scripts/recipes/sprinkler-compliance-audit.cs) |

Two side doors off that chain:

- **Sidewall heads** (corridors, no void above) → [`scripts/recipes/sprinkler-sidewall-layout.cs`](../../scripts/recipes/sprinkler-sidewall-layout.cs).
  Its own table, and the across-room throw check that spacing alone never makes.
- **"Check my sprinkler spacing"** with nothing to design → jump straight to step 9.

**All eight were written 2026-08-20 and none has been run against a real model yet.** That is not a reason
to refuse the job — it is the Brain's standing rule: run one element first, check the real result, then use
it for the batch, and say plainly that is what you are doing.

Two things the chain will not do for you, and both are yours to say out loud:

- **Step 6 changes the answer to step 4.** A head moved to clear a beam has different spacing, a different
  wall distance and a different area. Re-running the check is the step people skip and it is how a
  compliant grid quietly becomes a non-compliant one.
- **An obstructed room does not have a free grid.** Where the beams set the module, one axis of the spacing
  is decided by the bay. Find that out in step 1, not after the grid is drawn.

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

Route what you learned to the **one** chunk it belongs to — see the table at the top of this file. Never
copy a fact into two of them.

Two things are worth more than a normal note:

- **A number confirmed against the real adopted edition.** Several values in the folder carry an
  `[UNCONFIRMED]` tag. Replacing one with a checked figure and dropping the tag is the single most
  valuable edit anyone can make here. The **beam obstruction table** in
  [`scripts/recipes/sprinkler-obstruction-check.cs`](../../scripts/recipes/sprinkler-obstruction-check.cs)
  is top of that list — it is an input, seeded with commonly published values, and it prints a warning on
  every run until `beamTableConfirmed` is set.
- **Ajmal's own words for something.** His rule, 2026-08-10: *"this is my normal work and you have to
  remember the words am using."* A term, a spelling, the way he describes a whole job → a row in
  [`knowledge/site-vocabulary.md`](../../knowledge/site-vocabulary.md) (works immediately, no rebuild) and
  an entry in [`knowledge/glossary.md`](../../knowledge/glossary.md), in the same turn.

If he states a project standard — hazard class, a QCDD requirement, an office default mounting height —
that is a durable project fact. Record it, because it is exactly the kind of number that must not be
re-derived or re-guessed next session.

And when one of these fragments finally runs against a real model: **update its STATUS block with what
actually happened**, including anything it got wrong. All eight say "NOT LIVE-VERIFIED" today, and that
sentence is only useful while it is true.
