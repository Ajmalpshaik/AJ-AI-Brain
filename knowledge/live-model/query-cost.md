# What a model query actually costs — and the four choices that change it

Written 2026-08-23. Every fragment in this library starts with a `FilteredElementCollector`, and until
now nothing here said which way of writing one is cheap and which is not. On Ajmal's real models the
wait he notices is almost always a collector, so this is the note that decides how the next fragment
gets written.

**Read the honesty line first.** The ratios below come from a controlled benchmark of the collector API
run on a small seeded document — a fresh project with levels, walls, grids and views, not a 500 MB
services model. **The DIRECTION of each comparison is what transfers; the absolute microseconds do not.**
Where a ratio is quoted it is from that measurement, and it is labelled as such. Nothing here has been
measured on one of Ajmal's models yet. When one of these choices ever matters enough to argue about,
measure it on the real file rather than quoting this page.

## 1. Scope the collector to a view when the answer is "in this view"

`new FilteredElementCollector(doc, viewId)` was **about six times faster** than the same query over the
whole document. That is the biggest single difference in the whole set, and it is not a micro-optimisation
— Revit keeps a per-view element set from the last regeneration and the constructor reads it, instead of
walking the model and testing each element.

It changes what the query MEANS as well as what it costs, so it is not a free swap:

- It returns what was in the view **at its last regeneration**. A view that has never been opened in this
  session can answer with a stale or empty set. Where that matters, regenerate or use the model-wide
  collector and say so.
- It respects crop, view range, phase, discipline, category visibility and view filters — which is exactly
  why `action-report-views-showing-element.cs` uses it. That is a feature when the question is about a
  drawing and a bug when the question is about the model.

## 2. Never count when the question is "is there one"

An existence check cost about **1 µs**. A count of the same set cost about **80 µs** — eighty times more,
and it grows with the model while the existence check does not.

- `FirstElement()` / `FirstElementId()` stop at the first hit.
- `Count()` and `GetElementCount()` walk everything.

So `collector.FirstElementId() != ElementId.InvalidElementId` is the cheap "does any exist". Writing
`collector.ToElements().Count > 0` is the same question asked in the most expensive possible way — it
materialises the whole set, then throws it away.

When a real count IS the answer, the three ways are close: LINQ `Count()` ≈ `GetElementCount()` ≈ 0.9–1.1×
each other, and **`ToElementIds().Count` is the worst of the three** — it allocates the whole id list
(16 KB in the benchmark) purely to read its length.

## 3. Ask for ids when ids are all you need

Measured on the same set: `ToElementIds()` ≈ **1.0×**, `ToElements()` ≈ **1.6×**, `.Cast<T>().ToList()`
≈ **1.9×**. Ids are cheap because Revit never has to marshal an `Element` wrapper across the API boundary.

This is why the filter fragments here that hand ids to the next stage — rather than element objects — are
doing the right thing, and why `FilteredElementCollector(Document, idCollection)` is a good way to narrow
a second pass.

## 4. `UnionWith` is the expensive way to say "or"

Already known here for a correctness reason — **it silently drops quick filters** — and now the cost is
known too: `OfClass().UnionWith(OfClass())` was about **2.6× slower** than `OfClasses(a, b)` doing the
same job, and about 3.3× a plain single-category collect.

Use the multi-argument forms instead:

- several classes → `ElementMulticlassFilter`
- several categories → `ElementMulticategoryFilter`
- genuinely different queries → `LogicalOrFilter`

`UnionWith` is for the case none of those cover, and it should be a deliberate choice.

## 5. Parameter filters vs LINQ — the honest version

The common advice is that `ElementParameterFilter` is much faster than filtering with LINQ because it runs
"inside Revit". On the small benchmark model **they came out the same** — within noise on every comparison.

That does not make the advice wrong, it makes the reason wrong. The parameter filter's win is not CPU, it
is that **the elements never have to materialise**: LINQ over a collector pulls every candidate across the
API boundary as a managed object before your predicate sees it. On a small set that costs nothing
measurable. On 200,000 elements it is the difference between touching 200,000 wrappers and touching the
few hundred that pass.

So: **use a parameter filter when the set is large and the predicate is simple** (equals, greater than,
contains). Use LINQ without guilt when the set is already narrow, or when the predicate needs something no
filter rule expresses. And always put the quick filters — class, category — before the slow one, so the
slow filter sees the smallest possible candidate set.

One correctness point that is not about speed: a `double` comparison rule takes an **epsilon**, and the
overload without one is a trap on any dimension that came from geometry.

## 6. Two more, from the same measurements

- **Adding filters is not free.** `OfCategory()` alone beat `OfClass().OfCategory()` by about 10%. Where
  the category already implies the class, the extra filter is paying for nothing.
- **A level filter is expensive.** Filtering by level cost about **1.7×** a plain category collect, because
  it is a parameter comparison rather than a quick filter. On a per-level sweep, collect the category once
  and group in memory rather than running one level-filtered query per level.

## What this changes in practice

When a fragment here is slow, the order to check is:

1. Is it collecting the whole model where a view would do?
2. Is it counting something it only needs to know exists?
3. Is it materialising elements when ids would answer?
4. Is it running one query per level / per view / per category where one query and a grouping would do?

Those four cover nearly every slow fragment in this library. See
[`../revit-api-surface.md`](../revit-api-surface.md) for which fragment already demonstrates each filter
class.
