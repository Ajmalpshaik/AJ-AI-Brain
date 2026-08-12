# Setup — plugging this Brain into a new project

Three pieces have to come together for this Brain to actually work: **the Brain itself** (this folder —
skills/knowledge/scripts), **an MCP relay** (the small Node.js server in `mcp-server/`), and **a
Revit-side listener** (a running Revit add-in that actually executes the C#). This folder gives you the
first two. The third is the one real prerequisite — read the last section before assuming everything
here "just works."

## 1. Point your AI coding tool at this folder

**Option A — install as a Claude Code plugin (recommended).** This repo is itself an installable
plugin (manifest in `.claude-plugin/`): one install gets all 10 skills auto-loaded on every project on
that machine, plus the bundled MCP relay — no folder copying, no `.mcp.json` editing. In an
interactive Claude Code session on the target machine:

1. `/plugin marketplace add <path to this folder>` — e.g. `D:\Ajmal\AJ AI Brain`, or the private
   GitHub repo URL if that machine's git is already signed in to it.
2. `/plugin install aj-ai-brain@aj-ai-brain`

Two footnotes: the plugin's MCP relay runs `node` from PATH — if the relay shows as failed, run
`npm install` once inside the installed plugin copy's `mcp-server/` folder, and if Node isn't on PATH
at all, register the relay manually instead (step 2 below, full path to `node.exe`). And **don't do
both Option A and step 2's manual `.mcp.json` on the same machine** — same server key twice.

**Option B — manual copy (Claude Code without plugin support, or other tools that load a `.claude/`
folder):**
- **Per-project**: copy this Brain's `skills/`, `knowledge/`, `scripts/`, and `tools/` subfolders into
  that project's own `.claude/` folder (merge, don't overwrite anything already there).
- **Every project on this machine**: copy the same subfolders into your tool's global config folder
  instead (for Claude Code, that's the user-level `.claude/` folder), so it's available everywhere
  without copying per project.

Either way, also put [`START-HERE.md`](START-HERE.md) somewhere your tool reads automatically at the
start of a session (e.g. as that project's own `CLAUDE.md`, or referenced from it) — that's what makes
the "verify, don't trust" / "fresh reads" / self-improving habits apply without being asked for.
(Working *inside this Brain folder itself*, that's already done: this repo's own
[`CLAUDE.md`](CLAUDE.md) imports `START-HERE.md` automatically.)

## 2. Wire up your own MCP bridge

**Skip this whole step if you installed the plugin (Option A above)** — the plugin already registers
the same relay under the same key.

1. `cd mcp-server && npm install` (this folder ships `index.js` + `package.json`, not `node_modules` —
   installing fresh keeps the Brain small and avoids shipping stale dependency binaries).
2. Add an `.mcp.json` at your project root registering it under the **same key the skills/scripts already
   call** — `aj-tools-aj-ai` — so nothing in this Brain needs editing to find it:
   ```json
   {
     "mcpServers": {
       "aj-tools-aj-ai": {
         "command": "node",
         "args": ["mcp-server/index.js"]
       }
     }
   }
   ```
   If Node isn't on your PATH (e.g. a portable/no-admin install), point `command` at the full path to
   `node.exe` instead of the bare `node`.
3. Restart/reconnect your AI tool's MCP connections so it picks up the new server.

## 3. The one real prerequisite — a Revit-side listener

The Node relay in step 2 doesn't talk to Revit directly. It reads a discovery file
(`%APPDATA%\AJTools\ajai-bridge.json`, containing a pipe name + auth token) and connects to a **named
pipe that some running Revit add-in has to host** — that add-in is what actually executes the C# against
the open document and enforces the safety rules (blocking destructive ops unless explicitly allowed,
blocking reflection into its own internals). This Brain does not include that add-in's source — it's a
separate, compiled codebase, not something skills/knowledge/scripts can provide.

Two ways to get it:
- **You already have a Revit add-in that provides this bridge** (e.g. the AJ Tools add-in, if it's
  installed) — just make sure its "Connect AJ AI Bridge" toggle (or equivalent) is switched on in Revit
  before you start. Nothing else to build.
- **You don't have one yet** — you'd need to build or adapt a small add-in that: (a) hosts a local named
  pipe server, (b) writes its pipe name + a token to the discovery file above so the relay can find it,
  (c) on each request, runs the received C# against the currently open `Document`/`UIApplication` and
  returns the result as JSON. This is genuine add-in development work, out of scope for this Brain to
  hand you fully built — but every skill and script here assumes that protocol once it exists.

## 4. Verify it's working

- Native MCP tool, if exposed: `mcp__aj-tools-aj-ai__ping`.
- Fallback: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\invoke-bridge.ps1 -Ping`.

A successful ping means all three pieces are connected. From there, open [`START-HERE.md`](START-HERE.md)
and work normally — it routes to the right skill for whatever you're asking for.
