# NFPA 13 vs BS EN 12845 — where they agree, and where they quietly don't

> Chunk of [`README.md`](README.md). The NFPA numbers are [`../nfpa13-sprinkler-spacing.md`](../nfpa13-sprinkler-spacing.md);
> the void rule that started this is [`concealed-spaces.md`](concealed-spaces.md).

Written 2026-08-20 after Ajmal asked whether the 800 mm ceiling-void rule was NFPA. It was not — it is
BS EN 12845. He then asked the right follow-up: *"is there anything like that?"* There is, and there is a
pattern to it.

## The finding that explains the whole problem

**The two standards agree almost exactly on the headline number, and disagree on the details.**

| Max floor area per head | NFPA 13 | BS EN 12845 | |
|---|---|---|---|
| Light hazard | 225 ft² = **20.9 m²** | **21 m²** | effectively the same |
| Ordinary hazard | 130 ft² = **12.1 m²** | **12 m²** | effectively the same |

That is why everyone assumes the standards are interchangeable — the number they check first *is* the
same. Then the details bite, and they bite silently, because nothing about a head count reveals which
rulebook produced it.

**So the rule is: name the standard before you name the number.** Not because the area limit changes, but
because everything around it does.

## Where they actually diverge

`[Every row UNCONFIRMED — search snippets only, 2026-08-20. Confirm against the adopted documents.]`

| | NFPA 13 | BS EN 12845 | Who is stricter |
|---|---|---|---|
| **Deflector below a smooth ceiling** | 25–305 mm (1–12 in) | **75–150 mm** | **EN, by a lot.** A 250 mm drop is fine under NFPA and illegal under EN |
| **Minimum spacing between heads** | 6 ft = **1,829 mm** | **2,000 mm** | **EN.** A layout at 1.9 m passes NFPA and fails EN |
| **Ceiling void sprinklers** | no depth trigger — tests combustibility | **≥ 800 mm depth**, reported as regardless of materials | depends entirely on the void — see [`concealed-spaces.md`](concealed-spaces.md) |
| **Sidewall max area per head** | light 196 ft² ≈ 18.2 m² | light **17 m²**, ordinary **9 m²** | EN on light hazard |
| **Hazard classes** | Light / Ordinary Gp 1–2 / Extra Gp 1–2 | LH / **OH1–OH4** / HHP / HHS | **they do not map one to one** |
| **Distance to walls** | max half the spacing, min 4 in = 102 mm | different figures quoted, not resolved | **unresolved — check it** |

### The two that will actually catch you

**1. The deflector window.** NFPA gives you 25–305 mm; EN gives you 75–150 mm. A habit of "100 mm below
the ceiling" is comfortably inside both. A habit of "250" or "50" is fine under one and non-compliant
under the other — and nobody re-checks a mounting height that has worked for years. This is the single
most likely place for a career habit to be quietly wrong on a European-spec job.

**2. Hazard classes do not translate.** "Ordinary hazard" is not one thing. EN splits it into **four**
groups (OH1–OH4) and adds High Hazard Process and Storage as separate classes. NFPA has Ordinary Groups 1
and 2 and Extra Groups 1 and 2. Reading a spec that says "OH3" and laying out to NFPA "Ordinary Hazard" is
not a translation — it is a guess. The design density and area of operation behind them are different too
(EN light hazard is commonly quoted at 2.25 mm/min over 84 m², NFPA light hazard at about 4.1 mm/min over
139 m²), though that half is hydraulics and out of this Brain's scope.

## What this means for a layout, practically

The tools in this Brain take every limit as an **input**, so they work under either standard — that was
deliberate, and this is why it matters. To lay out to EN instead of NFPA you change the numbers you feed
in, not the fragment:

| Fragment input | NFPA (ordinary) | EN 12845 (OH) |
|---|---|---|
| `maxAreaPerHeadM2` | 12.08 | 12.0 |
| `maxSpacingMm` | 4572 | confirm from the EN table |
| `minSpacingMm` | 1829 | **2000** |
| deflector window in `sprinkler-deflector-height.cs` | 25 / 305 | **75 / 150** |
| `voidDepthThresholdMm` in the survey | not an NFPA concept | **800** |

`sprinkler-nfpa-grid.cs` and `sprinkler-compliance-audit.cs` both take a `standardLabel` and print it on
every report, so a check table can never come back without saying which rulebook it was measured against.
That exists because of this file: a head count with no standard named is the same failure as a head count
with no hazard class named.

## Which standard is on this job

**Ask, at the start, and write the answer into the job record.** It is not derivable from the model and it
is not guessable from the country.

- **QCDD enforces the NFPA suite** — so NFPA is the default on Ajmal's Qatar work.
- **But the project specification wins**, and Gulf projects specify BS EN 12845 often enough that it
  cannot be assumed away — particularly with a European consultant, a European insurer, or an FM Global /
  LPC-influenced brief.
- Some projects run **both**: NFPA for the building, an insurer's stricter rules for a specific area.

If nobody can say which, that is a real finding to report, not a gap to fill with a default.

## The pattern worth carrying beyond sprinklers

Ajmal remembered the 800 mm correctly and attributed it to the wrong standard. That is not carelessness —
it is what site knowledge looks like. Numbers survive in memory; the rulebook they came from does not.

The defence is cheap: **every number this Brain reports says where it came from.** Every check table names
its standard and hazard class. Every unconfirmed value carries the tag. It costs a line of output and it
turns a confidently wrong answer into a checkable one.

## Sources consulted (2026-08-20 — search snippets only, no source document could be fetched)

- Comparisons of NFPA 13 against EN 12845 (hazard classes, densities, zone sizes, water supply)
- Secondary summaries of BS EN 12845 coverage areas (LH 21 m², OH 12 m², sidewall 17/9 m²), the 2 m
  minimum spacing, and the 75–150 mm deflector window
- BS 5306-2 / BS EN 12845 lineage for the 800 mm ceiling-void trigger

**None is the standard itself, and one wall-distance claim could not be resolved at all** — it is left in
the table as unresolved rather than given a number, which is the honest state of it.
