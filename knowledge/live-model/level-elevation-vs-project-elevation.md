# A level has TWO heights, and only one of them shares a coordinate system with the geometry

Back to [`README.md`](README.md) · sibling: [`datums.md`](datums.md) (extents and bubbles),
[`geometry-and-transforms.md`](geometry-and-transforms.md) (link transforms).

**Written 2026-08-24 after an audit found this mixed the wrong way in 20 fragments of this library.**
It is a silent, conditional error: on most demo models the two numbers are identical and everything
works, and on a real site model with a survey offset every affected result is wrong by exactly that
offset — with no exception, no warning, and a plausible-looking number in the report.

## The two properties

| Property | What it is |
|---|---|
| `Level.Elevation` | The number shown in the Properties palette and on the level head. Measured from **whatever the level type's "Elevation Base" parameter says** — `Project` or `Shared`. Writable. |
| `Level.ProjectElevation` | Always measured from the **project origin**, whatever Elevation Base says. Read-only. |

Autodesk's own wording, from the shipped API documentation:

> `Elevation` — *"If the Elevation Base parameter is set to Project, the elevation is relative to project
> origin. If the Elevation Base parameter is set to Shared, the elevation is relative to shared origin
> which can be changed by relocate operation."*
>
> `ProjectElevation` — *"Retrieves the elevation relative to project origin, no matter what values of
> the Elevation Base parameter is set."*

Both exist on Revit 2020 through 2027 — this is not a version question.

## Why it matters: XYZ is always project-internal

**Every coordinate the API hands you is in project-internal coordinates.** `LocationPoint.Point`,
`LocationCurve.Curve`, `get_BoundingBox(...)`, `Solid` vertices, `ReferenceIntersector` origins,
`Room.IsPointInRoom(point)` — all of them. There is no "shared coordinates" mode for geometry; shared
coordinates are a *reporting* convention applied on top (`ProjectLocation`, `BasePoint.SharedPosition`).

So the moment a level's height meets an `XYZ`, the two must be in the same space:

```csharp
// WRONG whenever Elevation Base = Shared — off by the survey/shared offset, silently
double zProbe = room.Level.Elevation + mm(1000);
room.IsPointInRoom(new XYZ(p.X, p.Y, zProbe));

// RIGHT on every model
double zProbe = room.Level.ProjectElevation + mm(1000);
```

`Elevation` is not wrong in itself — it is wrong *in that sentence*. It is the correct thing to print
next to a level's name, because it is what the drawing shows.

## The rule, in one line

**Use `ProjectElevation` when the number will meet a coordinate. Use `Elevation` when the number will
be read by a human.** Ordering and comparison between levels is safe either way — both are monotonic
in the same direction, so `OrderBy(l => l.Elevation)` sorts correctly regardless.

## When the two differ

`Elevation` and `ProjectElevation` diverge only when a level type's **Elevation Base** is `Shared`
*and* the shared origin is not the project origin — i.e. the model has been given real site levels, or
relocated. That is normal on any project set out to a survey datum, and unusual on an in-house test
model. Which is exactly why this survives testing.

The Elevation Base setting lives on the **level type**, as `BuiltInParameter.LEVEL_RELATIVE_BASE_TYPE`
(present 2020–2027). Different level types in one model can disagree — one set of levels on Project and
another on Shared is a real, legal configuration and the nastiest version of this.

**Read that parameter with `AsValueString()`, not `AsInteger()`.** The API documentation names the
parameter and publishes **no enum for its values**, so "0 means Project" is an assumption. `AsValueString()`
returns exactly the words Revit shows in the Properties palette. They are localised, so never branch on
them — **decide from the measurement instead**: the difference between the two elevations, and the
vertical gap between the survey point's `Position` and its `SharedPosition`. Both are numbers, and both
mean the same thing in every language.

## How to tell in ten seconds

Run [`../../scripts/actions/reporting/action-report-level-elevations.cs`](../../scripts/actions/reporting/action-report-level-elevations.cs).
It prints both numbers, their difference, the Elevation Base of each level type, and the project base
point and survey point — and says in one line whether this model is affected. Run it **before** trusting
any height-based result on a model you have not checked.

## What was wrong here, and what was fixed

The audit found `Level.Elevation` mixed with an `XYZ` in the fire-sprinkler chain
(`sprinkler-obstruction-check`, `sprinkler-obstruction-survey`, `sprinkler-adjust-for-obstructions`,
`sprinkler-sidewall-layout`, `sprinkler-layout-options`, `sprinkler-place-heads`,
`sprinkler-deflector-height`, `sprinkler-compliance-audit`, `sprinkler-nfpa-grid`), in the coverage and
routing tools (`action-report-coverage`, `action-plan-shortest-route`,
`generate-room-coverage-layout`), in `action-report-ceiling-heights` and in the two dimensioning
fragments. All were switched to `ProjectElevation`.

A second pass the same day found five more, and how they were missed is the part worth keeping:

- `create-wall.cs`, `create-floor.cs`, `create-ceiling.cs` — each fed `double z = level.Elevation` straight
  into the `XYZ` of the new element's boundary or location line. These are three of the oldest creators in
  the library, live-verified since 2026-08-07, and every element they have ever made on a survey-datum model
  was placed at the wrong height with no error.
- `action-check-valve-accessibility.cs` and `action-auto-create-coordination-views.cs` — found by a peer
  session working in this repo at the same time, not by this audit.

`action-reassign-level.cs` and `action-change-wall-constraints.cs` were **left on `Elevation`
deliberately**: they compute a level-to-level *difference* to re-derive an offset parameter, and the
offset a wall stores is measured against the same base the level reports, so the two cancel. Changing
those would have introduced the error rather than removed it.

## How to sweep for it — and the grep that lies

The first sweep used `grep -rn "Level\.Elevation"`. That only matches where the word `Level` is literally
in the expression — `x.Level.Elevation`, `room.Level.Elevation`. It **cannot see the far more common
shape**, where the variable is already a `Level`:

```csharp
double z = level.Elevation;    // invisible to the grep above
lvl.Elevation                  // so is this
l.Elevation                    // and this
```

It reported clean and it was wrong. The sweep that actually works:

```bash
grep -rn "\.Elevation\b" scripts --include=*.cs \
  | grep -v "ProjectElevation" \
  | grep -v "ViewType\.Elevation" \
  | grep -vE "^[^:]+:[0-9]+: *//"
```

That returns roughly two dozen hits, and **most of them are correct code** — read every one:

| Shape | Verdict |
|---|---|
| `.OrderBy(l => l.Elevation)` | **Safe.** Sorting only. Both bases are monotonic in the same direction, so the order is identical either way |
| printed into `sb` / a report row | **Safe, and usually right** — this is the number the drawing shows |
| `ProjectPosition.Elevation`, `ViewFamily.Elevation`, `ViewType.Elevation` | **Not this property at all.** Different types that happen to share the word |
| `levelA.Elevation - levelB.Elevation` re-deriving an offset parameter | **Safe** — the base cancels. Do not "fix" these |
| the value reaching an `XYZ`, a `.Z`, or a subtraction against a world coordinate | **DEFECT** |

Only the last row is the bug. A sweep that fixes every hit is as wrong as one that fixes none.

## The related trap, same shape

`ViewPlan.GenLevel.Elevation` is the same property on the same class, and several fragments used it as
the drawing Z for a detail line or a dimension. Same fix, same reason.
