# The ceiling void — when it needs its own sprinklers

> Chunk of [`README.md`](README.md). Heights are [`deflector-and-ceiling-height.md`](deflector-and-ceiling-height.md);
> which head goes where is [`sprinkler-types.md`](sprinkler-types.md).

Ajmal's question, 2026-08-20, and he was right to flag that he was unsure of the source:
*"if the sealing and slab distance... this ceiling void is more than eight hundred or something, we need
an upright and pendent also."*

**The rule is real. The 800 mm is real. But it is NOT an NFPA rule** — and that matters, because his AHJ
enforces NFPA. Two standards, two completely different tests, and they disagree on the same void.

## The two tests, side by side

| | **NFPA 13** | **BS EN 12845 / BS 5306-2** |
|---|---|---|
| What triggers sprinklers in the void | **what the void is made of and what is in it** | **how deep the void is** |
| The test | is the concealed space of **combustible** construction? | is the ceiling-to-slab distance **≥ 800 mm**? |
| Depth threshold in mm | **none for a ceiling void** | **800 mm** `[UNCONFIRMED — corroborated in two secondary summaries, not read in the standard]` |
| Materials matter | yes — decisive | reported as **regardless of the materials** in the void |
| Deflector below a smooth ceiling | 25–305 mm (1–12 in) | **75–150 mm** — a much tighter window `[UNCONFIRMED]` |

Read the consequence carefully, because it goes **both ways**:

- A **900 mm** void of bare concrete slab, metal ducts and steel framing: **BS/EN says protect it**
  (over 800). **NFPA may not require it at all** — noncombustible construction, minimal combustible
  loading.
- A **600 mm** void with combustible construction: **NFPA says protect it**. **BS/EN would not**, on depth
  alone.

So "is it more than 800?" is the right question on a BS/EN job and the **wrong question** on an NFPA job.
Asking it on the wrong project gives a confidently wrong answer in either direction.

## Which one applies on Ajmal's projects

**Ask, per project, and write the answer down.** The AHJ is QCDD, which enforces the NFPA suite — so the
NFPA test is the default. But **the project specification can call up BS EN 12845 instead**, and plenty of
Gulf projects do, particularly where a European consultant or an insurer is involved. The specification
wins over the default.

This is exactly the kind of project fact the Brain is supposed to hold rather than re-derive: when it is
settled for a job, record it.

## The NFPA test, in the order you actually apply it

1. **Is the concealed space of exposed combustible construction?** If yes → sprinklers are required in it,
   subject to the exception list below.
2. **If it is noncombustible or limited-combustible** — the floor, ceiling, walls and structural elements
   of the space — **and has minimal combustible loading**, sprinklers may be omitted.
3. **Then check the specific exceptions.** These are the numeric ones that do exist in NFPA, and note that
   none of them is a ceiling-void depth `[all UNCONFIRMED]`:
   - noncombustible / limited-combustible spaces with minimal combustible loading and **no access**
   - joist channels fire-stopped into volumes **not exceeding 160 ft³ (4.5 m³)**, where a noncombustible
     or limited-combustible ceiling is attached to composite wood joists directly or on metal channels no
     deeper than 1 in (25 mm)
   - spaces **filled with noncombustible insulation** (to within about 2 in of the top)
   - concealed spaces over **isolated small rooms not exceeding 55 ft² (5.1 m²)**
   - tight combustible cavities, under about **6 in (152 mm)** between joists

**"Minimal combustible loading" is not defined numerically in the standard.** Sources agree it comes down
to engineering judgement and the AHJ. That is a real limit on how far this Brain can take you: it can
measure the void and report what is in it, but it cannot decide that question. Say so rather than implying
a clean answer exists.

The thing that most often flips a void from "omit" to "protect" is **not the structure at all** — it is
what got run through it later. Cable bundles, plastic pipe, insulation, stored material above a tile
ceiling. A void that qualified for omission at design stage may not qualify as built.

## What it means for the model — two layers, not one

When the void does need protection, the room becomes **two separate layouts**, and they are not copies of
each other:

| | Below the ceiling | Inside the void |
|---|---|---|
| Head type | **pendent** (or concealed/recessed) | **upright** |
| Reference plane for the height | the finished ceiling underside | the slab soffit, or the beam soffit where beams hang |
| Construction type | usually unobstructed | often **obstructed** — beams, ducts and trays are all in there |
| Consequence | the normal grid | frequently a tighter grid, and possibly heads in bays |

The trap: **people lay out the void by copying the ceiling grid up.** It is usually wrong. The void is a
different construction type, so its maximum area per head can be smaller, and it is full of obstructions
that the room below does not have. Run the whole chain again for the void as its own job — survey, grid,
obstruction check, height.

Two more practical points:

- The void heads are usually fed off the **same branch pipework**, with the pendent below dropping through
  the ceiling. That is a piping arrangement, not a layout rule — but it is why the two layouts want their
  branch runs aligned where possible.
- **An upright in the void still needs its own clearance to the deck**, and it still obeys the obstruction
  rules against the beams up there. See [`obstructions.md`](obstructions.md).

## Getting the void depth out of the model

[`scripts/recipes/sprinkler-obstruction-survey.cs`](../../scripts/recipes/sprinkler-obstruction-survey.cs)
already finds the ceiling underside and the slab soffit above a room, so it reports the **void depth**
directly and flags it against a threshold you set (`voidDepthThresholdMm`, defaulting to the BS/EN 800).
The flag is a prompt to ask the question, never an answer: it says *"this void is 950 mm — which standard
is this project on, and what is the void made of?"*

Caveats that come with that number, all in the fragment's header too:

- It is measured from **bounding boxes** — the ceiling's top and the slab's bottom — so it is right for a
  flat ceiling under a flat slab and approximate for anything sloped or stepped.
- **Beams inside the void are not deducted.** The clear depth under a downstand beam is less than the
  reported void depth, sometimes much less, and it is the clear depth a sprinkler actually lives in.
- A **linked** architectural ceiling or structural slab will not be found at all, and the survey then
  reports no ceiling rather than no void. Check the link situation before believing a clean result.

## Sources consulted (2026-08-20 — search snippets only, pages could not be fetched)

- NFSA — concealed spaces in NFPA 13, parts I and II
- UpCodes — concealed spaces not requiring sprinkler protection
- WoodWorks — sprinkler requirements for concealed spaces in light-frame projects
- Secondary summaries of BS 5306-2 and BS EN 12845 for the 800 mm figure and the 75–150 mm deflector
  window; EN 12845 superseded BS 5306-2, and the 800 mm requirement is attributed to that lineage

**None of these is the standard itself.** Confirm the 800 mm against BS EN 12845 and the omission list
against your adopted NFPA edition before either drives a drawing.
