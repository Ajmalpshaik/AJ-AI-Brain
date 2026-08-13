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

## Tool-for-tool (added 2026-08-14)

**Nonica does not publish the full 50 names** — only these 7 appear anywhere public:
`get_parameters_from_elementid`, `extract_size_in_MB_of_families`, `get_viewports_placed_on_sheets`,
`get_viewports_and_schedules_on_sheets`, `get_boundary_lines`, `get_document_switched`, and a
set-parameter tool. Everything else is published as categories, so any tool-level comparison is
capability-to-capability, not name-to-name. Their docs say "use Search and Tools in Claude to see the
full list" — **if the connector is ever installed here, dump the real list then and replace this.**

| Capability they advertise | Ours |
|---|---|
| Read parameters | `report_parameters` + `filters/by-property/` |
| Set parameters | `set_parameter_value` + `actions/parameters-naming/` (19) |
| Quantities / counts | `count_elements`, `model_summary`, `action-count-by-group.cs` |
| Schedules | `actions/sheets-views/` (34) |
| Model warnings | `context/context-all-warnings.cs` |
| Family size in MB | `recipes/model-health-audit.cs` |
| Viewports on sheets | `action-place-viewport-on-sheet.cs`, `action-duplicate-sheet.cs` |
| Boundary lines | `action-report-room-boundaries.cs` |
| Move / rotate / copy | `move_elements` + `actions/move-copy-rotate/` (11) |
| Delete | `delete_elements` (`confirm: true` required) |
| Select | `select_elements` |
| Recolor | `set_color`, `action-color-by-group.cs` + 19 colour actions |
| Sheets, views, grids, levels, tags, room elevations | `creators/` (33) + `actions/sheets-views/` (34) |
| Linked documents | `filters/by-relationship/`, 12 link-related fragments |
| ADA / accessibility | **nothing — the one real tool-level gap** |

Ours with no counterpart on their side: `run_csharp` (arbitrary C#), MEP trace and duct connectivity
verification, duct drawing/sizing, NFPA 13 sprinkler layout, .rfa family authoring, the grayout standard,
`search_brain` / `search_graph`.

## MCP Apps — the interactive report, and a wrong claim corrected (2026-08-14)

Nonica announced *"the first interactive Revit report in Claude"*: click a bar and Revit selects those
walls, type in a cell and a parameter is edited, drag cells down like Excel — **and no extra tokens**.

**A claim made in session that day was wrong and must not be repeated:** *"no widget, no artifact, no web
page can ever reach the bridge."* That is true only of a chat widget or a published page, which are not
connected to our MCP server. It is **false for a UI served by our own MCP server**. The mechanism is
**MCP Apps** (`io.modelcontextprotocol/ui`): the server registers a `ui://` resource holding HTML, a tool
points at it with `_meta.ui.resourceUri`, the host renders it in a sandboxed iframe, and the iframe calls
tools back over JSON-RPC via postMessage. The click never reaches the named pipe directly — it becomes a
**tool call the host routes to our local server**, which already owns the pipe. And because those calls
never enter the model's context, the "no extra tokens" claim is real, not marketing.

**The hard half already exists here.** Their three demos map onto tools this relay has had since
2026-07-22: click a bar → `select_elements`; edit a cell → `set_parameter_value`; the report itself →
`report_parameters`. Dragging cells is pure UI, no Revit call. `mcp-server/` runs
`@modelcontextprotocol/sdk` **1.29.0** and currently registers **tools only, no resources** — so what is
missing is the UI layer, not the Revit plumbing.

**The real unknown is host support, so test before promising.** Rendering was confirmed in Claude
Desktop, VS Code Copilot, Goose, Postman and MCPJam; **Claude Code was not on that list**, and there is
an open report of a host negotiating the capability and then not rendering the iframe. Build one small
proof and see, rather than announcing it works.

## Whose approach is better

Nonica's argument is *AI-written Revit code is unreliable, so remove code*. That is correct about raw
code generation and it is not what this Brain does. The third way is **write once, verify against a real
model, mark the status, reuse forever** — which behaves like their fixed menu for the 223 solved
problems and like code generation only for the genuinely new one.

So this approach is better **only for as long as the verification discipline holds.** Drop it and this
becomes exactly the unreliable thing Nonica designed against — their model is the safer default for
anyone not willing to do that work, and for a vendor shipping to thousands of users it is the only
sane choice. The 83% number is the argument; keep earning it.

## Which to use

They are not mutually exclusive; they are different layers, and both are MCP servers Claude can hold at
once. Reach for theirs when the job is generic documentation/QA on any Revit version and being safe
matters more than being exact; reach for this Brain when the job is MEP work done *his* way, or needs
something no fixed tool list contains.
