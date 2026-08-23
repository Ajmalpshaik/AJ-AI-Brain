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

## Which Revit types can be used as a Dictionary key — and the two that look safe and are not

**Checked by reflection against the shipped `RevitAPI.dll` on 2026-08-24, not assumed.** The question is
which types override `GetHashCode`, because that decides whether a `HashSet<T>` or `Dictionary<T,...>`
groups by VALUE or by object identity — and getting it wrong fails silently, never loudly.

| Type | `GetHashCode` declared by | What that means |
|---|---|---|
| `ElementId` | **`ElementId`** | Overridden — hashes by the id VALUE. **Safe as a key**, and it is what this library uses everywhere |
| `GeometryObject` (and `Solid`, `Face`) | **`GeometryObject`** | Overridden — **but not by geometry value.** See below |
| `Element` | `Object` | NOT overridden — reference identity only |
| `XYZ` | `Object` | NOT overridden — reference identity only |

### `XYZ` and `Element` are reference-keyed, so a HashSet of them does not do what it looks like

Two `XYZ` objects at the same coordinates are **different keys**, because nothing overrides equality.
So a `HashSet<XYZ>` built to find coincident points finds nothing, and reports zero duplicates on a model
full of them. The same applies to `Element`: Revit hands back a fresh managed wrapper on each call, so
`HashSet<Element>` can hold the *same* element several times over.

**The fix is always the same and this library already follows it — key on `ElementId`**, or for points on
a rounded tuple of the coordinates. Checked on 2026-08-24: **no fragment keys a collection on `XYZ` or
`Element`**, so this is a trap to keep avoiding rather than one to go and fix. `action-find-overlapping-lines.cs`
is the worked example of doing it properly — it keys lines by a rounded direction-plus-offset string
precisely because the geometry objects themselves cannot be compared that way.

### `GeometryObject.GetHashCode()` is the dangerous one, because it IS overridden

It is overridden, which makes it look like a value hash — and it is not one. It reflects the address of
the underlying native object, so it is **not stable between Revit sessions, and not necessarily even
stable for the same geometry within one session.**

**Never use it to answer "has this geometry changed" or "are these two shapes the same".** It will agree
when it should not and differ when nothing moved, and either way it will do so quietly. For "has this
changed", Revit has real answers: `Element.VersionGuid` (2021+, a per-element stamp) and
`Document.GetChangedElements(episodeGuid)` (2023+) — both used by
[`../../scripts/actions/qa-checks/action-compare-models.cs`](../../scripts/actions/qa-checks/action-compare-models.cs).
For "are these the same shape", the honest routes are comparing the geometry structure yourself, or
tessellating and comparing meshes — which is slow, and is the reason the pointer-hash shortcut is
tempting in the first place.
