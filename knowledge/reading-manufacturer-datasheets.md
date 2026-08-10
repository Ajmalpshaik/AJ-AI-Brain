# Reading a manufacturer submittal PDF (to build a family from it)

How to get real numbers out of a supplier's PDF on this machine, and — more important — **which page of
it to believe for what**. Written 2026-08-10 after building a Condair EL humidifier family first from a
single screenshot and then correcting it against the full 107-page submittal.

## The one rule: the data sheet gives SIZES, the shop drawing gives POSITIONS

A manufacturer "Data Sheet" page lists connection sizes (`Steam Outlet OD: 45 mm`, `Drain Water OD:
30.00 mm`, `Supply Water: G 3/4"`). **It does not say where any connection is, which face it is on, or
how many there are.** The dimensioned top/bottom/side views live on a separate **Shop Drawing** page,
usually titled *"Unit dimensions unit «X»"*.

Building from the data sheet alone produced a family that was wrong in three ways at once, none of them
visible from the data sheet:

| Guessed from the data sheet | Actually, per the shop drawing |
|---|---|
| condensate return on the **bottom** | on the **top**, and there are **two** ø8 ports |
| one power connector, on the bottom | **two** supplies (heating + control), both **top right** |
| all four small connections at invented x/y | every one dimensioned from the left edge and the back |

**So: if you only have the data sheet, the positions are unknown — say so and ask for the shop drawing.
Do not invent coordinates and let them look authoritative in a model.** Sizes from the data sheet,
positions from the shop drawing, and cross-check both against the project's own schedule (below).

## The project schedule carries things the manufacturer sheet omits

The Condair data sheet gives the heating supply only (`400/3/50-60 V/Ph/Hz`). The project's own
*Schedule Comparison* page listed a second supply — `230 V / 1φ / 50 Hz` — the **control** voltage, which
appears nowhere on the manufacturer page but does appear on the installation isometric as a separate
"Electrical isolator control voltage supply". Plant commonly has two feeds; read the project schedule
and the installation drawing, not just the product page.

## One family per HOUSING size, types for the capacity variants

Manufacturers group a range into a few housings. Condair EL: housing **S** = EL 5/8/10/15
(420 × 670 × 370), housing **M** = EL 20/24/30/35/40/45 (530 × 780 × 406). Everything inside one housing
shares the cabinet **and the connection positions** — only capacity, current, cylinder type and steam
outlet diameter change.

**So the natural structure is one Revit family per housing, with a type per capacity** — not one family
per model, and not one family stretched across housings (the connection positions differ, so a
parametric W/D/H would move the box but leave every connection wrong). Find the housing table before
deciding how many families to build; it is usually on the page after the shop drawing.

## Getting text and images out, on this machine

No poppler and no Python PDF library is installed, and `pdftoppm` is missing, so the Read tool cannot
render PDF pages directly. Two things that DO work here:

- **Text**: `pdftotext` ships with Git for Windows —
  `C:\Users\<user>\AppData\Local\Programs\Git\mingw64\bin\pdftotext.exe`, already on PATH in the Bash
  tool. Use `-layout` to keep table columns, and `-f`/`-l` to limit the page range.
- **Page images**: `uv run --with pymupdf python <script>` pulls PyMuPDF into a throwaway environment —
  nothing installed system-wide. `page.get_pixmap(dpi=170)` for a whole page, and
  `get_pixmap(dpi=600, clip=fitz.Rect(...))` to zoom a detail until dimension text is legible. Write the
  script to a file; escaping a multi-line `python -c` through Bash on Windows is not worth the fight.

**Shop drawings are usually raster, not vector** — `page.get_drawings()` returned 0 and
`page.get_images()` returned 25 embedded PNGs. So there is no way to measure coordinates programmatically;
you have to read the printed dimension text. Crop and zoom rather than squinting at a full page.

## `pdftotext -layout` shifts a two-column spec table by one row

On the Condair data sheet the **left** column came out with every value one row ABOVE its own label:

```
Rated Power:                  15 kW                 <- this 15 kW is Maximum Power's value
Maximum Power:                400/3/50-60 V/Ph/Hz   <- this is Power Circuit's value
Power Circuit:                21.7 A                <- this is Rated Current's value
```

Read it as *"the value on each line belongs to the label on the NEXT line"*. The **right** column of the
same table was not shifted at all. **Always sanity-check a few pairs against a rendered image of the
page before trusting an extracted spec table** — the offset is silent, internally consistent, and every
value is individually plausible, which is exactly what makes it dangerous. Here it would have recorded
the humidifier's maximum power as a voltage string and its cylinder count as a part number.

## Related

- [`live-model/families.md`](live-model/families.md) § Fourth build — the Revit-API side of turning this
  into a family (pipe/electrical connectors, clearance zones, negative extrusions).
- [`../scripts/recipes/create-equipment-family-from-datasheet.cs`](../scripts/recipes/create-equipment-family-from-datasheet.cs)
  — the recipe, whose INPUTS block now carries the real dimensioned Condair values as a worked example.
