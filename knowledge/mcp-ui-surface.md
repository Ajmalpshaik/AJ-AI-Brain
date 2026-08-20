# Can a clickable page reach the bridge? — MCP Apps, and a wrong claim corrected

Written 2026-08-14. Kept because the wrong version of this was said out loud in a session, and a fresh
session that repeats it will refuse a job that is actually buildable.

## The claim that was wrong

> *"No widget, no artifact, no web page can ever reach the bridge."*

**That is only true of a chat widget or a published artifact page.** Those are rendered outside our MCP
server and have no route to it, so for them the claim holds. It is **false for a UI served by our own
MCP server** — that case has a supported, documented route.

## The mechanism

**MCP Apps** (`io.modelcontextprotocol/ui`):

1. The server registers a `ui://` **resource** whose body is HTML.
2. A tool points at that resource with `_meta.ui.resourceUri`.
3. The host renders it in a **sandboxed iframe**.
4. The iframe calls tools back over **JSON-RPC via postMessage**.

The click never touches the named pipe directly. It becomes **a tool call the host routes to our local
server**, which already owns the pipe. Because those calls never enter the model's context, an
interactive report costs **no extra tokens** per click — that part is mechanical, not a marketing claim.

## What already exists here, and what does not

**The hard half is done.** The Revit-side plumbing for an interactive report has been in place since
2026-07-22:

| Interaction | Tool it becomes |
|---|---|
| Click a bar → Revit selects those elements | `select_elements` |
| Type in a cell → a parameter is edited | `set_parameter_value` |
| The report itself | `report_parameters` |
| Drag cells down like Excel | pure UI, no Revit call at all |

**What is missing is the UI layer, not the Revit plumbing.** `mcp-server/` runs
`@modelcontextprotocol/sdk` **1.29.0** and currently registers **tools only, no resources**.

## Before promising it, test it

Rendering has been confirmed in Claude Desktop, VS Code Copilot, Goose, Postman and MCPJam.
**Claude Code was not on that list**, and there is an open report of a host negotiating the capability
and then not rendering the iframe.

So: **build one small proof and look at it.** Do not announce that it works.

## Related

- [`../skills/ajtools-visual-report/SKILL.md`](../skills/ajtools-visual-report/SKILL.md) — how charts and
  dashboards are delivered today (chat by default, a published page only on request).
- [`../AGENT-SPEC.md`](../AGENT-SPEC.md) §1.4 — the bridge's one-connection-at-a-time limit.
