---
name: ajtools-live-model
description: Query or directly modify the user's live, open Revit model via the AJ AI Bridge (mcp__aj-tools-aj-ai__ping / run_csharp) — counts, sizes, schedules, view isolation/hide, creating levels, placing elements, running takeoffs, and similar direct model work. Use this whenever the user asks a question about what's currently in their model, or asks to change/create something in the model itself, right now — "how many X are there", "what size are they", "create a schedule for X", "hide everything except X", "add N of X on level Y", "create levels up to N". Do NOT use this to edit the Revit add-in's own compiled source code — that's a separate codebase this Brain doesn't cover. This is about the model, not the plugin.
---

# AJ Tools — Live Model Work

This is the skill for the single most common thing the user asks for: "tell me about my model" or "change my
model," done right now via the live AJ AI Bridge connection, not by building or fixing add-in code. If
the user is asking about ducts, VCDs, levels, schedules, views, or any other live Revit content, this is
the skill — editing the Revit add-in's own source files is a completely different kind of work, out of
scope for this Brain.

## AJ Adaptive AI-Local Workflow

Use this skill as a hybrid system, not as a fresh-code generator. AI understands the user's dictated
request, maps it to the right Revit category/parameter, chooses the reusable module shape, and fills the
inputs. Local `scripts/`, `knowledge/`, and the AJ AI Bridge run real Revit API
code against the open model. The AI/local split is flexible: a familiar task may be mostly local reuse,
while a new task may need mostly AI reasoning first.

The loop is always:

```text
request -> route by shape -> compose local modules -> run -> verify -> answer -> improve the library
```

Decision order:

```text
1. Reuse existing local modules when they fit.
2. If nothing fits, do the task normally with the smallest correct AI-written script.
3. After the result is checked, save the reusable part only if the pattern should repeat.
```

Each repeated task should reduce future effort. If a one-off script proves reusable, convert it into a
filter/action/creator/recipe and update the README instead of leaving the knowledge only in chat.

If no local module covers the request yet, AI still does the work: write the smallest correct one-off
AJ AI Bridge script, run it, verify it, answer the user, then decide whether the reusable part should be
saved back into `scripts/`. Do not stop just because the library is missing a module; the missing
module is exactly how the library grows.

## How to work: plan, split, then execute

Same discipline as the other AJ Tools skills — don't jump straight from the request to one opaque script.
For example, "how many VCDs and what size" splits into: Step 1 — recognize VCD as a family within Duct
Accessories, not its own category (check [`glossary.md`](../../knowledge/glossary.md) for terms like this).
Step 2 — collect all Duct Accessory elements. Step 3 — filter to the VCD family. Step 4 — count and group
by size. Step 5 — reply in the right format (see below). A view-changing request like "hide everything
except ducts" splits similarly: confirm the active view is a type that supports isolation → resolve the
category/element ids → apply → confirm what's now visible.

**Most requests here are a filter + one or more actions — build them from
[`scripts/`](../../scripts/README.md) instead of writing one bespoke script.** "Which elements"
(a category, a size, a family, a room, a selection) and "what to do to them" (color, isolate, hide,
select, count, set a parameter) are separate, reusable concerns — see the scripts folder's README for
the full explanation and the user's own worked example (500mm-height ducts → color → isolate → select,
[`examples/color-isolate-select-by-size.cs`](../../scripts/examples/color-isolate-select-by-size.cs)).
Pick the matching filter fragment from `filters/` (or a `creators/` fragment instead, when the elements
the user wants don't exist yet — "add N of X on level Y", "create levels up to N" — creators produce
`elements` the same way filters do, so an action can chain onto them too), pick one or more action
fragments from `actions/`, paste them together in order, fill in each `INPUTS` block, run once. If the
element type changes next time (pipes instead of ducts, a different family), only the filter/creator
fragment needs to change — every action fragment already works on whatever `elements` it's handed. Only
fall back to a genuinely new, one-off script when no filter/creator+action combination covers the
request.

**Important: search/route by request shape, not by element noun.** Do not start by globbing
`scripts/**/*duct*`, `*pipe*`, `*VCD*`, etc. and conclude no reusable fragment exists because the
file names are generic. "How many pipes?" and "how many ducts, what height?" both use
`filter-by-category.cs` plus `action-count-and-report.cs`; only the `targetCategory` and
`preferredParamName` inputs change.

## Before running anything

1. **Use the fast path for ordinary counts and one-parameter breakdowns**: when native
   `mcp__aj-tools-aj-ai__model_summary` is exposed, call it once with the category and optional
   parameter (for example, `ducts` + `Height`). It returns the Revit version and model name with the
   result, so do not make a separate ping call first. Use the normal script route below for complex,
   multi-parameter, geometry, or model-changing work.
2. **Check the bridge is connected when the fast path does not fit**:
   `mcp__aj-tools-aj-ai__ping`. If it fails, Revit is closed or
   the AJ AI pane's Connect AJ AI Bridge toggle is off — tell the user plainly, don't guess at an answer.

   **On the FIRST successful connection of a session, immediately run
   [`scripts/context/context-session-start.cs`](../../scripts/context/context-session-start.cs)** — one
   call, and it tells you everything you would otherwise assume: which Revit **and which API generation
   is live**, the document and whether it is workshared, **what unit the project actually displays**,
   model size, **links that are not loaded**, **worksets that are closed**, design options, phases,
   warnings, the active view. Say the headline back to the user in one line
   (*"Revit 2024, 'Tower-MEP', mm, 3 links loaded, 2 worksets closed"*) so they can correct you before
   any work starts, not after.

   **Why it is not optional:** an unloaded link, a closed workset and an unexamined design option each
   make a query quietly return LESS than the truth, and a project displaying metres rather than
   millimetres makes every figure wrong by a factor of a thousand. **None of them throws an error.** They
   are the confidently-wrong failures this Brain exists to prevent, and they are invisible unless you
   look at the start.
   If the native MCP tool is not exposed in the current agent session, use the checked-in fast helper
   instead of re-reading `mcp-server/index.js` or hand-writing a named-pipe wrapper:
   `powershell -NoProfile -ExecutionPolicy Bypass -File tools\invoke-bridge.ps1 -Ping`.
   For running composed C# in that fallback mode, write the composed script to a temporary `.cs` file and
   call `tools\invoke-bridge.ps1 -CodeFile <path>`. Do not do a broad memory search or bridge-source-code
   inspection for a normal count/size query once this skill, the scripts router, and this helper are
   already available.
3. **Check [`glossary.md`](../../knowledge/glossary.md)** for any ambiguous term in the request (VCD,
   "fitting" vs pipe fitting, "schedule" meaning a real Revit schedule vs. just a chat table, etc.) —
   ask rather than assume if a term could go two ways.
4. **Check [`live-model/README.md`](../../knowledge/live-model/README.md)** for the technical specifics of
   writing AJ AI Bridge scripts — unit conversion, view-isolation API patterns, what's blocked (reflection,
   destructive ops without explicit confirmation), and known dead ends (posting AJ Tools' own ribbon
   commands doesn't work — don't re-attempt that).
5. **Check [`scripts/filters/`](../../scripts/filters/), [`scripts/creators/`](../../scripts/creators/),
   and [`scripts/actions/`](../../scripts/actions/)** for the fragments that already cover this
   job's "which elements" (existing or newly created) and "what to do to them" — compose them rather
   than writing fresh (see the How-to-work note above). Start from the scripts README/router table, not a
   file-name search for today's element noun; generic modules will not be named `duct` or `pipe`. Check
   `scripts/recipes/` too for anything bespoke enough (real ordering/geometry dependencies between
   steps) that it doesn't fit the filter/creator+action shape at all.

## While running

- Compose from `filters/` + `actions/` following the patterns in `live-model/README.md`, rather than
  rediscovering them.
- For anything bulk or hard to reverse, run the filter fragment alone first to see the real count (see
  the scripts README's "explorer first" section) before appending the action(s) and running for real —
  for something small and easily undone (isolating a view, adding a handful of elements), just run the
  composed script directly and report what happened.
- If the request is genuinely ambiguous in a way that changes the outcome (which category "mechanical
  accessories" means, which specific element(s) to target, exact placement location for new elements),
  ask — briefly, with a sensible default offered — rather than guessing and having to redo it.

## Reply format

Check [`reply-style.md`](../../knowledge/reply-style.md) before answering — the user has specific, previously
corrected preferences here (e.g. a plain count question gets a bare number, one line; a size/breakdown
question gets a schedule-style table, not an inline list). Getting the format right the first time matters
more here than almost anywhere else in these skills, since this is the most frequent kind of request.

**A request narrowed to one specific value ("the 300x300 VCDs", "the ones on Level 2") is not a
count/breakdown question — it's asking for the actual items.** List them with their Element ID (compose
the matching filter with `action-report-parameters.cs`, not `action-count-and-report.cs`) — see the
"specific/narrowed value" rule in `reply-style.md`. The IDs are what let the user's next request ("select
those", "move them", "what's their length") act on this exact set without re-filtering.

## After finishing

If you hit a new technical gotcha (an API quirk, a compile error, something that didn't behave as
expected), add it to [`live-model/README.md`](../../knowledge/live-model/README.md). If a new ambiguous term
came up, add it to [`glossary.md`](../../knowledge/glossary.md). If the user corrected the reply format,
update [`reply-style.md`](../../knowledge/reply-style.md). If a genuinely new *kind* of filter, creator,
or action came up (not just a new INPUTS value on an existing one), save it as its own fragment in
`scripts/filters/`, `scripts/creators/`, or `scripts/actions/` and add it to the
README table — following the naming pattern (`filter-by-<what>.cs`, `create-<what>.cs`,
`action-<verb>-<what>.cs`). Same rule as the other skills: each fact goes in exactly one file — don't
duplicate across them.
