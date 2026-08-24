# Moving, copying and rotating elements — and the transforms that silently do nothing

Back to [`README.md`](README.md) · units and bridge rules in [`core.md`](core.md)

The theme of this file: **a Revit transform API can return without throwing and change nothing at all.**
That is the confidently-wrong failure this Brain exists to catch — worse than a crash, because the script
reports success and the reply says "moved 5 terminals" when nothing moved.

## A move on a GROUP MEMBER is silently ignored — no exception, no return value, no change

Proved live 2026-08-07 on `Project1_ajmal.al.rvt`:

```csharp
ElementTransformUtils.MoveElement(Document, terminalId, target - current);
// returned normally. Document.Regenerate(). Location re-read: IDENTICAL. Distance moved: 0 mm.
```

The element was an air terminal inside model group `MEP_TestGroup_Terminals`. What the model actually
said about it:

| Property | Value | What it means |
|---|---|---|
| `e.Pinned` | `True` | reported on the **member**, even though nobody pinned it |
| `e.GroupId` | `919598` | it belongs to a group |
| the group's own `.Pinned` | `False` | **the group is NOT pinned — only its members report as pinned** |

So the usual instinct — "check the group, the group isn't pinned, therefore it's movable" — gives the
wrong answer. The members are what block the move, and they inherit an effective pinned state from
group membership.

**You cannot unpin your way out of it either.** `element.Pinned = false` on a group member throws:

```
Element cannot be pinned or unpinned.
```

### What to do instead

1. **Move the Group element itself** (`Document.GetElement(e.GroupId)`) — that moves all members
   together, which is almost always what the user meant anyway.
2. **Or ungroup first** (`Group.UngroupMembers()`), move, and regroup — only if the user has agreed to
   losing the group, which is a real model change and needs asking.

### The check to run BEFORE any move/rotate/mirror/array

`Pinned` and `GroupId` are two cheap reads, and they separate "will work" from "will silently no-op":

```csharp
foreach (var e in elements)
    if (e.Pinned || e.GroupId != ElementId.InvalidElementId)
        sb.AppendLine($"  SKIPPED {e.Id.IntegerValue} — pinned or in group {e.GroupId.IntegerValue}; a move here does nothing.");
```

A genuinely pinned standalone element behaves the same way: silently unmoved. `Pinned` alone is the
common case (grids and levels are pinned by default in most templates); the group case is the one that
surprises people, because the group reads as unpinned.

### And the rule that catches it regardless — read the position back

This is [`README.md`](README.md) rule 2 ("read back after changing anything") in its most concrete form.
Do not report a move as done because the API didn't throw. Re-read the location and compare:

```csharp
var before = ((LocationPoint)e.Location).Point;
ElementTransformUtils.MoveElement(Document, e.Id, delta);
Document.Regenerate();
var after = ((LocationPoint)e.Location).Point;   // <- this is the evidence, not the absence of an exception
```

Every move/copy/rotate fragment in `scripts/actions/move-copy-rotate/` is verified against this standard:
a distance actually measured after the transform, not a success flag.

## Which transforms the group/pin block actually applies to — it is NOT all of them

Measured on the same fixture, 2026-08-07. The asymmetry is not obvious and is worth knowing before you
plan a fix around it:

| Transform on a pinned group member | Result |
|---|---|
| `ElementTransformUtils.MoveElement` | **silently does nothing** — no exception |
| `ElementTransformUtils.RotateElement` | **silently does nothing** — no exception |
| `ElementTransformUtils.CopyElement` | **works normally** — returned 5 new ids from 5 grouped air terminals |

So "it's in a group" is not a blanket answer. Copy is fine; move and rotate are the two that lie. That
is why `action-copy-elements.cs` needed no fix (its count comes from the ids Revit actually returned)
while `action-move-elements.cs` and `action-rotate-elements.cs` both did.

## Mirroring CONNECTED MEP in place does not mirror it

Different mechanism, same shape of wrong answer. Mirroring a duct/pipe run **in place** while leaving its
fittings out of the set does not reflect it — Revit preserves the connections and re-fits the geometry,
so you get a constrained shape rather than a mirror image.

Proved live 2026-08-07: three ducts joined through fittings 919041/919049 sat at Y midpoints
8391 / 12014 / 15111 mm. Mirroring those three ducts alone about the X axis gave
**-8462 / 2800 / -15225** instead of the true -8391 / -12014 / -15111 — and 12014 -> 2800 is not
even on the correct side of the axis. The control run, 8 unconnected walls mirrored the same way,
negated every Y to the millimetre.

Two ways to get a real mirror:

- **Include the whole connected set**, fittings and all, in `elements`.
- **Or mirror as a copy** (`mirrorCopy = true`), which builds a fresh set with its own connections and
  reflected exactly in the same test (new ducts landed on -8391 / -12014 / -15111).

## Why this was found

Verifying `action-find-duplicates.cs` on 2026-08-07. The check correctly reported "0 duplicate clusters",
and the plan to prove that zero was to stack two air terminals and watch the count go to 1. It stayed at
0 — which for a few minutes looked like a bug in the duplicate checker. The checker was right; the test's
own move had been silently ignored. Re-run against two ungrouped AHUs, the count went 0 -> 1 -> 0 exactly
as designed (gap 7857 -> 0.0 -> 7857 mm across the rollback).

Worth keeping as a cautionary tale: **when a check reports zero and your attempt to disprove it also
reports zero, suspect the attempt before the check.**

## Solid geometry — five traps that all fail quietly (2026-08-24)

Found while building `actions/qa-checks/action-audit-mep-openings.cs` and rewriting the linked-model
path of `action-report-clashes.cs`. None of these raises a clear error; each one produces a plausible
wrong answer, which is why they are worth writing down.

### 1. `Solid.GetBoundingBox()` is NOT in model coordinates — `Element.BoundingBox` is

Autodesk's own wording: *"The bounding box information is stored as bounds in local coordinates and a
transform. So the transform is to be taken in to account when using the bounds. **This is different from
the bounding box returned by Element.BoundingBox** in that the bounding box returned by that routine
stores the bounds in modeling coordinates with an identity transform."*

So `new Outline(solidBox.Min, solidBox.Max)` builds a box **in the wrong place**, and every quick filter
built on it silently finds the wrong candidates — usually none, which reads as "nothing there". Transform
all eight corners and take the componentwise min/max:

```csharp
var bb = solid.GetBoundingBox();
var t = bb.Transform ?? Transform.Identity;
// eight corners of (bb.Min, bb.Max) through t, then min/max each axis -> new Outline(lo, hi)
```

`Element.get_BoundingBox(null)` needs none of this. The two are not interchangeable.

### 2. Test a solid for emptiness BEFORE you transform it, never after

A solid with no faces or no edges cannot take part in a boolean — `ExecuteBooleanOperation` throws.
`SolidUtils.CreateTransformed` **gives an empty solid faces and edges**, so a transformed copy looks
healthy, passes the test, and throws anyway. Check `!solid.Faces.IsEmpty && !solid.Edges.IsEmpty` on the
original, once, before any transform.

### 3. Quick filter first, slow filter second — and the order is not cosmetic

`ElementIntersectsSolidFilter` and `ElementIntersectsElementFilter` are SLOW filters: Revit expands each
candidate's geometry to answer them. `BoundingBoxIntersectsFilter` is a QUICK filter — it reads only the
element record. Chained quick-then-slow, geometry is only ever built for candidates whose boxes already
overlap:

```csharp
new FilteredElementCollector(doc, ids)
    .WherePasses(new BoundingBoxIntersectsFilter(outline))   // quick
    .WherePasses(new ElementIntersectsSolidFilter(solid))    // slow
```

Putting the slow one first builds the geometry of every candidate in the set. See
[`query-cost.md`](query-cost.md).

### 4. A boolean that throws is not "no intersection"

Revit's kernel raises `Autodesk.Revit.Exceptions.InvalidOperationException` on geometry it dislikes —
coincident faces, self-intersecting solids, imported junk. Catching it and continuing turns an
unanswered question into a clean result, which is exactly the defect `action-report-clashes.cs` was
fixed for on 2026-08-23. Count them and say so. For a **coordination** report the safe default is to
treat a failed boolean as a possible hit and flag it, never as a miss.

### 5. Union solids one pair at a time, and bank the accumulator when a pair fails

An element can have several solids (a duct and its insulation, a wall's layers). Uniting them in a fold
and letting one bad pairing throw loses the whole element. Bank what has accumulated, start a new
accumulator with the solid that failed, and return the LIST — one bad join then costs that join only.

### `ElementIntersectsElementFilter` cannot cross documents

It takes an `Element` and tests candidates in **the same document**. To clash against a linked model,
take the linked element's solids, move them with `SolidUtils.CreateTransformed(solid,
linkInstance.GetTotalTransform())`, and use `ElementIntersectsSolidFilter` instead. Going the other way —
moving your own solids into the link with `.Inverse` — is right when the collector must run inside the
link document (which is what `action-audit-mep-openings.cs` does, because the structure lives there).

### Tolerances: ask Revit rather than inventing a number

`Application.ShortCurveTolerance`, `Application.AngleTolerance` and `Application.VertexTolerance` are
Revit's own thresholds. A length comparison against `ShortCurveTolerance` and a parallel test against
`AngleTolerance` agree with what Revit itself will accept; a hardcoded 1e-6 does not, and is the reason a
check can pass here and fail inside a Revit call two lines later. None of the three was used anywhere in
this library before 2026-08-24.
