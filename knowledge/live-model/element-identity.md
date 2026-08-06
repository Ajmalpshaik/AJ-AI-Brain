# Reading an element's identity — level, workset, design option

> Split out of [`core.md`](core.md) on 2026-08-06, which had grown past the ~300-line rule in
> [`../INDEX.md`](../INDEX.md). Everything here answers one shape of question: **"which X is this
> element on?"** — and each answer turned out to be a trap that fails *silently* rather than loudly.

All four were found the same way on the same day: a fragment returned `None`, or `0`, or `null`, and
reported success. None would have been caught by compiling. **When a Revit read comes back empty, check
the parameter's `StorageType` and whether the parameter exists at all before believing the model.**

### Elements cannot be MOVED into a Design Option through the API either — every route is read-only

Checked by reflection 2026-08-06, not assumed:

| Route | Writable? |
|---|---|
| `Element.DesignOption` property | **No** (`CanWrite = false`) |
| `BuiltInParameter.DESIGN_OPTION_ID` | **read-only** |
| `BuiltInParameter.DESIGN_OPTION_PARAM` | **read-only** |
| `View.DesignOption` property | **No** |
| The active view's `"Design Option"` parameter | **read-only** |
| Any `Document` method to set the active option | **none exist** |

**But there is a working route that needs one UI action.** An element lands in whichever Design Option is
*active at the moment it is created*, and the active option is set from Revit's status bar (bottom-right).
So: ask the user to pick the option there, confirm with `DesignOption.GetActiveDesignOptionId(Document)`
(returns `-1` for Main Model), then create elements normally — they arrive in that option. Moving
*existing* elements is UI-only: Manage → Design Options → Add to Set.

### Design Options cannot be created through the API in Revit 2020 — checked, not assumed

`DesignOption` exposes exactly **one** static method in this build (2020, `20220517_1515`):

```
ElementId GetActiveDesignOptionId(Document document)
```

No `CreateDesignOptionSet`, no `CreateDesignOption`, and nothing named `*Option*` on
`Autodesk.Revit.Creation.Document` either. **Design Options must be made by hand** (Manage → Design
Options), after which the API can read and filter them normally. Verified 2026-08-06 by reflecting over
the real type rather than trusting documentation, after `DesignOption.CreateDesignOptionSet` failed to
compile.

Worth reusing as a technique: when an API call will not compile, **reflect over the type and print its
actual members** before concluding anything. It distinguishes "this build lacks the method" from "I got
the name wrong", and the answer is definitive rather than a guess.

### "Which workset is this on?" — the parameter is an INTEGER, and workset Id 0 is real

`ELEM_PARTITION_PARAM` holds the workset, and reading it the obvious way returns nothing. Proved live
2026-08-06 on a workshared model:

| Read | Returns |
|---|---|
| `.StorageType` | `Integer` |
| `.AsString()` | **`null`** — easy to misread as "this element has no workset" |
| `.AsInteger()` | `0` — the workset **Id** |
| `.AsValueString()` | `"Workset1"` — the **name**, and the one you usually want |
| `element.WorksetId.IntegerValue` | `0` |

**Two traps, not one.** `AsString()` returning null looks exactly like an element with no workset — this
caught a probe written earlier in the same session. And **`Workset1` genuinely has Id `0`**, so any code
treating `0` as "unset" or "invalid" silently drops every element on the default workset. Compare against
a real `Workset.Id`, never against `0`.

Only `WorksetKind.UserWorkset` worksets are the ones a modeller means. On a live check, 136 of 3,221
non-type elements sat on the 2 user worksets; the rest are on family and view worksets, which is normal
and not a sign anything is missing.

### "Which level is this element on?" — MEP curves need a parameter the usual chain does not try

There is no single universal *get this element's level* API, so the library uses a fallback chain. **That
chain must end with `RBS_START_LEVEL_PARAM` or every MEP curve silently reports no level at all.**

Proved live 2026-08-06 by probing a real Duct — the four parameters normally tried are not merely empty,
they are **not present on the element**:

**The parameter is not even called the same thing.** Grouping or reporting by a *display name* fails
separately from the `LevelId` problem — confirmed live 2026-08-06:

| Element | Parameter named "Level"? | Its real name | `element.LevelId` |
|---|---|---|---|
| **Duct / Pipe** | no | **"Reference Level"** | `-1` |
| **Wall** | **no level parameter at all** | — | works |
| Air Terminal | yes | "Level" | works |

So `LookupParameter("Level")` answers `None` for most of a real model, and only looks correct on air
terminals. Anything grouping, scheduling or reporting "by level" must use the fallback chain below rather
than a name lookup — this is what broke `action-count-by-group.cs`.

| Tried on a Duct | Result |
|---|---|
| `element.LevelId` | `-1` (InvalidElementId) |
| `FAMILY_LEVEL_PARAM` | parameter not present |
| `SCHEDULE_LEVEL_PARAM` | parameter not present |
| `LEVEL_PARAM` | parameter not present |
| `INSTANCE_REFERENCE_LEVEL_PARAM` | parameter not present |
| **`RBS_START_LEVEL_PARAM`** | **`311` → Level 1** ✓ |

Applies to every MEP curve — Ducts, Pipes, Flex Ducts/Pipes, Cable Trays, Conduits. A `FamilyInstance`
such as an air terminal is fine on plain `element.LevelId`; it is the *curves* that differ.

**Why this one is nastier than a crash.** A missing level resolves to `InvalidElementId`, which simply
never equals the level you are filtering for — so "ducts on Level 1" returns **zero** and reports success.
Nothing looks wrong. Measured side by side on the same model: fixed chain 3 ducts, old chain 0 ducts,
walls unaffected at 8. Fixed in `lib/prelude.cs`, `filters/by-identity/filter-by-category.cs`,
`filters/by-location/filter-by-elements-on-level.cs` and `actions/reporting/action-report-parameters.cs`
— check any new level-resolution code against this list rather than re-deriving the chain.
