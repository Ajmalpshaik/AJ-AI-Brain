# Live Model — Color vocabulary (pastel, neon, and picking RGB from a plain-language word)

> Part of the live-model knowledge set. Index: [`README.md`](README.md) — go back there to route to another topic.

When the user names a color STYLE in plain words ("pastel colors", "neon colors", "muted", "bold") instead
of exact RGB, use this file to turn that word into real numbers — don't guess.

## The technique (HSV mental model)

Every color style word is really a statement about **saturation** (how vivid vs. washed-out) and
**brightness/value** (how light vs. dark), not about a specific hue. Any hue can be made pastel or neon.

| Style word | Saturation | Brightness (Value) | Feel |
|---|---|---|---|
| Pastel | low-medium (~0.25-0.4) | high (~0.9-1.0) | soft, light, "washed out" |
| Neon | maximum (~1.0) | maximum (~1.0) | electric, vivid, screen-bright |
| Muted / earth-tone | low (~0.2-0.35) | medium (~0.5-0.7) | dusty, natural, low-contrast |
| Bold / primary | high (~0.8-1.0) | medium-high (~0.7-0.9) | strong, saturated, but not neon-harsh |
| Deep / jewel-tone | high (~0.7-0.9) | low-medium (~0.3-0.5) | rich, dark-saturated |

Given a hue in degrees (0-360) and one of these saturation/value pairs, convert to RGB with standard
HSV→RGB math — the exact function already lives in
[`../../scripts/actions/color-graphics/action-color-by-group.cs`](../../scripts/actions/color-graphics/action-color-by-group.cs)
(`hsvToRgb`), reusable as a pattern for any one-off conversion needed elsewhere too.

## Two different situations — pick the right tool

- **One single color requested** ("make it pastel pink", "color these neon green") — pick a concrete RGB
  from the swatches below (or compute one from the table above) and set it directly as `colorR`/`colorG`/
  `colorB` in [`action-set-color-uniform.cs`](../../scripts/actions/color-graphics/action-set-color-uniform.cs)
  or [`action-set-category-color.cs`](../../scripts/actions/color-graphics/action-set-category-color.cs).
- **Several groups need to be told apart** ("color each system type", "color by level, pastel please") —
  use [`action-color-by-group.cs`](../../scripts/actions/color-graphics/action-color-by-group.cs)'s
  `colorMode = "pastel"` or `"neon"` directly — it hue-steps evenly around the color wheel at that
  saturation/brightness band, so every group is GUARANTEED visually distinct, not just individually
  pastel/neon. Don't hand-pick N swatches from the table below for a multi-group request — the fragment's
  hue-stepping already solves that better than picking colors by eye.

## Pastel swatches (ready to use for a single color)

| Name | R | G | B |
|---|---|---|---|
| Pastel pink | 255 | 209 | 220 |
| Pastel peach | 255 | 218 | 185 |
| Pastel yellow | 253 | 253 | 150 |
| Pastel mint | 189 | 252 | 201 |
| Pastel sky blue | 174 | 216 | 245 |
| Pastel lavender | 216 | 191 | 245 |
| Pastel lilac | 230 | 200 | 255 |

## Neon swatches (ready to use for a single color)

| Name | R | G | B |
|---|---|---|---|
| Neon red | 255 | 0 | 60 |
| Neon orange | 255 | 95 | 0 |
| Neon yellow | 255 | 255 | 0 |
| Neon green | 57 | 255 | 20 |
| Neon cyan | 0 | 255 | 255 |
| Neon blue | 0 | 120 | 255 |
| Neon pink | 255 | 20 | 147 |
| Neon purple | 191 | 0 | 255 |

## Note on Revit color range

Revit's `Autodesk.Revit.DB.Color` takes plain 0-255 `byte` RGB, same as any other RGB color — nothing
Revit-specific to convert. These swatches and the HSV table above work directly as `colorR`/`colorG`/
`colorB` inputs with no further conversion.
