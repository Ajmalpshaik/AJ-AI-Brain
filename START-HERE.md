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

## Route by what the request is

| The request is about… | Go to |
|---|---|
| Querying or changing the live, open Revit model right now — counts, sizes, schedules, view isolation, placing/creating elements | [`skills/ajtools-live-model/SKILL.md`](skills/ajtools-live-model/SKILL.md) |
| Placing HVAC air terminals (count + layout) | [`skills/ajtools-hvac-terminal-layout/SKILL.md`](skills/ajtools-hvac-terminal-layout/SKILL.md) |
| Calculating/updating a room's Space airflow | [`skills/ajtools-hvac-space-airflow/SKILL.md`](skills/ajtools-hvac-space-airflow/SKILL.md) |
| Placing an FCU, drawing/connecting ductwork | [`skills/ajtools-hvac-duct-routing/SKILL.md`](skills/ajtools-hvac-duct-routing/SKILL.md) |
| Checking whether ductwork already built is still fully connected | [`skills/ajtools-mep-connectivity-verify/SKILL.md`](skills/ajtools-mep-connectivity-verify/SKILL.md) |
| Figuring out unknown/ambiguous real MEP connectivity (what connects to what) | [`skills/ajtools-mep-trace/SKILL.md`](skills/ajtools-mep-trace/SKILL.md) |
| Building a brand-new parametric family (.rfa) in the Family Editor | [`skills/ajtools-family-creation/SKILL.md`](skills/ajtools-family-creation/SKILL.md) |
| Saving something reusable, splitting a big file, making a new skill, a whole-session "did we save everything" sweep | [`skills/brain-self-maintain/SKILL.md`](skills/brain-self-maintain/SKILL.md) |
| A technical gotcha, ambiguous term, or reply-format question with no task attached | [`knowledge/INDEX.md`](knowledge/INDEX.md) |
| Writing new AJ AI Bridge C# from scratch | [`scripts/README.md`](scripts/README.md) — compose from existing fragments first |

## This Brain improves itself — a light version of this runs every session, no setup needed

Two habits, always on, regardless of which skill (if any) is handling the actual request:

- **Before starting substantive work**, check whether [`knowledge/INDEX.md`](knowledge/INDEX.md) already
  answers or shapes the request — don't re-derive something already documented.
- **After finishing**, if something new surfaced (a technique, a gotcha, a reusable script, a recurring
  task pattern worth its own skill), save it in exactly the one place it belongs — see
  [`skills/brain-self-maintain/SKILL.md`](skills/brain-self-maintain/SKILL.md) for the routing rules and
  the size/splitting rules. Create-then-report: save it, then tell the user what was saved and why in the
  same reply — don't ask permission first, but always say what happened. Deleting or replacing something
  that already exists still needs the user's explicit OK.
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
