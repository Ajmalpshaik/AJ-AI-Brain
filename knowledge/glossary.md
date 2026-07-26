# Glossary — the user's terms → Revit terms

Maps how the user actually says things (often dictated, sometimes garbled) to the exact Revit API
meaning. Read this when a request uses ambiguous or misheard terms. Update it whenever a new term
causes confusion — that's the whole point of this file. Entries here are the durable *shape* of a
confusion (a term that's genuinely ambiguous, a dictation quirk); replace any project-specific example
values with your own project's real ones as you go.

- **Element ID (Revit)** → a unique number Revit assigns to every single element in a project — walls,
  doors, pipes, rooms, views, sheets, everything. No two elements share the same ID in one model. Used to
  find, select, or track an element individually (e.g. Add-ins → Select by ID, `filter-by-id-list.cs`,
  or in schedules/reports). Stays the same for the life of that element in that model, but changes if the
  element is copied into another project. One-liner for reference: *"Element ID – unique identifier Revit
  assigns to each element in a project, used to locate, track, or reference elements individually (not
  the same as Type ID or Global ID in IFC)."* **Whenever a script/skill reports on specific elements**
  (not a bare count) — always include each one's Element ID in the output, so the user can reference,
  re-select, or track that exact element afterward. The `report-*` action fragments already do this by
  default; keep it that way for any new one.
  **A second, more stable identifier also exists: `Element.UniqueId`** — a GUID-based string stamped on
  the element at creation, distinct from the integer `Element.Id`. Prefer `Id` for anything within one
  live session (it's what every fragment in this library already uses, and it's what the user reads off
  screen) — but if a script ever needs to re-identify the *same* element reliably across a worksharing
  sync, a detach-from-central, or any operation that can renumber integer IDs, `UniqueId` is the one that
  survives that; `Id` is not guaranteed to.
- **VCD** → Volume Control Damper. A *family* inside the **Duct Accessories** category
  (`OST_DuctAccessory`), not its own category.
- **"Fitting" / "hitting" (dictation)** → **ambiguous — do not assume Duct Fitting by default.**
  Could mean **Duct Fitting** (`OST_DuctFitting`) or **Pipe Fitting** (`OST_PipeFitting`). Check the
  surrounding context (is the conversation about ducts or pipes?) before picking one. If unclear, ask.
- **"Debt accessories" (dictation)** → Duct Accessories (`OST_DuctAccessory`).
- **"HVAC plants" (dictation)** → HVAC / Mechanical **floor plan views** (e.g. "1 - Mech"), not physical
  plant equipment.
- **Sub-Discipline** → a project parameter on views (separate from the built-in `Discipline` parameter)
  used to further classify Mechanical-discipline views as HVAC vs Piping etc., if the project has one.
  String-valued, not an enum.
- **Units in speech** → numbers like "four thousand" almost always mean millimetres (mm) unless the user
  says otherwise — confirm the user's working unit (mm vs feet/inches) once per project rather than
  assuming, since Revit's internal API always uses feet regardless of what the user speaks in.
- **A project's own system-type naming is not universal — read it fresh each project.** Pipe/duct
  System Type names (what shows up in `RBS_PIPING_SYSTEM_TYPE_PARAM` / the duct-system equivalent) are
  whatever that project's team named them — abbreviations like "CDP" for condensate drain, or a family
  of related system names sharing a prefix (e.g. several refrigerant sub-types all starting the same
  way), are project conventions, not a Revit standard. When the user names a system in speech ("the
  refrigerant pipes", "the CDP"), resolve it against *this* project's actual system-type list before
  filtering — don't assume a naming scheme from a different project carries over.
- **When a family has more than one "identity" parameter, confirm which one is authoritative before
  trusting it.** `Mark` is a generic built-in parameter that's often inconsistently populated (blank on
  some instances, holding unrelated values on others) — a project may instead use a dedicated instance
  parameter (e.g. an "Equipment Tag"-style parameter) as the real naming convention. If a query using one
  identity parameter produces unfamiliar-looking results, that's a signal to check whether a *different*
  parameter is the one the project actually keys on — don't conclude the equipment set changed.
- **Naming/tags describe intent, not guaranteed physical connectivity — verify by tracing.** Don't assume
  two pieces of equipment are paired just because their tags/codes look like they match (e.g. `A` pairs
  with `A`) — real wiring can be cross-connected, redundant, or otherwise not follow the naming at all.
  `Connector.IsConnected` has also been proven unreliable for this (can read `false` on a genuinely
  connected system, so a connector-graph walk alone can miss real connections). The reliable method: walk
  the pipe/fitting chain **geometrically** — match each element's connector origin to the nearest other
  element's connector origin within a small tolerance (e.g. ~50mm), continuing hop by hop until reaching
  the equipment at the other end. Full technique and worked example in
  [`live-model/mep-trace.md`](live-model/mep-trace.md).
- **"Schedule" → ambiguous, two different things — always check which one before acting:**
  1. A real **Revit model schedule** — an actual `ViewSchedule` element created in the document, shows up
     in the Project Browser, persists in the model.
  2. Just a **schedule-style table shown in the chat reply** — plain formatted text/markdown, nothing
     created in the model at all.
  If the user says "create a schedule" without making clear which, ask. Don't default to one — creating a
  real Revit element when they only wanted a quick chat table is unwanted model clutter; replying with a
  chat table when they wanted a real schedule means they have to ask again.
- **"Color/material standard" workbook** → some projects keep a master spreadsheet (Excel) mapping each
  MEP system to a standard color/material, with columns for a discipline code, a system name, and a
  target color. If the user has one, ask where it lives and what its columns mean before syncing it into
  Revit — column layouts vary project to project. The general sync technique (Excel → Revit System Types
  → Materials → View Filters) and the Revit-side gotchas that came up doing this are in
  [`live-model/mep-color-standard.md`](live-model/mep-color-standard.md).

- **"Coverage" → ambiguous, two different jobs exist in this Brain — ask which before starting.**
  (a) *Report* coverage of elements that already exist — "how much floor does each diffuser serve"
  → [`action-report-coverage.cs`](../scripts/actions/reporting/action-report-coverage.cs), which also has
  to be told WHERE the radius comes from (spacing / flow / a stated figure) because the standard supply
  diffuser carries no coverage parameter at all. (b) *Generate* a layout that doesn't exist yet — "how
  many sprinklers at 3 m coverage, and where do they go"
  → [`generate-room-coverage-layout.cs`](../scripts/recipes/generate-room-coverage-layout.cs). "Draw the
  coverage" points at (b); "what's the coverage" usually points at (a). Guessing wrong wastes the whole run.
  **(c) If the devices are fire sprinklers it is neither — it is a code job**, with several simultaneous NFPA
  limits and no coverage-radius concept at all →
  [`skills/ajtools-fire-sprinkler-layout/SKILL.md`](../skills/ajtools-fire-sprinkler-layout/SKILL.md).
- **"Fire fighting" / "fire figting" (dictation)** → the sprinkler system, and in this Brain it means the
  NFPA-governed sprinkler *layout/check* job, not HVAC or generic coverage. It does NOT mean hydraulic
  calculation, pipe sizing or pump selection — none of which this Brain does.
- **"Hexagonal" / "staggered" grid (device layout)** → the same thing: alternate rows shifted by half the
  in-row spacing. Worth knowing it is NOT automatically the cheaper option inside a room, despite the
  textbook plane result — see the gotchas in `generate-room-coverage-layout.cs` before quoting a saving.

### Log
- Seed entry — "fitting" is NOT always Duct Fitting; pipe fittings exist too, context decides.
- "schedule" is ambiguous between a real Revit `ViewSchedule` and a chat-only table — ask which one
  rather than assuming.
