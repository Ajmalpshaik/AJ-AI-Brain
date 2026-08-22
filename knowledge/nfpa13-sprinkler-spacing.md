# NFPA 13 sprinkler spacing — the rules a layout must satisfy

> Reference knowledge for [`skills/ajtools-fire-sprinkler-layout/SKILL.md`](../skills/ajtools-fire-sprinkler-layout/SKILL.md).
> Index: [`INDEX.md`](INDEX.md).

**Read this before laying out a single sprinkler.** Fire fighting is not the same job as HVAC or lighting
coverage: a sprinkler layout is governed by a code with *several simultaneous* limits, and a layout that
satisfies one and breaks another is not a layout. Geometric coverage — "no gaps" — is not one of the
NFPA rules at all.

## The hard boundary of this file

- This is a **paraphrase with section pointers, not the code**. NFPA 13 is a copyrighted standard and its
  tables are edition-specific. Every number below must be confirmed against **the edition your project has
  actually adopted** before it is used on a drawing. Editions renumber sections (the obstruction rules moved
  from 8.6.5 in the 2019 edition to 10.2.6 in 2022+), and values differ by construction type in ways the
  freely available summaries paraphrase inconsistently — sources found in July 2026 variously reported light
  hazard as 225, 200 and "130–200" ft².
- **The AHJ overrules this file.** For the user's projects the AHJ is the **Qatar Civil Defence Department
  (QCDD)**, which enforces the NFPA suite *plus* its own General Fire Safety Requirements. QCDD requirements
  and project specifications sit ON TOP of NFPA and can be stricter. Never present an NFPA-only check as
  "compliant".
- **This does not replace a fire protection engineer.** Head count, spacing and hydraulics are a licensed
  design responsibility. What this Brain does is produce a *geometrically correct candidate layout* and
  report which code limits it satisfies or breaks, with the numbers shown, so a competent person can judge it.
  It does not decide hazard class, density, or whether a design is acceptable.

## Hazard class is an INPUT — never assume it

Every spacing number depends on it, and it is not derivable from the model. Ask, and restate what was used.

| Hazard class | Typical occupancies |
|---|---|
| Light | offices, schools, hospitals, churches, theatres, most residential-type spaces |
| Ordinary I & II | mercantile, manufacturing, machine shops, garages, bakeries, restaurant service areas |
| Extra I & II | plastics processing, spray painting, chemical handling, metal extrusion |
| High-piled storage | racked/piled storage above the height thresholds — its own rule set entirely |

## Rule 1 — maximum protection area per sprinkler (NFPA 13 §10.2.4.2, Tables 10.2.4.2.1(a)–(d))

The single limit most often missed, because a covering algorithm has no reason to look at it.

| Hazard class | Max area per sprinkler | In metric | Notes |
|---|---|---|---|
| Light | 225 ft² | ≈ 20.9 m² (NFPA prints 21 m²) | the general light-hazard ceiling |
| Light, combustible **obstructed** construction, members < 3 ft apart | 130 ft² | ≈ 12.1 m² | closely spaced combustible members cut it hard |
| Ordinary I & II | 130 ft² | ≈ 12.1 m² | |
| Extra hazard, density ≥ 0.25 gpm/ft² | 100 ft² | ≈ 9.3 m² | |
| Extra hazard, density < 0.25 gpm/ft² | 130 ft² | ≈ 12.1 m² | |

**Consequence for a layout: this sets a hard MINIMUM head count** = room area ÷ max area per sprinkler,
rounded up. A layout with fewer heads than that fails no matter how the circles look.

## Rule 2 — maximum spacing between sprinklers (§10.2.4.2.1)

| Hazard class | Max head-to-head | In metric |
|---|---|---|
| Light | 15 ft | **4,572 mm** |
| Ordinary | 15 ft | **4,572 mm** |
| Extra / high-piled, density ≥ 0.25 gpm/ft² | 12 ft | **3,658 mm** |
| Extra / high-piled, density < 0.25 gpm/ft² | 15 ft | **4,572 mm** |

**4,572 mm, not 4,600.** A "round" 4,600 mm cap is 28 mm LENIENT and will pass a layout the code fails.
Convert the foot value; never work from a remembered metric approximation.

## Rule 3 — distance to walls (§10.2.5.2, §10.2.5.3)

- **Maximum: one half of the allowable head-to-head spacing**, measured perpendicular to the wall.
  At 15 ft spacing → 7 ft 6 in = **2,286 mm**. At 12 ft → 6 ft = 1,829 mm.
- **0.75 × spacing** is permitted in angled corners (§10.2.5.2.2).
- **Minimum: 4 in = 102 mm** from a wall, for pendent, upright and sidewall alike (§10.2.5.3).
- **Small room rule (§10.2.5.2.3)**: in a *light hazard* compartment of unobstructed construction and
  **≤ 800 ft² (74.3 m²)**, a head may sit up to **9 ft (2,743 mm)** from the wall. Only light hazard, only
  unobstructed, only under the area threshold — it is not a general allowance.

## Rule 4 — minimum spacing between sprinklers (§10.2.5.4)

**6 ft = 1,829 mm** on centre, to stop one head's spray cooling the adjacent head and delaying its
operation. Exception: in-rack sprinklers (§10.2.5.4.3). A dense layout can violate this while covering
perfectly — so a minimum-spacing check is as necessary as a maximum one.

## Rules 5 to 7 moved — they grew past a pointer (2026-08-20)

Deflector heights, obstructions, and the sidewall/extended-coverage tables were three short bullets here
until Ajmal asked for the whole subject studied properly. They are now full chunks of their own, and this
file deliberately does **not** repeat them — one fact, one place:

| | |
|---|---|
| **Deflector heights** — with a ceiling, without one, obstructed construction, sloped ceilings, ceiling pockets, the 18 in clearance | [`fire-sprinkler/deflector-and-ceiling-height.md`](fire-sprinkler/deflector-and-ceiling-height.md) |
| **Obstructions** — the three-times rule and its column exception, the beam table, wide obstructions, heads in bays | [`fire-sprinkler/obstructions.md`](fire-sprinkler/obstructions.md) |
| **Sidewall, extended coverage, concealed, upright, dry** — which head, and what changes with it | [`fire-sprinkler/sprinkler-types.md`](fire-sprinkler/sprinkler-types.md) |
| **The method end to end**, and the Revit side of it | [`fire-sprinkler/layout-method.md`](fire-sprinkler/layout-method.md), [`fire-sprinkler/revit-modelling.md`](fire-sprinkler/revit-modelling.md) |
| Folder index | [`fire-sprinkler/README.md`](fire-sprinkler/README.md) |

One thing that surfaced in the move and is worth keeping: **the old Rule 6 bullet here quoted "2.5 in at
1 ft, 5.5 in at 3 ft, 22 in at 10 ft", which is not the beam table** — a table reaching 22 in only at
around 10 ft is the *obstruction-against-a-wall* one, while the beam table reaches its 24 in maximum at
about 6 ft. Two different tables, easy to conflate, and conflating them is lenient in one direction and
strict in the other. Both are described in [`fire-sprinkler/obstructions.md`](fire-sprinkler/obstructions.md).

## Worked reference — the user's Project1 'Room 4' (287.7 m², 3,097 ft²), measured 2026-07-27

Minimum head count from the AREA rule alone:

| Hazard class | Max area/head | Minimum heads |
|---|---|---|
| Light (general) | 225 ft² | **14** |
| Light (combustible obstructed, members < 3 ft) | 130 ft² | **24** |
| Ordinary I & II | 130 ft² | **24** |
| Extra hazard ≥ 0.25 gpm/ft² | 100 ft² | **31** |

The two coverage layouts drawn in that room on 2026-07-27, judged against real NFPA numbers:

| | Heads | Area each | Spacing | To wall | Verdict |
|---|---|---|---|---|---|
| Square inset (blue) | 20 | 155 ft² | 4,140 mm ✓ | 2,070 mm ✓ | spacing and wall PASS; area 155 ft² is **light hazard only** |
| Staggered (magenta) | 19 | 163 ft² | 4,633 mm ✗ | 2,317 mm ✗ | FAILS both against 4,572 / 2,286 mm |

**The lesson worth carrying:** both layouts had verified 100% geometric coverage with zero gaps, and
neither fact told us anything about code compliance. On ordinary hazard both are short by at least 4 heads
purely on the area rule — while still covering every square metre. Coverage and compliance are different
questions, and the 3 m "coverage radius" used to draw those circles is not an NFPA concept at all.

## Sources consulted (July 2026 — secondary summaries, not the standard itself)

> **These links stay — Ajmal's decision, 2026-08-22: "keep the nfpa links".** They are the one deliberate
> exception to the no-outside-sources rule, and the reason is safety, not sentiment: these are fire-code
> values, the heading above warns they are *secondary summaries rather than the standard itself*, and
> stripping the sources would leave unverified life-safety numbers looking authoritative. A future
> session tidying outside references must skip this section. `tools/verify-consistency.mjs` check 11
> excludes it for the same reason.


- [QRFS — maximum and minimum sprinkler distance rules, standard spray](https://blog.qrfs.com/214-maximum-and-minimum-sprinkler-distance-rules-part-1-standard-spray-fire-sprinklers/)
- [QRFS — obstruction distance rules](https://blog.qrfs.com/225-distance-rules-part-2-sprinkler-head-obstruction-distance-rules-for-standard-spray-fire-sprinklers/)
- [sprinkler.wiki — obstructions to spray](https://sprinkler.wiki/docs/obstructions)
- [UpCodes — maximum protection area of coverage (NFPA 13 §10.2.4.2)](https://up.codes/s/maximum-protection-area-of-coverage)
- [UpCodes — maximum distance from walls](https://up.codes/s/maximum-distance-from-walls)
- [NFSA — the NFPA 13 small room rule FAQ](https://nfsa.org/2020/12/01/the-nfpa-13-small-room-rule-frequently-asked-questions/)
- [Archtoolbox — fire sprinkler head spacing and location](https://www.archtoolbox.com/fire-sprinkler-head-spacing-and-location/)
- [Qatar Civil Defence — fire prevention requirements](https://fire-matrix.org/wp-content/uploads/2021/12/Qatar-Civil-Defense.pdf)

## Worked layout — Room 4, Ordinary Hazard I/II, standard spray (2026-07-27)

First layout produced through the fire-sprinkler skill rather than the generic coverage recipe. The grid
was derived FROM the limits — smallest `nx × ny` that satisfies all of them at once — not from a chosen
radius: **6 columns × 4 rows = 24 heads**, S = 3,450 mm along the branch, L = 3,475 mm between branches,
wall inset 1,725 / 1,738 mm.

| Rule | Limit | As built | |
|---|---|---|---|
| Area per head, `A_s = S × L` | 130 ft² | 129.0 ft² (11.99 m²) | PASS |
| Head count vs area-rule minimum | ≥ 24 | 24 | PASS |
| Max head-to-head | 4,572 mm | 3,475 mm | PASS |
| Min head-to-head | 1,829 mm | 3,450 mm | PASS |
| Max to wall | 2,286 mm | 1,738 mm | PASS |
| Min to wall | 102 mm | 1,725 mm | PASS |
| Heads inside the room | all | 24/24 | PASS |

**Compute area per head as `A_s = S × L`, not room-area ÷ head-count.** NFPA defines it from the grid
dimensions — S is the distance along the branch line to the adjacent sprinkler (or twice the distance to
the wall, whichever is greater) and L is the distance between branch lines. The two methods agree only when
the grid tiles the room exactly with half-spacing insets, as here; on an irregular room or an off-centre
grid they diverge, and `S × L` is the one the code means.

Margin note: 129.0 against a 130 ft² cap is **1 ft² of headroom**. Geometrically valid, but a reviewer may
want more; 5 × 5 = 25 heads gives 123.9 ft² if margin is preferred over head count.

Drafting note: the circles were drawn at **r = 2,448 mm = half the cell diagonal** — the farthest any floor
point can be from its head. That radius is *derived from the code grid* so the drawing means something;
it is not a sprinkler throw or an NFPA quantity. Verified 3,243/3,243 floor points within it, no gaps.
