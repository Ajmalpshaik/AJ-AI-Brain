# Knowledge — index (start here, then open ONE file)

Same rule as every index in this Brain: **read this file, pick the row that matches the request, open
that one file — don't read the whole folder.**

## Route by what the request is about

| If the request is about… | Open |
|---|---|
| Any live-model / AJ AI Bridge task at all — running scripts, view visibility, MEP tracing, undo, HVAC terminals/ducts, tagging, revisions, families | [`live-model/README.md`](live-model/README.md) **← its own index, route from there** |
| "What actions are available" / a plain-language menu of universal Revit actions | [`universal-actions-reference.md`](universal-actions-reference.md) |
| Fire sprinkler spacing rules — head count, spacing, distance to walls, area per head, hazard class | [`nfpa13-sprinkler-spacing.md`](nfpa13-sprinkler-spacing.md) (the workflow is [`skills/ajtools-fire-sprinkler-layout/SKILL.md`](../skills/ajtools-fire-sprinkler-layout/SKILL.md)) |
| **Anything else about sprinklers** — pendent vs upright vs sidewall, how far below the ceiling, how far below the slab when there is no ceiling, what a beam or a column does to the layout, the method end to end | [`fire-sprinkler/README.md`](fire-sprinkler/README.md) **← its own index, route from there** |
| Building a family from a supplier's PDF submittal — which page to trust for sizes vs positions, how many families the product range needs, and how to get text/images out of the PDF on this machine | [`reading-manufacturer-datasheets.md`](reading-manufacturer-datasheets.md) |
| An ambiguous or misheard term in a request | [`glossary.md`](glossary.md) |
| A search missed because the site word isn't the Revit word ("floor levels", "light fitting", "out to excel") — **add a row, it works immediately, no rebuild** | [`site-vocabulary.md`](site-vocabulary.md) (data, read live by `semantic-index\ask-brain-hybrid.cmd`) |
| A request phrased in Dynamo node names (`Element.GetParameterValueByName`, `List.FilterByBoolMask`, ...) | [`dynamo-vocabulary-map.md`](dynamo-vocabulary-map.md) |
| **Will these scripts run on Revit 2024/2025/2026+** — what breaks when the Revit version moves, which .NET goes with which release, how many of the 282 fragments are affected | [`revit-version-compatibility.md`](revit-version-compatibility.md) |
| **Which Revit API class/parameter/category does this Brain already use, and which fragment shows it working** — reach for this before looking anything up online: 283 proven examples beat a signature | [`revit-api-surface.md`](revit-api-surface.md) (generated — `node tools/api-surface.mjs`) |
| How to format a reply (counts, tables, the Final Report) | [`reply-style.md`](reply-style.md) |
| A change to the Brain itself (new skill, split file, retired script) — recording it, not making it | [`brain-log.md`](brain-log.md) |
| **Should we rebuild the search / "do RAG properly"** — what the retrieval layer already has, the six standard upgrades that measured worse here, and the two conditions that would justify a rewrite | [`rag-architecture-decisions.md`](rag-architecture-decisions.md) |
| How this Brain's bridge compares to the bought alternative (NonicaTab A.I. Connector) — what it costs, what they have that we don't, which to reach for | [`tool-landscape-nonicatab.md`](tool-landscape-nonicatab.md) |

## Adding new knowledge

Put it in the **one** file it belongs to. Never duplicate a fact across two files. If a file grows past
~300 lines, split it and update the relevant index — see
[`skills/brain-self-maintain/SKILL.md`](../skills/brain-self-maintain/SKILL.md) for how.
