# AJ AI Bridge — MCP tools index

One file per tool. Read this table, open the one file you need — don't read the whole folder. Every
tool is registered from `../index.js`; nothing here runs on its own.

> **This folder is the Revit tools only.** There is one more registered tool, `search_brain`, which
> lives in `../brain-tools/` because it never touches Revit — see the last section of this file.

## Original tools (flexible, always available)
| Tool | File | Job |
|---|---|---|
| `run_fragment` | [`run-fragment.js`](run-fragment.js) | Run the PROVEN library by NAME with its INPUTS filled in — reach for this before `run_csharp` |
| `run_csharp` | [`run-csharp.js`](run-csharp.js) | Run any C# against the live document — the fallback for anything the native tools and the fragment library don't cover |
| `ping` | [`ping.js`](ping.js) | Check the bridge is connected |
| `model_summary` | [`model-summary.js`](model-summary.js) | Fast count/breakdown for a fixed set of common MEP categories |
| `list_revit_instances` | [`list-revit-instances.js`](list-revit-instances.js) | Show every connected Revit session, by version and open document |
| `use_revit_instance` | [`use-revit-instance.js`](use-revit-instance.js) | Pin this chat to one Revit session, by pid |
| `use_revit_document` | [`use-revit-document.js`](use-revit-document.js) | Pin this chat to one open project inside that Revit, by title |

**About `run_fragment` (added 2026-08-22).** It is the difference between *having* 290 proven fragments
and *running* them. Before it, every scripted job read the `.cs` file, hand-edited its `INPUTS` block and
pasted the result into `run_csharp` — so what actually reached Revit was a freshly retyped copy, and the
fragment's PROVEN mark said nothing about it. This sends the file **byte-identical apart from the
declarations**, so the proof survives the run.

It also moves three whole classes of mistake off Revit and onto this machine, where they cost
milliseconds instead of a round trip: a **fragment name that does not exist** (it answers with the near
misses), an **input name that is a typo** (it answers with the real field names, rather than silently
running on the file's value), and a **composition that cannot compile** — two filters both declaring
`sb`, or two fragments declaring the same input. `preview: true` returns the exact composed C# without
sending it.

What it does **not** do is compile-check the C#; only Revit and `tools/check-scripts.cmd` do that. It
checks the *form*, which is the half that is checkable without Revit. Rules and the whole-library sweep
that guards the rewrite: [`../test/run-fragment.test.js`](../test/run-fragment.test.js).

**About the last three (added 2026-08-20).** More than one Revit can host a bridge now: each session owns a
pipe named by its process id and publishes itself in `%APPDATA%\AJTools\bridges\<pid>.json`. They are
the only tools here that do **not** talk to Revit — they read that folder and set which session the rest
of the tools use.

**With one Revit open nothing changes and neither tool is needed.** With two or more, every other tool
refuses and says so, naming the sessions, until `use_revit_instance` picks one — Ajmal's rule, asked and
answered on 2026-08-20: *ask, don't guess.* An auto-picked session and a session he named are tracked
separately, because they must behave differently when the world changes: opening a second Revit
re-opens the question if the client merely took the only session going, but not if he chose it; and if
the session he chose closes, everything stops rather than sliding onto another project. Rules and their
reasons: [`../test/multi-instance.test.js`](../test/multi-instance.test.js).

**Two halves, two tools.** Picking the Revit is not the same as picking the project, and solving one
does not solve the other. One Revit can hold several projects open, and on the Revit side `Document`
has always meant `ActiveUIDocument.Document` — *the front window*, which moves when Ajmal clicks
another project between two calls of the same job. `use_revit_document` names the project explicitly,
so it stops mattering which window is in front. Same rule as above: a title that is not open is an
**error listing what is open**, never a fall back to the front window. Switching Revit clears the
project pin, because a title open in one Revit means nothing in another. What actually goes on the
wire: [`../test/document-targeting.test.js`](../test/document-targeting.test.js).

**Needs AJ Tools 1.56.0 or newer on the Revit side.** Against an older add-in the field is simply
ignored and everything behaves as it did before — the client omits it entirely when nothing is pinned,
so the payload is unchanged for callers that never use this.

## Native tools (typed, schema-validated — added 2026-07-22)
Each generates the same proven C# pattern as the matching `../../scripts/` fragment, via the shared
generator in [`../shared/element-filter.js`](../shared/element-filter.js).

| Tool | File | Job |
|---|---|---|
| `list_elements` | [`list-elements.js`](list-elements.js) | Real items (Id + Category + Family/Type), not just a count |
| `count_elements` | [`count-elements.js`](count-elements.js) | Bare count, any category |
| `hide_elements` | [`hide-elements.js`](hide-elements.js) | Temp or permanent hide |
| `unhide_elements` | [`unhide-elements.js`](unhide-elements.js) | Reverse a permanent hide |
| `isolate_elements` | [`isolate-elements.js`](isolate-elements.js) | Temporary isolate |
| `reset_isolation` | [`reset-isolation.js`](reset-isolation.js) | Clear temporary hide/isolate |
| `set_color` | [`set-color.js`](set-color.js) | RGB line + solid fill override |
| `reset_graphic_overrides` | [`reset-graphic-overrides.js`](reset-graphic-overrides.js) | Clear overrides |
| `set_transparency` | [`set-transparency.js`](set-transparency.js) | 0-100% surface transparency |
| `select_elements` | [`select-elements.js`](select-elements.js) | Set the active Revit selection |
| `set_parameter_value` | [`set-parameter-value.js`](set-parameter-value.js) | Bulk-set one parameter |
| `report_parameters` | [`report-parameters.js`](report-parameters.js) | Parameter table with Element IDs |
| `move_elements` | [`move-elements.js`](move-elements.js) | Translate by an mm offset |
| `delete_elements` | [`delete-elements.js`](delete-elements.js) | Permanent delete — schema requires `confirm: true` |

## Adding a new native tool
1. Copy the shape of an existing simple one (`hide-elements.js` for an action, `count-elements.js` for
   a query).
2. Import what you need from `../shared/element-filter.js` (`filterFields`, `viewField`,
   `buildElementsClause`, `buildViewClause`, `runGenerated`, `cs`).
3. Export a `register(server)` function that calls `server.tool(...)`.
4. Wire it into `../index.js` (import + call).
5. Add a row to this table.
6. Add it to the two lists in `../test/smoke.test.js` (the `register` import + a `SAMPLE_ARGS` entry),
   then `npm test` — `node --check` alone has already been proven NOT to catch everything (a corrupted
   NUL byte once passed `node --check` clean; see `knowledge/brain-log.md`, 2026-07-22).

## Testing
`npm test` (from this `mcp-server/` folder) runs `test/smoke.test.js` — imports every tool module,
registers it against a fake server, and invokes every handler with representative args. No live Revit
connection is needed or used: every call is expected to reach the "AJ AI Bridge is not connected" error,
proving each handler's own C#-generation code runs to completion first. It does NOT replace live
verification against a real Revit session for actual Revit-API behavior — it only proves the JS side
never throws before reaching the bridge.

## Not a Revit tool: `search_brain`

`../brain-tools/search-brain.js` registers one more tool, **`search_brain`** — ask the AJ AI Brain a
question in plain English and get back the skills, knowledge notes and C# fragments that answer it,
matched by meaning as well as exact words. It reads only, never opens the bridge, and **works with
Revit closed**.

| Input | Meaning |
|---|---|
| `query` | The question in plain English, in the user's own words, site terms and all |
| `top` | How many results (1–20, default 5) |
| `area` | Restrict to `fragment`, `knowledge`, `skill` or `guide`. Omit to search everything |

**Why it is not in this folder.** `tools/brain-status.mjs` counts every `.js` file here and reports the
total as *native tools*, meaning Revit bridge tools. Putting a non-Revit tool in with them would quietly
turn a true number into a false one — the documentation-ahead-of-reality failure this repo keeps having.
Different kind of tool, different folder, both counts stay honest.

**Why it exists.** `semantic-index\ask-brain-hybrid.cmd` is a Windows batch file. On Claude Code for web,
or any Linux/macOS container, it does not fail — it *silently does nothing*. That is exactly how a whole
session of edits went through unchecked on 2026-08-04, when a `.ps1` hook wrapper had the same problem.
A tool call works everywhere Node runs.

**Adding another non-Revit tool** follows the same steps as above with two changes: put it in
`../brain-tools/`, and give it **its own test** in `../test/smoke.test.js` rather than adding it to the
two lists there. Those lists assert exactly 17 registrations and that every handler fails with a bridge
error; a tool that does neither would break a guard worth keeping.

## Not a Revit tool: `search_graph`

`../brain-tools/search-graph.js` registers **`search_graph`** — it asks the Brain's knowledge **graph**
(`graphify-out/graph.json`) rather than its vector index. Reads only; works with Revit closed.

| Input | Meaning |
|---|---|
| `question` | The question (mode `query`), or a node label (mode `explain`) |
| `mode` | `query` (default) walks out from the question's entities · `explain` describes one node · `path` finds the shortest path between `nodeA` and `nodeB` |
| `nodeA` / `nodeB` | Endpoints, mode `path` only |
| `budget` | Token cap, default 1500. Raise it if the output says `TRUNCATED`. |

**It is not a second opinion — it answers a different shape of question.** Measured on
*"how do I stop ducts overlapping the ceiling"*:

| | Top hits |
|---|---|
| `search_brain` (vector) | `filter-by-element-intersection.cs`, `tagging.md`, `brain-log.md` |
| `search_graph` (graph) | `create-ceiling.cs`, **`ray-trace-to-ceiling.cs`**, `hvac-ducts.md` |

`ray-trace-to-ceiling.cs` never appeared in the vector results at any depth, because the graph matched
the *entities* in the question and walked to their neighbours instead of comparing embeddings. Use
`search_graph` for "what depends on X", "what else touches this", "how do these two relate"; use
`search_brain` for "how do I do X".

**Honest limitation: the graph does not rebuild itself.** The semantic index does (Stop hook), but the
graph needs `graphify update .` for code and a subagent pass for markdown — so `search_graph` can be
older than `search_brain` in a way that is invisible from its output. Check `knowledge/brain-log.md`
for the last rebuild before trusting it on something written today.
