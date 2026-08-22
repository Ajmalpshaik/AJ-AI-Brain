# Duct sheet-metal takeoff — gauge, sheet area and weight

Back to [`INDEX.md`](INDEX.md).

The job: *"how many kilos of ductwork is this"*, *"what gauge should this duct be"*, *"give me the sheet
metal weight for the BOQ"*. Procurement and fabrication, not airflow.

**Not** [`live-model/hvac-duct-sizing.md`](live-model/hvac-duct-sizing.md), which sizes a duct from its
flow. That decides how big the duct is; this decides how thick the metal is and what it weighs once it
is that big.

Harvested 2026-08-22 from the add-in's Duct Standard tool. These are **Ajmal's own working values**,
shipped as its defaults — treat them as this office's standard, not as a code citation, and confirm the
project's own table before quoting a figure to anyone.

## The four steps

1. **Shape** — rectangular, round or oval.
2. **Governing size** — the number the gauge table is looked up on.
3. **Gauge / thickness** — from a size band, per shape *and* per pressure class.
4. **Sheet area → weight** — area × thickness × density, then fabrication allowances.

## Step 1 — shape, and how to read it reliably

Ask the duct **type** for its `Shape` property first. When that is unavailable, fall back to which size
parameters actually carry a value:

| Has width + height | Has diameter | Shape |
|---|---|---|
| yes | no | rectangular |
| no | yes | round |
| yes | **yes** | **oval** |

That last row is the useful one: an oval duct carries width, height **and** diameter, so testing for
"has a diameter" alone would call every oval duct round.

## Step 2 — governing size

| Shape | Governing size |
|---|---|
| rectangular, oval | **max(width, height)** — the longer side, not the perimeter and not the average |
| round | the diameter |

## Step 3 — gauge and thickness, by shape and pressure class

Pressure class is read from a duct parameter (default name *"Duct Pressure Class"*), falling back to
**low** when it is blank. A size band is inclusive at both ends and the first matching band wins.

**Rectangular** (mm, governing size)

| Band | Low | Medium | High |
|---|---|---|---|
| 0–400 | 0.60 mm / 26 g | 0.80 mm / 24 g | 1.00 mm / 22 g |
| 401–550 | 0.80 mm / 24 g | 1.00 mm / 22 g | 1.20 mm / 20 g ✱ |
| 551–1200 | 1.00 mm / 22 g ✱ | 1.20 mm / 20 g ✱ | 1.50 mm / 18 g ✱ |
| 1201+ | 1.20 mm / 20 g ✱ | 1.50 mm / 18 g ✱ | 1.90 mm / 16 g ✱ |

**Oval** uses the same table as rectangular.

**Round** (mm diameter) — different bands, and note they are not the same numbers as rectangular

| Low | | Medium | | High | |
|---|---|---|---|---|---|
| 0–406 | 0.55 / 26 g | 0–381 | 0.61 / 24 g | 0–381 | 0.76 / 22 g |
| 407–559 | 0.70 / 24 g | 382–686 | 0.76 / 22 g | 382–686 | 0.91 / 20 g |
| 560–1219 | 0.86 / 22 g | 687–1067 | 0.91 / 20 g | 687–1067 | 1.21 / 18 g ✱ |
| 1220+ | 1.00 / 20 g ✱ | 1068–1524 | 1.21 / 18 g ✱ | 1068–1524 | 1.52 / 16 g ✱ |
| | | 1525+ | 1.52 / 16 g ✱ | 1525+ | 1.90 / 14 g ✱ |

**✱ = reinforcement required**, which adds its own weight allowance (below).

## Step 4 — sheet area, then weight

Area is the **developed sheet**: perimeter × length.

| Shape | Sheet area (m²) |
|---|---|
| rectangular | `2 × (w + h) × L` |
| round | `π × d × L` |
| **oval** | **Ramanujan's approximation** — see below |

An oval's perimeter has no closed form, so use Ramanujan with semi-axes `a = w/2`, `b = h/2`:

```
P ≈ π × [ 3(a + b) − sqrt( (3a + b)(a + 3b) ) ]
area = P × L
```

Then:

```
base weight (kg) = area_m² × (thickness_mm / 1000) × density_kg/m³
```

**Densities:** GI **7850**, black steel **7850**, stainless steel **8000**, aluminium **2700** kg/m³.
Default GI when the duct's material parameter is blank.

## The allowances — this is where the real number comes from

Base sheet weight is not the delivered weight. The allowances are added as **percentages of the base**,
summed and applied once:

| Allowance | Default |
|---|---|
| Seam | 3% |
| Joint | 2% |
| Flange | 4% |
| Fittings | 10% |
| Wastage | 5% |
| **Reinforcement** (only when the gauge rule says so) | 5% |

```
total = base × (1 + (seam + joint + flange + fittings + wastage [+ reinforcement]) / 100)
```

So an unreinforced duct carries **+24%** over bare sheet, and a reinforced one **+29%**. Quoting the
base weight as "the weight" understates a job by roughly a quarter — that is the single most useful
thing on this page.

**Fittings at 10% is an allowance, not a measurement.** It stands in for bends, tees and transitions
that are not being counted individually. If the model's fittings *are* being counted separately, take
this to zero or you will double-count them.

## Traceability

Record, per duct, which rule produced the number — standard name, shape, pressure class and the size
band. A weight nobody can trace back to a band is a number nobody will sign.

## The fragment

[`../scripts/actions/reporting/action-report-duct-weight.cs`](../scripts/actions/reporting/action-report-duct-weight.cs)
— read-only, groups by size and reports kg with the allowances shown separately from the base.
