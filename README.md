# AJ AI Brain

A portable knowledge package for doing real Revit modeling work through an AI agent connected live to an
open Revit session (an "MCP bridge"). It holds proven **skills** (task workflows), **knowledge**
(techniques and gotchas learned from real work), and **scripts** (working C# fragments), so a session
starts smart instead of re-deriving everything from zero — and gets smarter over time instead of staying
static.

Start at [`START-HERE.md`](START-HERE.md) for the operating rules and routing table, or
[`SETUP.md`](SETUP.md) to plug this into a new project. [`AGENT-SPEC.md`](AGENT-SPEC.md) has the complete
picture in one document if you'd rather read start-to-finish.

## Kinds of work this covers

| Skill | What it's for |
|---|---|
| [`ajtools-live-model`](skills/ajtools-live-model/SKILL.md) | Querying or changing the live, open Revit model — counts, sizes, schedules, view isolation, placing/creating elements |
| [`ajtools-hvac-terminal-layout`](skills/ajtools-hvac-terminal-layout/SKILL.md) | Placing HVAC air terminals (count + layout) |
| [`ajtools-hvac-space-airflow`](skills/ajtools-hvac-space-airflow/SKILL.md) | Calculating/updating a room's Space airflow |
| [`ajtools-hvac-duct-routing`](skills/ajtools-hvac-duct-routing/SKILL.md) | Placing an FCU, drawing/connecting ductwork |
| [`ajtools-fire-sprinkler-layout`](skills/ajtools-fire-sprinkler-layout/SKILL.md) | Fire fighting — sprinkler head layout and NFPA 13 spacing checks |
| [`ajtools-mep-connectivity-verify`](skills/ajtools-mep-connectivity-verify/SKILL.md) | Checking whether ductwork already built is still fully connected |
| [`ajtools-mep-trace`](skills/ajtools-mep-trace/SKILL.md) | Figuring out unknown/ambiguous real MEP connectivity (what connects to what) |
| [`ajtools-family-creation`](skills/ajtools-family-creation/SKILL.md) | Building a brand-new parametric family (.rfa) in the Family Editor |
| [`brain-self-maintain`](skills/brain-self-maintain/SKILL.md) | Saving something reusable, splitting a big file, making a new skill, a whole-session save-everything sweep |

## What's in the folder

- `knowledge/` — glossary, reply-style rules, and the `live-model/` topic set (routed via
  [`knowledge/INDEX.md`](knowledge/INDEX.md))
- `scripts/` — reusable C# fragments (filters, actions, creators, context, recipes — see
  [`scripts/README.md`](scripts/README.md))
- `tools/` — `brain-status.mjs` (**one honest answer to "what is the state of this Brain?"** — counts, how much of the library has actually been run against a real model, open items, drift; computed from disk every time so it can't go stale, and wired as a SessionStart hook so a fresh session knows before it acts. `--full`, `--capabilities`, `--json`), `invoke-bridge.ps1` (fallback bridge caller) and `verify-consistency.ps1` (seven drift checks over this whole folder: skill frontmatter, markdown link targets, scripts-README-vs-disk, skill coverage in the entry docs, AGENT-SPEC's fragment counts, text encoding, and fragment `// SOURCE:` cross-references; `verify-consistency.mjs` is the same seven checks in portable Node; `verify-consistency-hook.mjs` wraps it as the auto-run edit hook wired in `.claude/settings.json` — it runs the Node checker because the older `verify-consistency-hook.ps1` fired on Windows only and silently did nothing on Linux/macOS or Claude Code for web, which is exactly where an agent is least likely to run it by hand)
- `mcp-server/` — the Node.js relay that talks to a Revit-side bridge listener (see [`SETUP.md`](SETUP.md) step 2)
- `.claude-plugin/` — makes this whole repo installable as a Claude Code **plugin** (all 9 skills + the MCP relay in one install — see [`SETUP.md`](SETUP.md) step 1, Option A)

## What this deliberately does NOT cover

Editing the Revit add-in's own compiled source code (the thing that hosts the bridge listener on the
Revit side) is a different codebase — out of scope here. This Brain is about using the bridge to work on
Revit *models*, not building the add-in that provides the bridge.

## Private repo

This repo is private and belongs to Ajmal. It is a standalone package, separate from the AJ Tools plugin
source repo.
