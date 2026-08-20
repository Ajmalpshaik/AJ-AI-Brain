# AJ AI Brain — start here

This is a portable knowledge package for doing real Revit modeling work through an AI agent connected
live to an open Revit session (an "MCP bridge" — see [`SETUP.md`](SETUP.md) for what that requires).
It holds proven **skills** (task workflows), **knowledge** (techniques and gotchas learned from real
work), and **scripts** (working C# fragments) — so a new session starts smart instead of re-deriving
everything from zero, and gets smarter over time instead of staying static.

**New here?** Read [`SETUP.md`](SETUP.md) once to plug this into a project. Then come back to this file
at the start of any Revit modeling session.

**Want the complete picture in one document instead of routing through this file?** See
[`AGENT-SPEC.md`](AGENT-SPEC.md) — a full, self-contained operating-manual-style specification (tool
reference, workflows, lessons learned, anti-patterns, quick-reference tables, response standards). This
file (`START-HERE.md`) stays the fast, routed entry point; `AGENT-SPEC.md` is the deliberate exception to
the "small routed files" rule — one document meant to be read start-to-finish.

## How to work — read this before doing anything substantive

1. **Verify, don't trust the API/naming at face value.** Revit's own data (element names, tags,
   `Connector.IsConnected`) describes intent, not always physical reality — both have been proven wrong
   in real sessions. When the obvious answer doesn't hold up, find the technique that gets the *real*
   answer (geometry, a second property, walking the model) — see
   [`knowledge/live-model/mep-trace.md`](knowledge/live-model/mep-trace.md) for the technique.
2. **Fresh reads, never recall.** The user edits and undoes things in Revit between messages. Re-query
   before acting on "known" state; read back after changing anything. Never trust your own earlier
   tool-call result in this same conversation as still-current truth.
3. **Every number is a per-request input, never a default.** Clearances, flows, heights, margins —
   confirm fresh and restate before calculating; never reuse a past session's value just because it
   worked before. The user speaks in mm; Revit's internal API is feet — convert explicitly both ways.
4. **Plan → split → execute.** Show a short numbered plan, run one step at a time, check each step's
   real result before starting the next. Never one opaque script that does everything at once.
5. **Confirm before bulk or hard-to-reverse changes** — state what will happen and how many elements,
   and wait for a clear go-ahead. Small, easily-undone changes: just do them and report.
6. **"Mistake" / "undo" / "previous"** → Revit's native Undo command via the bridge, never a hand-written
   delete script. If the user says they already undid it themselves, believe them and re-query.
7. **Reply format** — see [`knowledge/reply-style.md`](knowledge/reply-style.md): a count question gets a
   bare number, a size/breakdown gets a schedule-style table, substantive work closes with a short
   final-report summary. Plain language, no unexplained jargon.
8. **Show the numbers, don't just list them — without being asked.** His standing rule, 2026-08-14:
   *"i need always need visualization... if vishalization needdd it need to come."* Two or more numbers
   that invite comparison → draw them **as a chart inside the chat reply**. A published page (his word:
   an **"artifact"**) is made **only when he asks for one** — never unasked, it just buries the answer
   behind a link. One number stays one number (rule 7 still wins). How:
   [`skills/ajtools-visual-report/SKILL.md`](skills/ajtools-visual-report/SKILL.md). Note his word
   **"visualization" means a chart or dashboard of the model's numbers**, never a 3D render.

## Route by what the request is

| The request is about… | Go to |
|---|---|
| Querying or changing the live, open Revit model right now — counts, sizes, schedules, view isolation, placing/creating elements | [`skills/ajtools-live-model/SKILL.md`](skills/ajtools-live-model/SKILL.md) |
| Placing HVAC air terminals (count + layout) | [`skills/ajtools-hvac-terminal-layout/SKILL.md`](skills/ajtools-hvac-terminal-layout/SKILL.md) |
| Calculating/updating a room's Space airflow | [`skills/ajtools-hvac-space-airflow/SKILL.md`](skills/ajtools-hvac-space-airflow/SKILL.md) |
| Placing an FCU, drawing/connecting ductwork | [`skills/ajtools-hvac-duct-routing/SKILL.md`](skills/ajtools-hvac-duct-routing/SKILL.md) |
| **Fire fighting** — sprinkler head layout, an NFPA spacing check, pendent vs upright vs sidewall, how far below the ceiling or the slab, or what a beam or column does to the heads ("how many sprinklers", "check my sprinkler spacing", "there is a beam in the room") | [`skills/ajtools-fire-sprinkler-layout/SKILL.md`](skills/ajtools-fire-sprinkler-layout/SKILL.md) — its own code rules, NOT the plain coverage job. The full rule set is [`knowledge/fire-sprinkler/README.md`](knowledge/fire-sprinkler/README.md), the tools are the eight `scripts/recipes/sprinkler-*.cs` |
| Laying out fixed-radius devices so a room has no gap — smoke detectors, CCTV, WiFi, lighting ("how many at 3 m coverage, and where") | [`scripts/recipes/generate-room-coverage-layout.cs`](scripts/recipes/generate-room-coverage-layout.cs) — read its header first; it records which mistakes this has already made |
| **"Do the grayout" / "do the grayout for MEP"** — grey the background, bring the services forward, on a view | [`skills/ajtools-mep-grayout/SKILL.md`](skills/ajtools-mep-grayout/SKILL.md) — his own standard, values already settled; don't re-ask them |
| Checking whether ductwork already built is still fully connected | [`skills/ajtools-mep-connectivity-verify/SKILL.md`](skills/ajtools-mep-connectivity-verify/SKILL.md) |
| Figuring out unknown/ambiguous real MEP connectivity (what connects to what) | [`skills/ajtools-mep-trace/SKILL.md`](skills/ajtools-mep-trace/SKILL.md) |
| Building a brand-new parametric family (.rfa) in the Family Editor | [`skills/ajtools-family-creation/SKILL.md`](skills/ajtools-family-creation/SKILL.md) |
| **Showing numbers, not just listing them** — a chart in the reply, or a shareable dashboard page ("make it a dashboard", "give me the graph", "like that artifact") | [`skills/ajtools-visual-report/SKILL.md`](skills/ajtools-visual-report/SKILL.md) — **and it applies unasked**, see rule 8 below |
| Saving something reusable, splitting a big file, making a new skill, a whole-session "did we save everything" sweep | [`skills/brain-self-maintain/SKILL.md`](skills/brain-self-maintain/SKILL.md) |
| A technical gotcha, ambiguous term, or reply-format question with no task attached | [`knowledge/INDEX.md`](knowledge/INDEX.md) |
| Writing new AJ AI Bridge C# from scratch | [`scripts/README.md`](scripts/README.md) — compose from existing fragments first |
| Hearing out loud what the AI is doing — silencing it, changing the voice, or fixing it when it goes quiet | [`tools/voice/README.md`](tools/voice/README.md) — `tools\voice\voice.cmd off` stops it instantly |
| **You don't know which row above applies**, or don't know the word to grep for | `semantic-index\ask-brain-hybrid.cmd "the request, in plain English"` — searches all 342 files by meaning *and* exact words. **Read the top 3–5, not just #1** (measured ~3 in 4 right at #1); weakest on site vocabulary, so try the Revit word too — see [`semantic-index/README.md`](semantic-index/README.md) |

## This Brain improves itself — a light version of this runs every session, no setup needed

These habits are always on, regardless of which skill (if any) is handling the actual request:

- **Before starting substantive work**, check whether [`knowledge/INDEX.md`](knowledge/INDEX.md) already
  answers or shapes the request — don't re-derive something already documented. The fastest way to find
  out is `semantic-index\ask-brain-hybrid.cmd "<the request>"`, which searches every skill, knowledge note
  and fragment at once instead of routing by hand. **Rebuilding is automatic since 2026-08-13** — the
  Stop hook re-indexes at the end of any turn that edited a file. Only edits made *outside* a session
  (a git checkout, a file changed in an editor) still need `semantic-index\index-brain.cmd` by hand.
- **After finishing**, if something new surfaced (a technique, a gotcha, a reusable script, a recurring
  task pattern worth its own skill), save it in exactly the one place it belongs — see
  [`skills/brain-self-maintain/SKILL.md`](skills/brain-self-maintain/SKILL.md) for the routing rules and
  the size/splitting rules. Create-then-report: save it, then tell the user what was saved and why in the
  same reply — don't ask permission first, but always say what happened. Deleting or replacing something
  that already exists still needs the user's explicit OK.
- **Write down the user's own words, not only the conclusion** (the user's rule, 2026-08-10: *"this is
  my normal work and you have to remember the words am using"*). When they name something in their own
  phrasing — a term, an abbreviation, a dictated near-miss, the way they describe a whole job — record it
  in [`knowledge/glossary.md`](knowledge/glossary.md) as *their words → the Revit meaning*, in the same
  turn, **without waiting for it to cause confusion first**. Their sentence is what a future session has
  to route from, and the measured weak spot of the search layer is exactly the site vocabulary that
  appears in no file.
- **The Brain is the only portable memory** (the user's rule, 2026-07-26). An AI assistant's own local
  memory (machine- or account-specific) is a cache, not the record — anything worth remembering (methods
  the user teaches, gotchas, standards, working preferences) must ALSO be written into Brain files
  (`knowledge/`, `scripts/`, `skills/`), because moving to another system means copying this folder only.
  If it exists only in local memory, it will be lost on the next machine.

This is what makes the Brain "day by day stronger" without a scheduled job or extra setup — it's a
standing discipline baked into how every task gets worked, not a separate process running in the
background.

## What this Brain deliberately does NOT cover

Editing the Revit add-in's own compiled source code (the thing that provides the bridge listener on the
Revit side) is a different codebase and a different kind of work — out of scope here. This Brain is about
using the bridge to work on Revit *models*, not building the add-in that provides the bridge.

**The Revit API reference itself** — `revitapidocs.com`, `rvtdocs.com`, the SDK's `.chm` — is out of
the main index too, decided 2026-08-20 when Ajmal asked for all of it to be pulled in. The instinct was
right; the scale is the problem. The API is **~1,700 classes and 30,000+ documented members** against
this Brain's **3,786 chunks**, so indexing it would leave the Brain as roughly **11% of its own index**
and every question would land on a reference page. It is the 604-chunk standards mistake, eight times
over. Two things replace it, and they are better: [`knowledge/revit-api-surface.md`](knowledge/revit-api-surface.md)
lists the **229 types this library actually uses** and names a working fragment for each — because a
signature does not tell you that `UnionWith()` silently drops quick filters, and a proven fragment does.
If the full API is ever genuinely needed it goes in a **separate index the Brain's own search never
touches**, never in the same collection.

**External standards documents** — Ashghal/PWA CAD standards manuals, QCS, NFPA, manufacturer
catalogues — are also out of scope, decided 2026-08-13 in the user's own words: *"we are making a Revit
AJ AI RAG, it's not connected with Ashghal standards or something like that — for that we have another
skill, or we will create one."* Indexing them was built and then reverted the same hour. Three measured
reasons, so nobody has to re-derive them: the PWA manuals are **CAD drafting** rules (layers, title
blocks, drawing numbers), not Revit modelling; they would have added **604 chunks, a 20% increase** to a
search whose measured accuracy is in `semantic-index/score-history.md`, with **no way to measure the damage** until the test set
grows; and **nothing has ever asked for them** — `job-log/` records what is really needed, so wait for
evidence. If one standards rule genuinely matters, write it as a knowledge note **in your own words,
having read it** — higher signal than 600 unchecked chunks, and it cannot look authoritative while being
the superseded version. Standards belong to the `bim-standards-check` skill, not to this Brain's index.
