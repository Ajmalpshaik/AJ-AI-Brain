# Setup — plugging this Brain into a new project

Three pieces have to come together for this Brain to actually work: **the Brain itself** (this folder —
skills/knowledge/scripts), **an MCP relay** (the small Node.js server in `mcp-server/`), and **a
Revit-side listener** (a running Revit add-in that actually executes the C#). This folder gives you the
first two. The third is the one real prerequisite — read the last section before assuming everything
here "just works."

## 1. Point your AI coding tool at this folder

**Option A — install as a Claude Code plugin, straight from GitHub (recommended).** This repo is
itself an installable plugin (manifest in `.claude-plugin/`). One install gets every skill auto-loaded
on every project on that machine, the bundled MCP relay, the live hooks, and the `/brain-setup`
command — no folder copying, no `.mcp.json` editing, and nothing pointing at anyone else's paths. In
an interactive Claude Code session on the target machine:

1. `/plugin marketplace add Ajmalpshaik/AJ-AI-Brain` — or a local path such as
   `D:\Ajmal\AJ AI Brain` if the folder was handed over on a drive rather than pulled from GitHub.
   A private repo needs that machine's git to be signed in to it already.
2. `/plugin install aj-ai-brain@aj-ai-brain`
3. Restart Claude Code, then run **`/brain-setup`** once.

Step 3 is the one that makes it work on *that* machine. The install brings the knowledge; it cannot
bring the search index, the Python environment or the relay's npm packages, because those are
gitignored on purpose — an index built on someone else's machine is stale on arrival. `/brain-setup`
builds all three, takes a few minutes, and is safe to re-run. You will not have to remember it: a
SessionStart hook checks on every session and stays silent unless something is genuinely missing, in
which case it names the missing piece and prints that one command.

Two footnotes: the plugin's MCP relay runs `node` from PATH, so if the relay shows as failed the usual
cause is Node not being on PATH — register the relay manually instead (step 2 below, full path to
`node.exe`). And **don't do both Option A and step 2's manual `.mcp.json` on the same machine** — same
server key twice.

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

## 5. Handing this folder to someone else — the fresh-machine checklist

Sections 1–4 assume the machinery already exists somewhere. This section is the other case: a colleague
is handed the folder and starts from nothing.

**What he receives, and what he does not.** The Brain's own content — `skills/`, `knowledge/`,
`scripts/`, the relay source and the root docs — is tracked and travels intact. The layers *derived*
from that content do not: the Python environment, the vector index, the knowledge graph and the
Obsidian vault are all gitignored on purpose, each with its reason written in [`.gitignore`](.gitignore).
The short version is that a stale index is worse than no index, so they are rebuilt on the machine that
will use them rather than shipped.

So he starts with all of the knowledge and none of the machinery. Rebuilding it is about an hour, most
of which is unattended.

> **Copying the folder rather than cloning it does not skip this.** A Python virtual environment records
> the absolute path it was created at, so `semantic-index/venv` copied from another machine is dead
> weight — it has to be recreated either way.

### Step A — what the machine needs that this folder cannot provide

| Needs | What it is for | Confirm with |
|---|---|---|
| Node.js on PATH | the MCP relay, and every hook in `.claude/settings.json` | `node --version` |
| Python 3.11 or newer | the plain-English search | `python --version` |
| **A Revit-side bridge listener** | **the one real blocker — see section 3 above** | a successful ping (step D) |
| The graphify skill, in his own user-level skills folder | only to rebuild the knowledge graph and the vault; the search and every fragment work without it | — |

Everything except the Revit listener is a normal install. The listener is not — read section 3 before
promising anyone this will work end to end.

### Step B — install the Brain

Use the plugin route in **section 1, Option A**. On a second machine that choice matters more than it
does on the first: the plugin registers the relay with a bare `node` and its own folder path, so it is
portable. This repo's own `.mcp.json`, by contrast, records the **absolute path of the `node.exe` on the
machine that wrote it** — on any other machine that path does not exist and the bridge never starts. If
he registers the relay by hand instead of installing the plugin, that one line is the thing to change:
point it at his own Node, or simply at `node` when Node is on PATH.

### Step C — build the layers that did not travel: one command

```
/brain-setup
```

That is the whole step. It installs the relay's npm dependencies, creates the Python environment,
installs the search dependencies and builds the search index from scratch — in that order, stopping at
the first failure rather than reporting success over a half-built environment. A few minutes, safe to
re-run, and it resumes from wherever it stopped. It also redirects pip's temp folder inside
`semantic-index/` by itself, which on a company-managed PC is the difference between a clean install
and antivirus quarantining half the packages (the reason is at the top of
[`semantic-index/requirements.txt`](semantic-index/requirements.txt)).

**He will not have to remember to run it.** A SessionStart hook checks every session and says nothing
at all when the machinery is present; when it is missing it names the missing piece and prints that
command.

Outside a Claude session, the same thing runs directly:

```
node tools/brain-setup.mjs
```

and `node tools/brain-setup.mjs --check` reports what is missing without changing anything.

Then the index itself. The embedding model ships inside the search dependency and fetches itself on this
first run, so this step needs internet once and then never again:

```
semantic-index\index-brain.cmd --full
```

Expect a few minutes, and a final `DONE (full rebuild)` line stating the file and chunk counts. It
verifies its own count before recording success, so if it says done, it is done.

### Step C2 — turn auto-update on, once

This is the only other thing he ever has to do, and it takes one keystroke. Claude Code enables
auto-update for its own marketplaces but **leaves it off for third-party ones by default**, so without
this he stays on the version he first installed:

1. `/plugin`
2. **Marketplaces** tab → `aj-ai-brain`
3. **Enable auto-update**

From then on Claude Code refreshes the marketplace in the background shortly after each session starts
and pulls new plugin versions down on its own.

**Nothing else needs doing when an update lands.** The files change, and the next session's
`brain-setup --check` notices the index is older than the files and starts an incremental rebuild in
the background — a couple of hundred milliseconds to fire, no waiting, and the search picks up the new
fragments within the minute. That is the whole reason the check re-runs every session rather than only
on first install: **a plugin update replaces files and nothing else**, and an index describing
yesterday's library would hide exactly the newest work.

For the knowledge graph and the Obsidian vault, follow
[`skills/brain-update-layers/SKILL.md`](skills/brain-update-layers/SKILL.md) — it is the maintained
procedure for all three derived layers and it says which steps are no-ops. The graph's document side is
the slow part, because it needs an AI session rather than a script.

### Step D — prove it works, in this order

Each of these tests something the one before it does not:

1. `node tools/brain-status.mjs` — reads the true state from disk. The **Derived layers** block should
   name today for each layer, with nothing newer.
2. `node tools/verify-consistency.mjs` — all checks pass, no drift.
3. `semantic-index\ask-brain-hybrid.cmd "how do I stop ducts overlapping the ceiling"` — must return in
   under 4 seconds, print no `STALE INDEX` banner, and put genuinely relevant files at the top. A fast
   empty answer is still a failure.
4. `mcp__aj-tools-aj-ai__ping`, or the fallback in section 4 — this is the only one that proves Revit is
   reachable.

Steps 1–3 can all pass on a machine with no Revit at all. That is worth knowing rather than discovering
later: they prove the Brain, step 4 proves the bridge.

### What he will not inherit

`job-log/` holds the record of which fragments get used in real work, and it is per-machine. His starts
empty, so the most-used-fragments hint on each message shows nothing until he has done some jobs. That is
correct behaviour — it is his usage it should be learning, not someone else's.
