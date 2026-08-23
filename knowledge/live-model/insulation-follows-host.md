# Insulation and lining must follow their host into any colour change

**Ajmal's standing rule, 2026-08-23, in his own words:**

> *"if you are coloring the ducts or duct accessorys or any thing it means if the items there is
> linsilation you have to color the isulation also other vise we ccant see can you do it that"*

This is not a per-job preference to re-ask. It applies to **every** colouring or highlighting job on a
live model, whatever the category — ducts, duct accessories, pipes, equipment, anything that can carry a
wrap.

## Why it matters on screen

Insulation is a **separate element** in Revit (`DuctInsulation`, `DuctLining`, `PipeInsulation` — all
derive from `InsulationLiningBase`). It is not a property of the duct. So a graphic override applied to
the duct does nothing to the sleeve wrapped around it.

In a "highlight one thing, grey the rest" view that produces the worst possible result: the element you
asked to see is painted red, then **covered by its own dimmed grey jacket**. From outside, the thing
reads as grey. The highlight looks like it failed even though the override was applied correctly and the
tool reported success. That is what Ajmal means by *"other vise we ccant see"*.

Reporting "41 highlighted, 0 skipped" while he is looking at a grey model is exactly the kind of
technically-true-but-wrong answer this Brain exists to stop.

## How to do it

[`../../scripts/actions/color-graphics/action-highlight-vs-rest.cs`](../../scripts/actions/color-graphics/action-highlight-vs-rest.cs)
has the switch built in — **`expandInsulationAndLining = true`, and leave it on.** It walks both
directions: host → its insulation and lining, and wrap → the host underneath it.

Setting it to `false` is only correct when reproducing the pre-2026-08-22 behaviour for a comparison.
Do not set it false to "keep to the proven path" — that was the mistake made on 2026-08-23, twice,
before Ajmal had to point it out.

## Verified live

2026-08-23, Revit 2020, model `4355-BHVD-3D-60A10-BL003A.rvt`, view `{3D - ajmal.al}`:

| Run | Matched | Wraps pulled in | Total red | Greyed |
|---|---:|---:|---:|---:|
| VCDs, switch **off** | 41 | 0 | 41 | 2,093 |
| VCDs, switch **on** | 41 | 40 | **81** | 2,053 |

That model holds **620 duct insulation elements**, so on this job the switch was the difference between
a working highlight and an invisible one.

## The count will not match — that is normal

40 wraps followed 41 VCDs, because one VCD carries no insulation. An unequal count is the expected
shape of the result, not a sign that elements were missed. Say so when reporting, so the mismatch does
not read as a bug.

## Related

- [`graphic-override-precedence.md`](graphic-override-precedence.md) — what wins when several overrides
  target the same element
- [`mep-color-standard.md`](mep-color-standard.md) — the project colour standard and its filter set
- [`../../skills/ajtools-mep-grayout/SKILL.md`](../../skills/ajtools-mep-grayout/SKILL.md) — the full
  drawing-standard grayout, which handles insulation as a quiet dashed wrapper rather than following the
  host colour; that is a different job with different rules
