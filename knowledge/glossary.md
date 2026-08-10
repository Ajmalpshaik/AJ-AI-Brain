# Glossary — the user's terms → Revit terms

Maps how the user actually says things (often dictated, sometimes garbled) to the exact Revit API
meaning. Read this when a request uses ambiguous or misheard terms. Entries here are the durable *shape*
of a confusion (a term that's genuinely ambiguous, a dictation quirk); replace any project-specific
example values with your own project's real ones as you go.

**Write the user's own words down as they use them — don't wait for a term to cause confusion first**
(the user's rule, 2026-08-10: *"this is my normal work and you have to remember the words am using"*).
This file is a record of their working vocabulary, not only a log of misunderstandings. The reason is
measured, not stylistic: the semantic index's weakest spot is site vocabulary that appears nowhere in
these files, so a word only exists to a future session if it was written here. Record it in **their**
phrasing on the left, the Revit meaning on the right — a term that read as obvious the day it was said
is exactly the one a fresh session cannot route.

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
- **"Biggest/maximum size" of a duct or pipe → ambiguous, and there is no single right answer.**
  Rectangular ducts carry **Width × Height**; round ducts carry **Diameter**. No one number ranks the two
  kinds together, so `1200x600`, `900ø` and `800x800` are each "the biggest" under a different rule —
  largest first dimension, largest single dimension, or largest cross-sectional area. **The size-breakdown
  table is sorted by first number then second** (`action-count-and-report.cs`,
  `action-report-length-by-size.cs`), so its last row is the largest *first dimension*, NOT necessarily the
  largest duct. Don't report that row as "the maximum" without saying which rule it reflects — ask which
  measure the user means, or give the breakdown table and state the rule plainly.
  Separately, **"size" itself points at two different data sources**: the `Size` *string* parameter
  (`RBS_CALCULATED_SIZE`, e.g. `"450x250"` or `"250ø"`, one text value covering both shapes) versus the
  individual numeric Width / Height / Diameter parameters. The string is right for grouping and display;
  the numerics are right for comparing, filtering or sorting. Pick deliberately — they answer different
  questions, and only the numerics can be compared across round and rectangular.

- **"Grayout" / "gray out" / "grey out" (the user's word, MEP work)** → **his name for a defined,
  multi-step MEP procedure — NOT a one-off graphic tweak, and NOT yet documented here.** He named it on
  2026-08-10 and said he would teach the steps one at a time: *"if I say the grayout for the MEP work it
  means you need to do some work, the work one by one I will tell you."*
  **Until the step list below is filled in, do not guess what it means and do not start doing anything.**
  In particular, do not assume it is simply halftoning or greying the non-MEP elements in a view — that is
  only the most obvious reading of the English word, and he has explicitly said it is a sequence of work,
  so acting on the obvious reading risks doing the wrong job to a real model. If he says "grayout" and the
  steps are still missing here, ask him for the next step rather than improvising one.
  **Trigger phrasings (his, 2026-08-10):** "do the grayout", **and** "do the grayout for MEP" — he
  corrected this explicitly, so treat both as the same instruction; do not require the "for MEP" half.
  **Steps as taught:**
  1. *(candidate — asked, NOT yet confirmed by him as step 1)* In the active view's Visibility/Graphics,
     turn **every model category ON**, then turn **OFF only Structural Rebar (`OST_Rebar`) and Structural
     Rebar Couplers (`OST_Coupler`)**. Parent categories only — leave sub-categories (centre lines, Interior
     Fill, Reference) at whatever they were, since Revit's defaults have most of those off deliberately.
     Done live on 2026-08-10 in view "1 - Mech": 20 turned on, 63 already on, 33 not controllable, 2 off.
  2. *(candidate — 2026-08-10)* Set the **line and pattern for projection and cut**. Colour is
     **two different greys, and getting them the same way round matters:** lines are the darker
     **RGB 150,150,150** (projection line + cut line), patterns are the lighter **RGB 200,200,200**
     (surface pattern + cut pattern), pattern set to **`<Solid fill>`**. He corrected this mid-run —
     *"sorry patens color i need 200,200,200"* — so a single 150 grey for everything is the wrong answer.
     **Applies to EVERY model category — MEP included, no exceptions.**
     *(He was asked first whether MEP should stay at its normal Revit colours and said keep-MEP-normal;
     then corrected it mid-run — "no all make it we will change that after and you can change all to
     150,150,150". So: grey the lot in this step; the services get their real colours back in a later step,
     not by being skipped here. Do not re-ask this.)*
     Done live in view "1 - Mech": 85 categories written, 33 not controllable. Read back:
     **80 hold the 150 line, 38 hold the 200 surface fill, 24 hold the 200 cut fill, 5 hold nothing.**
     **Expect most to come back partial** — Revit silently discards parts of a category override,
     measured in detail in
     [`live-model/graphic-override-precedence.md`](live-model/graphic-override-precedence.md). The
     architectural/structural background greys completely; **MEP greys as lines only** (its fill is
     discarded); and Rooms, Areas, Spaces, Raster Images and Point Clouds take nothing at all.
  When the full sequence exists, it stops being a glossary entry and becomes a skill or a recipe — route it
  with [`skills/brain-self-maintain/SKILL.md`](../skills/brain-self-maintain/SKILL.md) Step 1 and leave a
  pointer here.

- **Family filename prefix — `TRG_`, and this does NOT contradict the `MEP_` line-style rule.** The
  office family library at `D:\Ajmal\BIM Resources\BIM Resources\NEW` is uniformly
  `TRG_<TYPE>_<Description>_<Model>.rfa` — e.g. `TRG_CRAC_Close Control Air Conditioning_NRG1103.rfa`,
  `TRG_EDH_T001_ElectricalDuctHeater.rfa`, `TRG_FAN_RooftopCentrifugalExhaust_RTC-300D6-0.18-EX.rfa`.
  The standing "office prefix is `MEP_`, never AJ or TRG" rule is about **line styles / drafting
  standards only** (see `create-mep-line-standards.cs`) — applying it to a family filename would make
  that family the odd one out in its own library. **Two different namespaces: `MEP_` for annotation and
  line standards, `TRG_` for loadable families.** Confirmed 2026-08-10 by listing the folder before
  saving a new humidifier family; the user chose `TRG_` over the generic ISO 19650 element name
  (`[System]_[ElementType]_[Size/Spec]`) precisely so it matched the library. **Look at the destination
  folder before naming a family** — the house convention beats the generic standard, and it is one
  directory listing away.

### Log
- Seed entry — "fitting" is NOT always Duct Fitting; pipe fittings exist too, context decides.
- "schedule" is ambiguous between a real Revit `ViewSchedule` and a chat-only table — ask which one
  rather than assuming.
- 2026-08-10 — remit widened. This file now records the user's working vocabulary as he says it, not
  only terms that already caused a misunderstanding. His instruction, verbatim: *"from now if I say
  something you have to remember okkey this is my normal work and you have to remember the words am
  using."* Nothing was removed — the existing confusion entries stand; the bar for adding one dropped.
- 2026-08-04 — "the maximum duct size" has no single right answer: round carries Diameter, rectangular
  carries Width × Height, and the breakdown table's last row is the largest FIRST dimension, not the
  largest duct. Found by walking that exact question through the routing to see what a fresh session
  would do with it.
