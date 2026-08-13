# Tool landscape — NonicaTab A.I. Connector vs this Brain's AJ AI Bridge

Written 2026-08-14, because the user asked *"our tool and nocia tab ai connector — what is the
different, what we don't have from them, what is the best"*. Facts here came from Nonica's own site and
docs on that date; **prices and tool counts move — re-check before quoting them at anyone.**

## Who they are

**NonicaTab**, by Nonica (`nonica.io`, Autodesk Authorized Developer since 2021, listed as an Anthropic
official integration since 2025). It is a Revit ribbon add-in — "the plugin of plugins" — that bundles a
customisable toolbar, ~35 ready-made Revit tools, Dynamo-script buttons, and, since 2025, an **A.I.
Connector**: an MCP server built into the add-in.

| | NonicaTab A.I. Connector |
|---|---|
| Cost | FREE €0 · PRO €85/member/year · Enterprise pay-per-session |
| Revit | Toolbar 2020–2027 · A.I. Connector 2022–2027 |
| AI apps | Claude Desktop, ChatGPT Work, Copilot in VS Code, Cursor — auto-configured |
| Tools | ~37 read-only on FREE · 50+ read **and** write on PRO |
| Execution | **Predefined micro-tools only — deliberately no code generation** |
| Parallel | Multi-agent mode supported; docs say disable editing tools when running agents in parallel |

Their stated reason for no code generation: AI-written Revit code is unreliable, so the AI calls
pre-built functions and reacts to Revit's response instead.

## The one real difference

Theirs is a **fixed menu** — safe, supported, installable in minutes, and hard-limited to what is on the
menu. Ours is an **open door plus a proven library** — `run_csharp` executes any Revit API C# against the
live document, and the library exists so that door is rarely walked through blind (270 fragments, 83%
verified against a real model; see `tools/brain-status.mjs`). Everything else follows from that.

Consequence worth stating plainly: **anything not in their 50 tools cannot be done at all**, while for
us it is a new fragment. Conversely their 50 tools ship tested by a vendor, and our unproven ones are
marked unproven for a reason.

## What they have that we do not

1. **A product** — Autodesk App Store installer, docs site, issue tracker, updates, support, a company.
   Ours needs the AJ Tools add-in, Node, and an MCP registration (`SETUP.md`).
2. **Declared multi-version support** (2022–2027). This Brain's testing baseline is Revit 2020.
3. **Multi-agent / parallel sessions.** The bridge here is one connection at a time (`AGENT-SPEC.md` §1.4).
4. **Several AI apps, auto-configured** — ChatGPT Work, Copilot, Cursor, Claude Desktop.
5. **The non-AI half**: shareable team toolbars, 35 ready tools, Dynamo buttons.
6. **ADA / accessibility checking** — searched 2026-08-14, this Brain has no accessibility fragment. The
   nearest thing is `actions/reporting/action-report-coverage.cs`. Genuine gap if it is ever wanted.

Checked and **not** gaps — we already have these: model warnings (`context/context-all-warnings.cs`),
whole-model health incl. file size (`recipes/model-health-audit.cs`), room boundary geometry
(`actions/reporting/action-report-room-boundaries.cs`), viewports on sheets, schedules, purge.

## What we have that they do not

- **Arbitrary C#** — no ceiling at 50 tools.
- **Whole jobs, not calls**: 10 skills (duct routing, NFPA 13 sprinkler layout, MEP trace, grayout,
  family authoring…) that carry method, order, and verification, versus a tool list the AI must sequence
  itself every time.
- **Memory of this office**: the user's own vocabulary, the MEP_ line standard, the grayout values, the
  duct-sizing method, gotchas that cost a real session to learn.
- **Honest provenance** — every fragment carries proven / unproven / blocked, and `brain-status.mjs`
  recomputes it from disk. A vendor tool list tells you nothing about what has actually been run here.
- **Self-improvement** — what today's session learns is in the folder tomorrow.

## Which to use

They are not mutually exclusive; they are different layers, and both are MCP servers Claude can hold at
once. Reach for theirs when the job is generic documentation/QA on any Revit version and being safe
matters more than being exact; reach for this Brain when the job is MEP work done *his* way, or needs
something no fixed tool list contains.
