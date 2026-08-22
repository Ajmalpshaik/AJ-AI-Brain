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

> **split-review: kept whole** (reviewed 2026-08-20, at 309 lines). Past the ~300-line rule and staying
> that way. The rule is a split candidate, not a mandate, and its purpose is to cut the cost of *reading a
> file whole* — but **nobody reads a glossary whole.** It is a lookup: you arrive with one word and leave
> with one meaning. There is a visible seam (the term list, then the dated `### Log`), and splitting on it
> would still be wrong: both halves answer the same question — *what does Ajmal mean by this word* — so a
> split just means guessing which file a word landed in, and the semantic index would have to get that
> guess right too. That is the "splitting adds hops" case the rule names.
>
> It will keep growing, by design — his standing rule is that his words get written down every session.
> Growth alone is not a reason to revisit this. **Re-open the decision only if a genuinely different
> subject starts accumulating here** — a body of entries that someone would look up *without* having heard
> Ajmal say the word first. That would be a different question, and it would deserve its own file.

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
- **"Duck" (dictation)** → **duct** (`OST_DuctCurves`). Said 2026-08-13 as the two-word command
  **"duck hide"** — meaning *hide the ducts in the current view*. Two things to carry forward, not one:
  1. **"Duck" is always "duct".** There is no Revit category, family or parameter called "duck", so a
     bare "duck" never needs a clarifying question — just read it as duct and say which reading you took.
  2. **He gives terse commands as `<noun> <verb>`, not `<verb> the <noun>`.** "duck hide" = hide the
     ducts; expect the same shape for others ("pipe isolate", "wall hide"). Read the noun as the target
     and the trailing word as the action — don't stall on the word order.
  **Scope: the bare noun means that category only.** "duck hide" hid the 3 elements in **Ducts** and left
  Duct Fittings visible — correct for what he said, but the fittings then float unattached on screen, so
  say what is still showing and offer to hide them too rather than assuming either way.
- **"Hired" (dictation)** → **hidden**. Said 2026-08-13: *"The remaining everything need to be hired."*
  Nothing in Revit is "hired", so read it as hidden every time.
- **"Deduct fittings" (dictation)** → **Duct Fittings** (`OST_DuctFitting`). Same swallowed-consonant
  pattern as "debt accessories" → Duct Accessories, above. Note this one names the category outright, so
  the "fitting"/"hitting" duct-vs-pipe ambiguity further up does **not** apply — "deduct fittings" is
  unambiguously the duct side.
- **"See only the X" / "the remaining everything need to be hidden"** → **isolate X**, not hide X — and
  the two are opposites, so getting it backwards blanks exactly the wrong half of the view. Use
  `isolate_elements` (it resets any prior temporary hide/isolate itself, so no separate reset call is
  needed); `reset_isolation` is Revit's Reset Temporary Hide/Isolate and puts everything back.
  **The trap this came from (2026-08-13):** he first said **"duck hide"**, which is hide-the-ducts, then
  immediately corrected to **"see only the duct"**, which is the exact inverse. The two phrasings sit one
  word apart in speech and one `isolate`/`hide` call apart in the model. When a follow-up message
  re-states the same noun with a different verb, assume he is **correcting the direction**, not adding a
  second action on top — re-read which half he wants left on screen before running anything.
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

- **"Grayout" / "gray out" / "grey out" (the user's word, MEP work)** → **his own view standard for a
  coordination drawing: the architectural/structural background flattened to grey, the services brought
  forward in black, insulation as a quiet dashed wrapper, rebar off.** Trigger phrasings, both his and
  both meaning the identical job: **"do the grayout"** and **"do the grayout for MEP"**.
  **The values are settled — do not re-derive them and do not ask him again**, which is the whole point
  of writing them down (his instruction, 2026-08-10: *"if i need to do in anothor model i will tell you
  only that grayout for mep so the all same work need todo"*).
  Full scheme, the reasoning behind each value, and the three that look wrong but are not:
  → [`skills/ajtools-mep-grayout/SKILL.md`](../skills/ajtools-mep-grayout/SKILL.md)
  → runs via [`scripts/recipes/mep-grayout.cs`](../scripts/recipes/mep-grayout.cs)
  → what Revit silently discards doing it: [`live-model/graphic-override-precedence.md`](live-model/graphic-override-precedence.md)

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

- **"voice mode to Revit and from Revit" / "Claude will talk to Revit and Revit will talk to Claude"**
  → the user's words for the **whole two-way channel between the assistant and Revit**, not the spoken
  narration. Said 2026-08-11. **This reads like a microphone request and is not one** — a fresh session
  hearing "voice" reaches for speech-to-text, and that is the wrong half. He means the MCP bridge:
  Claude sends, Revit answers. When he says "voice" about the *bridge*, "talk" means **send a message**,
  not make a sound. Ask which layer he means before designing anything; the two are separate systems
  that never touch (see [`../tools/voice/README.md`](../tools/voice/README.md) for the sound layer, and
  the add-in's `McpBridgeService.cs` for the message layer).
  **The distinction that actually matters to him:** Revit can only ever *answer* — it cannot start a
  sentence. Nothing in the bridge lets Revit send anything unasked, so "Revit will talk to Claude" is
  half-built, not built.

### Log
- Seed entry — "fitting" is NOT always Duct Fitting; pipe fittings exist too, context decides.
- "schedule" is ambiguous between a real Revit `ViewSchedule` and a chat-only table — ask which one
  rather than assuming.
- 2026-08-10 — remit widened. This file now records the user's working vocabulary as he says it, not
  only terms that already caused a misunderstanding. His instruction, verbatim: *"from now if I say
  something you have to remember okkey this is my normal work and you have to remember the words am
  using."* Nothing was removed — the existing confusion entries stand; the bar for adding one dropped.
- 2026-08-13 — "duck" = duct, and the `<noun> <verb>` command shape ("duck hide" = hide the ducts).
  Recorded on first use, before it caused any confusion, per the 2026-08-10 rule above.
- 2026-08-13 — "hired" = hidden, and "see only the X" = isolate X. Both from the same exchange, where
  "duck hide" was corrected to "see only the duct" one message later — logged with the hide/isolate
  inversion trap that near-miss exposes.
- 2026-08-04 — "the maximum duct size" has no single right answer: round carries Diameter, rectangular
  carries Width × Height, and the breakdown table's last row is the largest FIRST dimension, not the
  largest duct. Found by walking that exact question through the routing to see what a fresh session
  would do with it.
- 2026-08-13 — **"the bigger counts" = the most NUMEROUS group, not the largest size.** Said live, as
  *"all the vcds that bigger counts... i thing 200X200 size"* — and he was right, 200×200 was the most
  common at 11 of 66. The trap is that "bigger" reads as size, and the same conversation had just asked
  for the *biggest width*, which was a genuinely different element (950×800, one only). **So "biggest"
  and "bigger counts" are two different questions one message apart.** If a request could mean either,
  say which one you are answering — or ask. Related: the 2026-08-04 entry above, where "maximum duct
  size" had the same shape of ambiguity.
- 2026-08-14 — **"visualization" (his word) = a CHART or DASHBOARD of the model's numbers**, not a 3D
  render, not a Revit visual style, not a rendering job. Said as *"i need always need visualization...
  if vishalization needdd it need to come"* while showing two dashboard pages of Revit data. The trap is
  real: in normal Revit vocabulary "visualization" means rendering/walkthrough work, so a fresh session
  could route this to the exact opposite job. If he ever means an actual render he will say render,
  camera, or 3D view. Standing rule and workflow:
  [`../skills/ajtools-visual-report/SKILL.md`](../skills/ajtools-visual-report/SKILL.md).
- 2026-08-14 — **"artifact" (his word) = a published HTML page with its own link**, the shareable kind —
  as opposed to a chart drawn inside the chat reply. Said as *"if i ask the artifects its need to come
  like this you make html file"*. This is a **request word, not a default**: he wants the chart in the
  chat normally, and only a page when he says artifact / dashboard / link / "I want to send it". Getting
  this backwards buries the answer behind a link, which is exactly what he corrected. See
  [`../skills/ajtools-visual-report/SKILL.md`](../skills/ajtools-visual-report/SKILL.md).
- 2026-08-19 — **"sound attenuator", never "silencer"** — his correction, in his own words: *"sound
  attenuator NOT Silencer ADD HTIS"*. Same device, and Revit content often ships the other word: the
  family in this project is literally named `TCM_ATN_T001_Silencer_Rectangular` while its `ATN` code
  means attenuator. So the wrong word is sitting in the model waiting to be copied. Write **Sound
  Attenuator** in any description, schedule, register or reply. Found while rewriting duct-accessory
  Descriptions on `4355-BHVD-3D-60P00-BL006A`.
- 2026-08-19 — **his description format is `<Device in words> - <Shape>`**, taken from the family name
  and dropping deflection, plenum/duct-mounted and material — e.g.
  `TCM_SAG_T200_SupplyAirGrille_Single-Deflection_Rectangular_WithPlenum` becomes **"Supply Air Grille -
  Rectangular"**. Applied to a whole family at once, every type, not only the placed ones. Consequence he
  accepted knowingly: the material (aluminium / galvanised steel / PVC) disappears from the text.
- 2026-08-19 — **the model file name is a data source, not just a label.** Ajmal's rule, his own words:
  *"CWA it will not be always 60P00, this is as per the model name our 4355-BHVD-3D-60P00-BL006A.rvt...
  after 3D what is that 60P00, so that's why its came."* The title reads
  `<project>-<disc><sub>-3D-<CWA>-<block>`:

  | Token | In `4355-BHVD-3D-60P00-BL006A` | Meaning |
  |---|---|---|
  | 1 | `4355` | project number |
  | 2 | `BHVD` | **Sub-Discipline = the first two characters, `BH`** (confirmed by Ajmal 2026-08-19: *"yes Sub-Discipline also from model name BHVD"*), then `VD`; sheets prove the split by writing `4355-BH-VD-…` |
  | 3 | `3D` | model type |
  | 4 | **`60P00`** | **CWA — Construction Work Area.** The token straight after `3D` |
  | 5 | `BL006A` | building / block |

  So `4355-BHVD-3D-60A10-BL002A` carries CWA `60A10`. **Derive BOTH CWA and Sub-Discipline from the open document's title
  every time; never carry either from the last model.** Same for the sheet numbers, view names and levels — Ajmal:
  *"now new model so model number will be different and sheet numbers will be different, view name levels
  totally different."* Automated in
  [`../scripts/recipes/fill-mm-document-register.cs`](../scripts/recipes/fill-mm-document-register.cs),
  which aborts rather than guess if the title has no `3D` token.
- 2026-08-19 — **CWA = Construction Work Area**, the site's zoning of a plant into work areas. Appears as a
  shared parameter on every element in the MM_ register and as segment 4 of the model file name.
- 2026-08-19 — **`View Owner` and `View Use Group`** are the two custom view parameters that drive the
  Project Browser grouping on the Tecnimont MM_ projects — `Tecnimont` and `03 - MileMate!` on 4355. A newly
  created schedule has them **empty**, so it lands outside the browser structure and looks lost. Ajmal:
  *"there is parameter for this schedule, view owner and view use group, that we need to change as per the
  MM_V03, same like that we need for new creating schedules also."* Copy them off the project's own `MM_V03`
  at run time rather than hardcoding — see
  [`../scripts/creators/create-schedule.cs`](../scripts/creators/create-schedule.cs).
- 2026-08-19 — **the Equipment Tag prefix identifies the equipment; the family name often does not.** Ajmal's
  method, his own words: *"ACCU or ACU Equipment Tag is HCN, it means that is CRAC unit outdoor."* On
  4355 the tag is `<PREFIX>-<number>` and the prefix is the real type code. It cut through a genuinely
  messy library on `4355-BHVD-3D-60P00-BL003A`, where **four different family names** — `ACCU_Type 1.1`,
  `ACCU_Type 1.3`, `ACU_Type 2.1`, `ACU_Type 2.2` — all carry `HCN` and are one piece of equipment. Read
  the tag before trusting a family name, and before asking what something is.

  | Prefix | Equipment | Seen on |
  |---|---|---|
  | `HCN` | **CRAC Unit Outdoor** (Ajmal, 2026-08-19) | ACCU/ACU families |
  | `HSU` | indoor split units — VRF wall-mounted, ceiling cassette, precision AC | `TCM_WallMounted`, `STI_ME_ATU…cassette`, `Precision Air Conditioner` |
  | `HEF` | exhaust fan (axial and centrifugal alike) | `TCM_EAF_T001`, `TCM_ECF` |
  | `HGH` | exhaust louvre | `TCM_EAL_T100_J002` |
  | `HPU` | packaged unit | `Packaged Air Conditioner` |
  | `HAB` | PRF fan | `TCM_PRF_T004_FAN` |
  | `HAH` | air handling unit | `AHU` |
  | `HGS` / `HGR` / `HSL` | supply / return air terminal, sand trap louvre | air terminals |
  | `HSL` louvre families | `TCM_STL_T001_J004_SandTrapLouvre`, `…_J005_…` — seen correctly categorised on BL003A, 2026-08-20 | air terminals |
  | `HVD` / `HFD` / `HGD` / `HND` | volume / fire / gas-tight / non-return damper | duct accessories |
  | `HSA` | **sound attenuator** — the project's own code agrees with [[his "never silencer" rule]] | duct accessories |
  | `HAF` / `HWL` / `HEH` / `HID` / `DAD` | air filter / weather louvre / electric heater / — / duct access door | duct accessories |

  Untagged is itself a signal: on BL003A the only equipment with no tag at all was
  `MC_ConnectionNode_Outdoor_Supply_Air_Rectangular`, a placeholder rather than real equipment — Ajmal's
  ruling on it was *"no need to look"*.
- 2026-08-19 — **his equipment wording, given directly:** `TCM_WallMounted` → **"VRF Split Indoor"**;
  `STI_ME_ATU_Air Terminal Unit_cassette` → **"Cassette Air Conditioner Unit"**; `TCM_PRF_T004_FAN` →
  **"PRF Fans"**; `TCM_EAL_T100_J002_ExhaustLouvre1` → **"Exhaust Louvre"** (no shape suffix); ACCU/ACU →
  **"CRAC Unit Outdoor"**. From BL006A the same day: `PackagedAirConditioner` → **"Packaged Unit"**,
  `TCM_ACU_T002_AirCooledCondensingUnit` → **"Split Outdoor"**, `TCM_ECF_ExhaustCentrifugalFan` →
  **"Exhaust Fan for Inertial Air Filter"** (named by what it serves, not what it is).
- 2026-08-20 — **his fire-fighting wording, from the message that started the sprinkler build.** Written
  down in his phrasing, per his rule that the words matter as much as the conclusion. The spellings also
  went into [`site-vocabulary.md`](site-vocabulary.md) so a search finds the files whichever way he types.

  | His words | What it means |
  |---|---|
  | "srinkler" | sprinkler — his consistent spelling, in the same message four times |
  | "pendend sprinkler" | **pendent** head, pointing down below a ceiling |
  | "upraght sprinkler" | **upright** head, pointing up, used where there is no ceiling |
  | "wall sprinkler" | **sidewall** head — its own spacing table, not a pendent turned sideways |
  | "beem" / "colom" | beam (Structural Framing) / column (Structural Columns and Columns) |
  | "with celling how, without celling how" | the two deflector-height cases: below a ceiling, or below the slab and beams where there is none. This is the question [`fire-sprinkler/deflector-and-ceiling-height.md`](fire-sprinkler/deflector-and-ceiling-height.md) exists to answer |
  | "how mcuh from the wall" | the distance-to-wall rule — max half the allowable spacing, min 4 in (102 mm) |
  | "if upraght howmcuh from the slab" | the upright deflector rule — 25–305 mm below a flat soffit, or 25–152 mm below the **beam** where beams hang below the deck |
  | "room boundy" | the room's real boundary, as opposed to its bounding box — and he is right that it is the thing to lay out from |

  His framing of the whole job, worth keeping verbatim because it is the routing sentence a future session
  has to recognise: *"main aim to add sprinklers in the room with room boundy and if any beem or colom is
  there in the room as per that need to place"*. That is: room boundary in, obstruction-aware head
  positions out — the chain in [`fire-sprinkler/layout-method.md`](fire-sprinkler/layout-method.md).
- 2026-08-20 — **"sealing void" = the ceiling void, and the 800 he remembered is real but not NFPA.** His
  question, verbatim: *"as part of NFP, as I remember, okay, I'm not sure as part of the NFP, if the
  sealing wood... sealing and slab distance... this sealing void is more than eight hundred or something,
  we need a print and pendant also."*

  | His words | What it means |
  |---|---|
  | "sealing" | **ceiling** — dictation, and it appears in most of his sprinkler questions |
  | "sealing void" / "sealing and slab distance" | the **ceiling void** — the gap between the suspended ceiling and the slab above. NFPA's word for it is a **concealed space** |
  | "print sprinkler" | **upright** sprinkler — same dictation family as "upraght" |
  | "we need a print and pendant also" | the **two-layer** case: upright inside the void, pendent below the ceiling |

  **He was right to say he was not sure it was NFPA — it is not.** The 800 mm ceiling-void trigger comes
  from the BS lineage (BS 5306-2, superseded by BS EN 12845). NFPA 13 has **no depth trigger for a ceiling
  void at all**: it asks whether the concealed space is of combustible construction, and lets you omit
  sprinklers from a noncombustible / limited-combustible space with minimal combustible loading. The two
  standards therefore disagree on the same void in both directions. Full write-up, including which one
  applies on his jobs: [`fire-sprinkler/concealed-spaces.md`](fire-sprinkler/concealed-spaces.md).

  Worth keeping as a pattern, not just a fact: **he remembered the number correctly and the source
  incorrectly.** That is the normal shape of site knowledge, and it is why "which standard is this project
  on" belongs in the opening questions of every sprinkler job rather than being assumed.

- 2026-08-22 — **"widget" (his word) = a picture drawn IN THE CHAT REPLY.** Confirmed by him the same
  day, when asked directly. It is a synonym of his 2026-08-14 **"visualization"**, and it means the
  same three things: a diagram, a chart, or both, *in the reply itself* — never a published page, and
  never something to click. Said twice in one message while asking for an explanation of how the Brain
  works: *"tell me about the aj ai very simply what is hapening with shwo widget also"* and *"if i set
  message what is hapening... and with widget also"*.

  **The point is that it applies to explanations, not only to model numbers.** The 2026-08-14 rule was
  written around Revit figures ("two or more numbers that invite comparison"), and both uses here were
  about *how something works* — a pipeline, a before/after — with no model reading involved. So when he
  asks how anything works, draw it: a box-and-arrow flow for a process, a bar chart for a comparison.
  His **"artifact"** stays the separate, opposite thing — a published page with a link, only when he
  asks for one by name.
