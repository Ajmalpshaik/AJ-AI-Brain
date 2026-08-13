# AJ AI Bridge — MCP tools index

One file per tool. Read this table, open the one file you need — don't read the whole folder. Every
tool is registered from `../index.js`; nothing here runs on its own.

> **This folder is the Revit tools only.** There is one more registered tool, `search_brain`, which
> lives in `../brain-tools/` because it never touches Revit — see the last section of this file.

## Original tools (flexible, always available)
| Tool | File | Job |
|---|---|---|
| `run_csharp` | [`run-csharp.js`](run-csharp.js) | Run any C# against the live document — the fallback for anything the native tools below don't cover |
| `ping` | [`ping.js`](ping.js) | Check the bridge is connected |
| `model_summary` | [`model-summary.js`](model-summary.js) | Fast count/breakdown for a fixed set of common MEP categories |

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
