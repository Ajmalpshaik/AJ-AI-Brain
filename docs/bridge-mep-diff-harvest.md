# Harvesting five repos — bridge architecture, MEP and model diff — the ledger

**2026-08-24.** Ajmal asked for ten GitHub repos, split them into two batches of five, and gave the
other five to a second session. This is the ledger for **this** session's five. Same method and the
same four-way verdict as the earlier harvests, per [`harvest-prompt.md`](harvest-prompt.md).

Working ledger, not knowledge — it lives in `docs/`, outside the search index. Per the standing rule
nothing here names a source.

## The five, and why these five together

Chosen so that this batch and the parallel one land in **different folders**: this one in
`knowledge/`, `actions/qa-checks/`, `actions/color-graphics/` and `actions/reporting/`; the other in
`actions/reporting/`, `context/` and `actions/sheets-views/`.

| Shape | Size | Verdict in one line |
|---|---|---|
| An in-process AI agent that writes and runs C# inside Revit | 48 files, 14.7k lines | **SKIP as code, KEEP one lesson** — it is a peer to our own bridge and it paid for a safety-gate finding |
| A task-based async wrapper for the Revit API | 28 files, 1.9k lines | **SKIP entirely** — every path needs a class we cannot declare |
| A unit-test runner that executes inside Revit | 110 files, 9.1k lines | **SKIP as code, KEEP one lesson** — the verification problem, solved a way we cannot copy |
| An MEP node library | 125 files, 19.7k lines | **BUILD 1** — and the wrapper itself is SKIP; the value is what it wraps |
| A model-comparison add-in | 66 files, 12.3k lines (half duplicated) | **BUILD 2** — the biggest blank in the library |

**57,601 lines across the five.** Two mechanical findings before any reading: the comparison add-in
ships **two byte-identical copies** of its core (`ComparisonMaker.cs` matches exactly in both trees), and
the test runner duplicates its controller between the runner and the add-in. Roughly a fifth of the line
count is the same code twice.

## The method step that did the most work

The library-harvest technique from the prompt — *diff the API surface, not the file list* — but tightened,
because the first attempt was useless. Extracting "capitalised token followed by a dot" from five repos
and diffing against the Brain returned the repos' own class names, `JsonSerializer` and `TaskScheduler`:
noise, not signal.

**The fix was to intersect against the Revit API itself.** Every public type and member name was
reflected out of the shipped `RevitAPI.dll` — **34,901 names** — and the diff became "tokens that are
really Revit API, that these repos use, and that appear in none of our 355 fragments". That produced a
list short enough to read and act on:

| Repo | Revit API names absent from the Brain |
|---|---|
| MEP node library | **159** |
| Model comparison | **67** |
| AI agent | 55 |
| Test runner | 16 |
| Async wrapper | **8** |

The last row is the whole verdict on that repo in one number, and it was worth more than reading it.

## BUILD — 4 new fragments

**Three came from the survey; the fourth came from the full read** (see the second pass below). All compile on Revit 2020, 2024 and 2027. **None has been run against a real model.**

| Built | The gap |
|---|---|
| [`action-show-analysis-heatmap.cs`](../scripts/actions/color-graphics/action-show-analysis-heatmap.cs) | **A gradient heatmap painted on the model, with Revit's own legend** — the Analysis Visualization Framework. `SpatialFieldManager`, `AnalysisDisplayStyle`, `AnalysisResultSchema` and the whole `Analysis` namespace appeared in **no fragment**. Every colour fragment here sets a flat per-element override: one colour per bucket, no scale, no legend, and 26 l/s looks identical to 260 l/s. This is a continuous scale — use the override fragments to show *which*, this to show *how much*. Pipes by pressure drop, spaces by airflow, ducts by velocity |
| [`action-report-routing-preferences.cs`](../scripts/actions/reporting/action-report-routing-preferences.cs) | **Which fitting Revit will insert, and at which sizes.** Routing preferences are what turn "draw a pipe" into "and put the right fittings on it", and they are invisible until something goes wrong. `recipes/draw-main-duct-with-cap.cs` reaches exactly one rule (`GetRule(Caps, 0)`) and nothing else in the library reads the table. Reports the whole table **and** answers the direct question via `GetMEPPartId` — "at 150 mm, what would you actually use?" |
| [`action-compare-models.cs`](../scripts/actions/qa-checks/action-compare-models.cs) | **Compare the open model against another .rvt** — added, removed, and changed parameters. `action-compare-elements.cs` compares elements inside ONE model; nothing compared two models, which is the version-to-version question a coordination job actually asks. Works through the background-open mechanism the folder upgrade already proved |

### The traps those builds are really made of

- **A heatmap that stores its data and draws nothing.** Creating the display style is not enough — the
  view's own `VIEW_ANALYSIS_DISPLAY_STYLE` parameter must be pointed at it. Miss that and everything
  succeeds and the model looks untouched.
- **The analysis schema id goes stale.** `RegisterResult` returns an int valid only while that schema is
  registered on that view's manager. Cache it, or let someone clear the view, and the next write targets
  an id that no longer exists. It is re-checked against `GetRegisteredResults()` on every run.
- ~~**Two models cannot be matched on ElementId.**~~ **WRONG, and corrected in the second pass below.**
  It is true of two unrelated models and false for a model and an earlier save of itself, which is the
  only case the fragment is for. Left visible rather than deleted, because it is the exact shape of
  mistake this ledger exists to catch: a plausible rule, written confidently, from a survey.

## KEEP OURS / SKIP — and two lessons worth more than the code

**The async wrapper: SKIP, and the number said so before the reading did.** Eight Revit API names absent
from the Brain, and its entire surface is `IGenericExternalEventHandler<TParameter,TResult>`,
`ExternalEvent` and static registries — **every path requires declaring a class and implementing an
interface, which a fragment body cannot do.** This is the fourth instance of that structural limit
(after `IFailuresPreprocessor`, `IDuplicateTypeNamesHandler` and `IFamilyLoadOptions`), and it confirms
the rule rather than bending it. Worth noting that it also solves a problem **the bridge already
solved** — running API code from a non-API context is what the add-in does.

**The MEP node library: the wrapper is SKIP, the wrapped list is the harvest.** It is a Dynamo package —
`Revit.Elements.Element`, `[NodeCategory("Query")]`, `.ToDynamoType()` — and Ajmal had already said no
Dynamo. That rules out the layer, not the content: underneath the wrappers are pure Revit API calls, and
159 of the names were absent here. The prompt's own guidance applies exactly — *a wrapper library's real
value is the LIST of what it wraps*.

**The AI agent: SKIP as code, and one finding that is worth the whole repo.** It puts a policy gate
between the model's tool call and execution, with rules that force confirmation on destructive work. Its
own comment records what that cost to tune:

> Only block bulk deletes in loops — high risk, irreversible. Regular transaction writes are handled at
> the prompt level. **Double-gating at code level caused 2-3x confirmation roundtrips and confused weaker
> LLMs into infinite loops.**

That is a measured lesson about safety-gate design in exactly our kind of system, and it lands on a live
question here: our equivalent is a *convention* (dry-run by default in every fragment header) plus the
agent's judgement, with no code-level gate at all. The lesson is not "add one" — it is that **gating the
same operation twice is worse than gating it once**, so if a code-level gate is ever added to
`mcp-server/`, the fragment-header convention for that operation should come out at the same time.
Recorded, not built: it changes `mcp-server/`, not `scripts/`, and that is a separate decision.

**The test runner: SKIP, and it names our own weakness.** It exists to run unit tests *inside* Revit,
which is precisely the gap `brain-status.mjs` reports every session — a large share of the library has
never run against a real model. But its mechanism is an add-in command plus a file-watcher protocol and
a WPF app, none of which a fragment can be. It does not transfer. What it does is make the point that
our verification problem has a known shape and a known cost, and that our answer to it remains "run one
element first and check the result".

## What this harvest found in OUR code

- **`recipes/draw-main-duct-with-cap.cs` reads routing preferences the shallow way.** `GetRule(group, 0)`
  takes the FIRST rule in the group regardless of the size being drawn. That is fine for a cap and wrong
  as a general habit: the rule that matches depends on the size, which is what `GetMEPPartId` is for. The
  recipe is unproven and MEP-fixture-blocked, so it was not changed — but the new fragment now documents
  the correct lookup, and that is the thing to reach for next time.
- **Nothing in the library had ever used the `Analysis` namespace**, so "show me this number on the
  model" had only one answer here — flat colour overrides — and that answer silently discards magnitude.
- **`Document.GetDocumentVersion` was unused.** `VersionGUID` plus `NumberOfSaves` is the authoritative
  "is this the same model, and has it been saved since" check, and it is better than any file date. It is
  now the first line of the comparison output, precisely so a same-GUID pair is caught *before* anyone
  reads a diff that cannot be real.

## Small facts worth keeping

- **A routing preference rule has no size on it.** The rule holds a LIST OF CRITERIA
  (`NumberOfCriteria` / `GetCriterion(i)`), and the size window lives on a `PrimarySizeCriterion`
  inside it. Reaching for `rule.MinimumSize` reads as obvious and does not compile — it cost one round
  here. A rule with no size criterion applies to **all** sizes, which is a real configuration.
- **`AnalysisDisplayStyle.FindByName` does not return `InvalidElementId` when the style is absent** — it
  returns an id that does not resolve. Testing the number means naming `IntegerValue` (gone in 2027) or
  `.Value` (absent before it); asking `Document.GetElement(id) == null` is version-proof and also catches
  a style deleted since it was named.
- **A single spatial-field primitive takes roughly a thousand points** before Revit starts dropping them.
  Painting per face stays well under it; a point-cloud version would have to split across primitives.
- **`Application.WriteJournalComment(text, false)`** writes a line into Revit's own journal — an audit
  trail that survives the session, for long batch work.
- **The prelude's helpers are not available to the compile checker.** `ToMm` and `IdValue` exist for
  composed scripts but `verify-fragments-compile.ps1` builds each fragment alone, so a fragment that
  names them fails the gate. Use the arithmetic inline — which is what the rest of the library does.


## Second pass — the full read, after Ajmal asked "everything you took, no missing?"

He was right to ask. The first pass was a **rigorous survey and a partial read**: the API diff was sound,
but only about **1,500 of 57,601 lines** had actually been opened. The method exists because a survey
shows what code CALLS and never what it LEARNED, and substituting a better survey for the read is still
substituting. What the full read then found, in order of how much it mattered:

### 1. A defect in a fragment written an hour earlier — `action-compare-models.cs`

It shipped with a composite match key (category + family + type + Mark, falling back to location) on the
stated reasoning that *"an ElementId is meaningless across two documents"*. **That reasoning is true of
two unrelated models and false for the only case the fragment is for** — a model and an earlier save of
ITSELF. A save-as preserves ElementIds, so the id IS the identity. The mature implementation matches on
ElementId and nothing else, and the composite key was strictly worse: it invents an "added" and a
"removed" every time somebody edits a Mark or nudges an element, which is exactly the noise a change
report must not produce.

**Found by reading, inside 150 lines of a file I had built against blind.** Corrected: `matchOn = "id"`
is now the default, the old behaviour survives as `"key"` for genuinely unrelated models, and the report
says which one it used.

The same read produced two capabilities the fragment now uses, both reached by reflection:

- **`Document.GetChangedElements(episodeGuid)`** (Revit 2023+) — Revit itself returns what was created
  and modified. Absent on 2020.
- **`Element.VersionGuid`** (Revit 2021+) — a per-element stamp; equal on both sides means the element
  did not change at all, so the parameter walk is skipped entirely. On a large model that is the
  difference between a report and a coffee break.

### 2. The missing half of the most-used MEP fragment — a new BUILD

`action-report-connectors.cs` is one of the most-run fragments here, and reading the connector library's
capability list against it showed the shape of what it does: **Radius, Height, Width, Shape, Origin,
Domain, IsConnected, AllRefs, Owner, CoordinateSystem** — all geometry and topology. Every one answers
*"where is it and what is it joined to"*. **None answers "what is it carrying."**

`Demand`, `AssignedFlow`, `AssignedKCoefficient`, `AssignedFixtureUnits`, `AssignedLossCoefficient`,
`AssignedPressureDrop`, `Coefficient` and `EngagementLength` appeared in **no fragment at all** — so
[`action-report-connector-loads.cs`](../scripts/actions/reporting/action-report-connector-loads.cs) was
built. **The failure it catches is the classic one:** Revit calculates system flow and pressure loss from
what is set on the connectors, so a fixture with no Demand or a sprinkler with no K-factor contributes
zero — and the system report comes back clean, fast and completely wrong. The numbers are real; they are
just the wrong ones.

It deliberately splits zeros two ways, because **a zero is not always a fault**: a pipe's own connectors
carry no Demand (the demand belongs to the fixture at the end of the run), so only zeros on load-bearing
categories are flagged. And reading a property the domain does not have **throws** rather than returning
zero, so every read is guarded individually and a blank means "not applicable", never "set to nothing".

Complementary to the parallel session's `action-report-mep-pressure-drop.cs`, not overlapping: **theirs
reads what Revit CALCULATED (the outputs), this reads what is SET (the inputs).** Outputs are only worth
as much as the inputs listed here.

### 3. A finding for our own bridge, from a file that was nearly skipped

The AI agent compiles the model's C# by wrapping it as
`class DynamicScript { public static object Execute(Document doc, UIDocument uiDoc, StringBuilder output) { ...user code... } }`
— **structurally the same design as our bridge**, which independently confirms that "a fragment body
cannot declare a class" is inherent to the approach rather than a quirk of ours.

But its wrapper injects `using` for **`Autodesk.Revit.DB.Architecture`, `.Mechanical`, `.Electrical` and
`.Plumbing`**, and ours does not. That is why fragments here must write
`Autodesk.Revit.DB.Architecture.Room` in full — a constraint recorded in
`action-assign-location-data.cs` as a fact of life. **It is not a fact of life; it is four lines in the
wrapper.** An `mcp-server/` change, not a `scripts/` one, so it is recorded rather than made — but it
would simplify every spatial fragment in the library.

### What remains unread, and why each skip holds

| Unread | Lines | Why it is a defensible skip |
|---|---|---|
| The agent's chat window | 2400 | WPF. The bridge has no UI |
| The test runner's annotations file | 1236 | **243 attribute declarations** — analyser boilerplate, zero Revit content |
| The MEP library's tessellation helper | 1136 | **Zero `Autodesk.Revit` references** — pure geometry maths, not a Revit harvest |
| The comparison add-in's results form | 809 | WinForms |
| The agent's session reporter and tool loop | 2613 | **Zero `Autodesk.Revit` references** between them — LLM plumbing, not Revit |

That is **8,194 lines skipped on evidence** (checked by grepping each for Revit references and attribute
density), against ~1,500 skipped on assumption in the first pass. The difference is the point.

## State at the end

All 13 consistency checks pass. **The 4 new fragments compile on Revit 2020, 2024 and 2027**, and `action-compare-models.cs` was corrected and re-checked on all three after the full read. None has
been run against a real model: the routing report and the model comparison are read-only, and the heatmap
writes analysis data into a view and has a `mode = "clear"` to take it out again. Run the heatmap on a
handful of elements in a 3D view first — in plan it paints the faces pointing up, which for a pipe is
nearly invisible.
