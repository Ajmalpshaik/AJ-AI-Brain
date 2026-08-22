# Domestic water pipe sizing — fixture units to pipe size

Back to [`INDEX.md`](INDEX.md).

Sizing a **cold or hot water supply** run: count what it serves, turn that into a probable flow, pick the
smallest pipe that keeps velocity under the limit, then check the friction loss. This is the classic
Hunter's-curve method.

**Not** for drainage or vent sizing (different fixture-unit tables, different rules), and not for duct
sizing — that is [`live-model/hvac-duct-sizing.md`](live-model/hvac-duct-sizing.md), which uses a
velocity limit too but nothing else in common.

Harvested 2026-08-22 from the add-in's Pipe Sizing tool, which is Ajmal's own working method.

## The four steps

1. **Add up water supply fixture units (WSFU)** for everything downstream of the run — table below.
2. **Convert WSFU to probable demand in GPM** off Hunter's curve — table below. Flush *valve* and flush
   *tank* systems have different curves.
3. **Size on velocity**: the smallest pipe whose **real internal diameter** is at least
   `d = sqrt( (Q / v) × 4/π )`.
4. **Check friction loss** with Hazen-Williams and, if it is too high for the available head, go up a
   size.

Every number is a per-request input. **Ask the velocity limit, don't assume it** — the general rule in
[`../START-HERE.md`](../START-HERE.md) applies here as much as anywhere.

## Step 1 — fixture units

Each fixture contributes to cold, hot, and total. **Total is not cold + hot** — a fixture that draws from
both never draws its full share of each at once, so the total is the diversified figure. Use the column
that matches the run you are sizing.

| Fixture | Cold | Hot | Total |
|---|---|---|---|
| Bathroom group, private, flush tank | 2.7 | 1.5 | 3.6 |
| Bathroom group, private, flush valve | 6.0 | 3.0 | 8.0 |
| Bathtub, private | 1.0 | 1.0 | 1.4 |
| Bathtub, public | 3.0 | 3.0 | 4.0 |
| Bidet | 1.5 | 1.5 | 2.0 |
| Combination fixture | 2.25 | 2.25 | 3.0 |
| Dishwasher, private | 0.0 | 1.4 | 1.4 |
| Dishwasher, commercial | 0.0 | 2.0 | 2.0 |
| Drinking fountain | 0.25 | 0.0 | 0.25 |
| Kitchen sink, private | 1.0 | 1.0 | 1.4 |
| Kitchen sink, restaurant | 3.0 | 3.0 | 4.0 |
| Bar sink | 1.0 | 1.0 | 1.4 |
| Laundry trays (1–3), private | 1.0 | 1.0 | 1.4 |
| Lavatory, private | 0.5 | 0.5 | 0.7 |
| Lavatory, public | 1.5 | 1.5 | 2.0 |
| Service sink | 2.25 | 2.25 | 3.0 |
| Mop basin / janitor sink | 2.25 | 2.25 | 3.0 |
| Wash sink (per set of faucets) | 1.5 | 1.5 | 2.0 |
| Shower head, private | 1.0 | 1.0 | 1.4 |
| Shower head, public | 3.0 | 3.0 | 4.0 |
| Urinal, 1" flush valve | 10.0 | 0.0 | 10.0 |
| Urinal, 3/4" flush valve | 5.0 | 0.0 | 5.0 |
| Urinal, flush tank | 3.0 | 0.0 | 3.0 |
| Water closet, private, flush tank | 2.2 | 0.0 | 2.2 |
| Water closet, private, flush valve | 6.0 | 0.0 | 6.0 |
| Water closet, public, flush tank | 5.0 | 0.0 | 5.0 |
| Water closet, public, flush valve | 10.0 | 0.0 | 10.0 |
| Clinic sink (flush valve) | 8.0 | 0.0 | 8.0 |
| Washing machine, private (8 lb) | 1.0 | 1.0 | 1.4 |
| Washing machine, public (8 lb) | 2.25 | 2.25 | 3.0 |
| Washing machine, public (15 lb) | 3.0 | 3.0 | 4.0 |
| Hose bibb (1/2" connection) | 2.5 | 0.0 | 2.5 |
| Hose bibb (3/4" connection) | 3.0 | 0.0 | 3.0 |

## Step 2 — fixture units to GPM (Hunter's curve)

**Interpolate linearly between rows.** Don't round the fixture-unit total up to the next row — that
oversizes.

| WSFU | Flush tank GPM | Flush valve GPM | | WSFU | Flush tank | Flush valve |
|---|---|---|---|---|---|---|
| 1 | 3.0 | — | | 20 | 19.6 | 35.0 |
| 2 | 5.0 | — | | 25 | 21.5 | 38.0 |
| 3 | 6.5 | — | | 30 | 23.3 | 41.0 |
| 4 | 8.0 | — | | 35 | 24.9 | 43.8 |
| 5 | 9.4 | 15.0 | | 40 | 26.3 | 46.5 |
| 6 | 10.7 | 17.4 | | 45 | 27.7 | 49.2 |
| 7 | 11.8 | 19.8 | | 50 | 29.1 | 51.5 |
| 8 | 12.8 | 22.2 | | 60 | 32.0 | 54.0 |
| 9 | 13.7 | 24.6 | | 70 | 35.0 | 58.0 |
| 10 | 14.6 | 27.0 | | 80 | 38.0 | 62.0 |
| 11 | 15.4 | 27.8 | | 90 | 41.0 | 66.0 |
| 12 | 16.0 | 28.6 | | 100 | 43.5 | 71.0 |
| 13 | 16.5 | 29.4 | | 120 | 48.0 | 77.0 |
| 14 | 17.0 | 30.2 | | 140 | 52.5 | 83.0 |
| 15 | 17.5 | 31.0 | | 160 | 57.0 | 89.0 |
| 16 | 18.0 | 31.8 | | 180 | 61.0 | 95.0 |
| 17 | 18.4 | 32.6 | | 200 | 65.0 | 101.0 |
| 18 | 18.8 | 33.4 | | 225 | 70.0 | 107.0 |
| 19 | 19.2 | 34.2 | | 250 | 75.0 | 113.0 |
| | | | | 275 | 80.0 | 118.0 |
| | | | | 300 | 85.0 | 124.0 |
| | | | | 400 | 105.0 | 148.0 |
| | | | | 500 | 124.0 | 170.0 |
| | | | | 750 | 170.0 | 208.0 |
| | | | | 1000 | 208.0 | 239.0 |

**Two limits worth stating out loud:**

- **The flush-valve curve does not start until 5 WSFU.** Below that, use the flush-tank column even on a
  flush-valve system — there is no flush-valve figure to use.
- **The table stops at 1,000 WSFU / 239 GPM and the flow is clamped there.** Feed it 5,000 fixture units
  and it still says 239 GPM. On anything that big, say the table has run out rather than reporting a
  number that has stopped growing.

## Step 3 — velocity sizing, against REAL internal diameters

```
Q  (m³/s) = GPM × 0.0000630901964
d  (mm)   = sqrt( (Q / v) × 4/π ) × 1000        v = velocity limit in m/s
```

Then take the **first pipe whose internal diameter is ≥ d** — next size up, never down. If nothing in the
table is big enough, the largest size is used and that must be reported, not hidden.

**Size against internal diameter, never nominal.** A "1 inch" pipe is 26.6 mm bore in uPVC Sch 40 and
22.9 mm in CPVC SDR 11 — a 14% difference in bore, which is a 30% difference in area. Nominal size is a
label, not a dimension.

Internal diameters in mm, by material:

| Nominal | uPVC Sch 40 | CPVC SDR 11 | PPR SDR 7.4 | Copper Type L |
|---|---|---|---|---|
| 1/2" | 15.80 | 12.42 | 14.4 (20 mm) | 13.84 |
| 3/4" | 20.93 | 18.16 | 18.0 (25 mm) | 19.94 |
| 1" | 26.64 | 22.89 | 23.2 (32 mm) | 26.04 |
| 1 1/4" | 35.05 | 28.58 | 29.0 (40 mm) | 32.13 |
| 1 1/2" | 40.89 | 33.88 | 36.2 (50 mm) | 38.23 |
| 2" | 52.50 | 44.17 | 45.8 (63 mm) | 50.42 |
| 2 1/2" | 62.71 | 55.00 | 54.4 (75 mm) | 62.61 |
| 3" | 77.93 | 65.00 | 65.4 (90 mm) | 74.80 |
| 4" | 102.26 | 88.00 | 79.8 (110 mm) | 99.19 |
| 6" | 154.05 | 131.00 | 116.0 (160 mm) | 148.00 |

**Then compute the ACTUAL velocity in the pipe you picked** — `v = Q / (π × (d/2)²)` — and report it.
It is always lower than the limit (you rounded up), and the number tells you whether you have gone a size
too far.

## Step 4 — friction loss (Hazen-Williams)

```
psi per 100 ft = 4.52 × Q^1.852 / ( C^1.852 × d_inches^4.87 )       Q in GPM
```

The `^4.87` on diameter is why one size up transforms the pressure drop: going 1" → 1 1/4" in uPVC
(26.64 → 35.05 mm bore) cuts friction loss to about **26%** of what it was.

C-factors in use here: **uPVC 150, CPVC 150, PPR 150, copper 130.**

> Copper at 130 is the conservative end — much published work uses 140–150 for copper tube, and the other
> three materials here are all at 150. It is recorded as Ajmal's own working value because that is what
> the tool ships. Worth a deliberate decision before quoting a friction figure to a consultant; the
> effect is real (C 130 vs 150 is roughly **+30%** head loss for the same flow and bore).

## Sanity checks before believing a result

- **Cold vs hot vs total** — sizing a hot run off the total column oversizes it badly. Water closets and
  urinals contribute **zero** to hot.
- **Velocity limit is a per-request input.** Typical practice is around 2 m/s in occupied areas (noise)
  and higher on plant-room runs, but the number comes from the project, not from here.
- **Friction loss is per 100 ft**, so multiply by the real developed length — including an allowance for
  fittings — before comparing it against available head.
- **Interpolation, not rounding**, at step 2.

## The fragment

[`../scripts/recipes/size-domestic-water-pipe.cs`](../scripts/recipes/size-domestic-water-pipe.cs) —
does the whole calculation, and can either take a fixture-unit total directly or count Plumbing Fixtures
out of the live model and add up their WSFU.
