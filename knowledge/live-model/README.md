# Live Model Notes — index (start here, then open ONE topic)

AJ AI Bridge/live-model knowledge, split by task shape. **Read this file, pick the row that matches the
request, open that one file — don't read the whole set.** Each topic file links back here.

`core.md` is the only file worth reading alongside another: it holds the bridge rules and the
feet↔mm conversion every script needs.

## Route by what the request is asking for

| If the request is about… | Open |
|---|---|
| Running anything through the bridge at all; units (mm↔feet); Revit version differences; reading a raw category ID | [`core.md`](core.md) **← read this for any live-model task** |
| "Which level / workset / design option is this element on?" — and why each one returns nothing instead of erroring | [`element-identity.md`](element-identity.md) (split out of `core.md` 2026-08-06) |
| **"Can I use this as a Dictionary key?"** — `ElementId` yes, `XYZ` and `Element` NO (reference equality, so a HashSet of them silently finds nothing), and `GeometryObject.GetHashCode()` is a native POINTER, not a geometry value — never use it for "has this changed" | [`element-identity.md`](element-identity.md) |
| Isolating/hiding elements in a view; creating a section view | [`views.md`](views.md) |
| "What actually connects to what" — tracing pipe/duct/equipment when names, tags or `IsConnected` can't be trusted | [`mep-trace.md`](mep-trace.md) |
| "Mistake", "undo", "go back" — reversing something | [`undo.md`](undo.md) |
| **"It's not deleting", "I can't move it", "it's already synced and it's still there"** — an element that survives every sync, with *Can't edit the element. It was deleted in the Central Model.* Telling a stale local from a corrupt central, and why no client-side fix can ever win | [`worksharing-central-corruption.md`](worksharing-central-corruption.md) |
| Space airflow params; how many air terminals; terminal grid layout; a terminal's Flow value | [`hvac-terminals.md`](hvac-terminals.md) |
| Changing a family's CATEGORY (e.g. Duct Accessory → Air Terminal) — what survives, what is silently dropped, what to check first | [`family-category-change.md`](family-category-change.md) |
| Drawing duct between points; branch duct (riser + elbow + takeoff); connecting to an existing open end; drawing FROM a connector | [`hvac-ducts.md`](hvac-ducts.md) |
| Putting diffusers/sprinklers/lights on the CEILING TILE CENTRES — reading a ceiling's real tile size and angle, and which elements actually sit over an L-shaped ceiling | [`ceiling-grid.md`](ceiling-grid.md) |
| Two runs already exist and DON'T MEET — closing the gap, the offset crank, stretch-vs-create, one sub-transaction per attempt | [`mep-connect-existing-runs.md`](mep-connect-existing-runs.md) |
| Slicing a trunk into progressively smaller segments for duct sizing; why the trunk gets split; recovering an orphaned branch | [`hvac-duct-sizing.md`](hvac-duct-sizing.md) |
| Placing an FCU (or similar equipment) relative to a door; rotating equipment to face a target direction | [`hvac-equipment-placement.md`](hvac-equipment-placement.md) |
| **Grids and levels** — extents short in one view but not another, the 2D/3D toggle, resetting to the shared 3D extent, stretching levels to a section box, moving the bubble to the other end | [`datums.md`](datums.md) |
| **A level has TWO heights** — `Elevation` vs `ProjectElevation`, and which one is in the same space as an `XYZ`. Silent, conditional, and it was wrong in fifteen fragments here until 2026-08-24 | [`level-elevation-vs-project-elevation.md`](level-elevation-vs-project-elevation.md) |
| **Placing doors and windows by script** — why the obvious fragment does not host them, why half of them come out facing the wrong way in a corridor layout, and checking by room containment rather than coordinate | [`hosted-doors-and-windows.md`](hosted-doors-and-windows.md) |
| **Cutting openings / sleeves** where MEP passes through a wall, floor or beam — finding the real crossing, and the three different `NewOpening` calls | [`mep-openings.md`](mep-openings.md) |
| Moving/copying/rotating elements — and why a transform can silently do NOTHING while reporting success (pinned elements, group members) | [`geometry-and-transforms.md`](geometry-and-transforms.md) |
| Pushing the MEP Color Data Standard (Excel) into System Types / Materials / View Filters | [`mep-color-standard.md`](mep-color-standard.md) |
| Turning a color STYLE word ("pastel", "neon", "muted") into real RGB; guaranteeing distinct colors across several groups | [`color-vocabulary.md`](color-vocabulary.md) |
| A color/override change isn't showing — which graphic mechanism beats which (Linework, per-element, Filters, Category, Object Styles, ...) | [`graphic-override-precedence.md`](graphic-override-precedence.md) |
| **Colouring ANYTHING that can carry a wrap** — the highlight looks grey on screen even though the tool reported success, because the insulation stayed dimmed. Ajmal's standing rule | [`insulation-follows-host.md`](insulation-follows-host.md) |
| **A junction looks lumpy or squeezed after a resize** — a fitting stuck at the old size with reducers bolted on, versus a single-size fitting family that cannot reduce at all. Ajmal's rule: a fitting is the size of the biggest pipe on it | [`fitting-size-follows-biggest-pipe.md`](fitting-size-follows-biggest-pipe.md) |
| A bulk change raises Revit warnings, or a script "succeeds" and the model is unchanged — and why the usual `IFailuresPreprocessor` answer cannot be written in a fragment at all | [`failure-handling-without-a-class.md`](failure-handling-without-a-class.md) |
| **A query is slow, or I am about to write a new one** — what a `FilteredElementCollector` actually costs, and the four choices that change it (view-scoped is ~6x faster, an existence check ~80x cheaper than a count, ids cheaper than elements, `UnionWith` the expensive way to say "or") | [`query-cost.md`](query-cost.md) |
| **Which argument does this member need, and does it even exist** — the members that answer for the wrong thing when called blind (paint vs geometry, per-view, per-phase), the validators that turn a bare exception into a sentence, and the names that are simply not on the type | [`api-members-that-need-an-argument.md`](api-members-that-need-an-argument.md) |
| **Dimensioning by script** — why it fails, the three ways to get a geometry `Reference`, and the one option that makes a duct's centreline reachable | [`dimensioning.md`](dimensioning.md) |
| Placing tags by script; finding the right tag family; leader elbows/side; tag overlap; view scale and clearances | [`tagging.md`](tagging.md) |
| Revisions and revision sequences | [`revisions.md`](revisions.md) |
| Building a parametric family in the Family Editor (geometry, parameters, resize test) | [`families.md`](families.md) |
| What was done on a past date | [`log.md`](log.md) |

## The two rules that override everything in these files

1. **Verify, don't trust.** Revit's own data describes intent, not reality. `IsConnected`, element names
   and tags have all been proven wrong here. Get the real answer (geometry, a second property, walking
   the model) and report what you actually found — see [`mep-trace.md`](mep-trace.md) for the proof case.
2. **Fresh reads, never recall.** The user edits and undoes things in Revit between messages. Re-query before
   acting on "known" state; read back after changing anything. Every number (clearance, flow, height) is a
   per-request input — never reuse a past session's value as a default.

## Before writing new C#

Check [`../../scripts/README.md`](../../scripts/README.md) first — most requests compose from existing
fragments ("which elements" + "what to do to them") instead of a fresh bespoke script.

## Adding new knowledge

Put it in the **one** topic file it belongs to, and add a row above only if it's a genuinely new topic.
Never duplicate a fact across two files. If a topic file grows past ~300 lines, split it and update this
table — the point of this folder is that no single read is expensive.
