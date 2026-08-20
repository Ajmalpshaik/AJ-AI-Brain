# The Revit API index — deliberately separate

Ajmal asked (2026-08-20) for the whole Revit API to be kept in the Brain, **in a separate section**.
The second half of that sentence is what makes the first half safe, so it is enforced here rather than
left as a convention.

## What this is

A second, independent vector index holding the **entire Revit API of the Revit you actually run** —
every public type, property, method signature and enum value. Ask it about a signature. Ask the Brain
about a job.

```
api-index\ask-api.cmd "how do I collect every element of a category"
semantic-index\ask-brain-hybrid.cmd "how many diffusers do I need in this room"
```

## Why it is not in the Brain's index

Measured, not assumed:

| | |
|---|---|
| Revit API | ~1,700 classes, 500 enumerations, **30,000+ documented members** |
| The Brain's whole index | **3,786 chunks** |

Indexed together, the Brain becomes roughly **11% of its own index**, and every modelling question lands
on a reference page instead of on the skill or fragment that answers it. This repo already made that
mistake at one eighth the scale — 604 chunks of external standards, indexed 2026-08-13 and reverted the
same hour. The full argument is in [`knowledge/revit-api-surface.md`](../knowledge/revit-api-surface.md).

**Three independent reasons the Brain cannot see this index** — checked on 2026-08-20, not assumed:

1. different database directory — `api-index/chroma-db-api`, not `semantic-index/chroma-db`
2. different collection name — `revit_api`, not `aj_brain`
3. `api-index/` is outside `brain_common.INDEX_TARGETS` (which is `scripts/`, `knowledge/`, `skills/`
   plus a fixed root-doc list), so the Brain's indexer never reads these files at all

Verified live: each client lists only its own collection, and the Brain's indexed set contains **0**
files from `api-index/`.

## Where the content comes from — and why not revitapidocs.com

The corpus is produced by **reflecting over the `RevitAPI.dll` your Revit has actually loaded**, through
the bridge, using [`scripts/context/harvest-revit-api.cs`](../scripts/context/harvest-revit-api.cs).

That beats scraping a documentation site on every count that matters here:

- **It is your version.** The API this reports is the API you have — not a website's copy of some
  version. After the 2026-08-20 migration made the fragment library run on 2020 through 2027, knowing
  what exists in *this* Revit is the whole point.
- **No scraping, no third-party dependency, no licence question.** `revitapidocs.com` is a community
  site, not a primary source, and bulk-copying it into a Brain that must stay portable is the wrong
  shape of dependency. (It is also unreachable from the Claude Code environment's egress proxy.)
- **It cannot go stale.** Re-run the harvest after a Revit upgrade and the index is correct again.

If Autodesk shipped `RevitAPI.xml` beside the DLL, the descriptions come through too. If not, you still
get every signature — which is the part you cannot guess.

## Use it

```
1.  run scripts/context/harvest-revit-api.cs through the bridge     (writes api-index/corpus/)
2.  api-index\index-api.cmd                                          (builds the separate collection)
3.  api-index\ask-api.cmd "your question"
```

`index-api.cmd --stats` reports what the corpus holds without indexing anything.

Neither the corpus nor the database is committed — both are large, version-specific, rebuildable, and
Autodesk's API surface rather than this Brain's own work. Only the `.py`, `.cmd` and `.md` travel.

## Reach for the Brain first

This index gives you a **signature**. It will not tell you that
`FilteredElementCollector.UnionWith()` silently drops quick filters, or that `RBS_START_LEVEL_PARAM` is
the only level parameter an MEP curve has. The Brain knows both, because it learned them the hard way,
and it holds 283 proven working fragments.

[`knowledge/revit-api-surface.md`](../knowledge/revit-api-surface.md) is the bridge between the two: the
229 types this library actually uses, each naming a fragment that uses it correctly.
