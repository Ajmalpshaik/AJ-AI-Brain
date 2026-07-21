---
name: ajtools-mep-connectivity-verify
description: Verify that HVAC ductwork already built by ajtools-hvac-duct-routing is actually connected end-to-end — trace every air terminal's full connector chain (riser → elbow → branch → takeoff → main trunk → FCU) and report exactly where any silent break is, rather than trusting a terminal's own IsConnected flag. Use whenever the user says things like "check the duct connection", "verify the air terminal is connected", "is everything connected", "check if anything is broken", "trace the terminal to FCU", "chek the conection", or any request to confirm the ductwork is genuinely wired up end-to-end rather than just visually placed. This is read-only/diagnostic by default — it finds and reports breaks, it does NOT fix them (that's ajtools-hvac-duct-routing's job, once told what's broken). Do NOT use this for tracing UNKNOWN/ambiguous existing MEP connectivity where naming or tags can't be trusted (e.g. "which outdoor unit does this indoor unit really connect to") — that's ajtools-mep-trace, which figures out physical wiring nobody documented; this skill instead checks connections THIS project already built on purpose, for a break that crept in afterward. Do NOT use this for placing or drawing new ductwork — that's ajtools-hvac-duct-routing.
---

# AJ Tools — MEP Connectivity Verify

This is the skill for confirming HVAC ductwork is *actually* connected end-to-end, not just visually
sitting in the right place. It exists because of a real, repeated failure mode found during the
2026-07-08/09 duct-routing session: a terminal's own connector reported `IsConnected == true` while its
real path back to the FCU was silently broken somewhere further along the chain — most dramatically when
a curve-editing operation silently deleted a takeoff fitting and orphaned the entire branch downstream of
it, and the terminal's local connector never showed anything wrong. The only way that was actually found
was tracing connector-by-connector via `Connector.AllRefs` until hitting an open end — this skill turns
that manual debugging technique into a reusable, on-demand check instead of redoing it from scratch every
time something looks off.

It's a companion to, but separate from, [`ajtools-hvac-duct-routing`](../ajtools-hvac-duct-routing/SKILL.md)
(which builds the ductwork this skill checks) and [`ajtools-mep-trace`](../ajtools-mep-trace/SKILL.md)
(which is for figuring out *unknown* physical wiring when naming/tags can't be trusted — refrigerant
pairing, "which unit really connects to which" — a genuinely different problem from checking a connection
this project already built on purpose for a break that crept in since).

## How to work: plan, split, then execute

1. **Confirm scope** — a specific room, a list of rooms, or the whole model. If the user doesn't say, ask
   rather than assuming "just the room we're currently talking about" — users often move between rooms
   quickly across a session, and a stale assumption here wastes the whole check.
2. **Collect the terminals in scope** — filter explicitly by system type (family name contains "Supply" or
   "Return", or the terminal's own `DuctSystemType`) the same way `ajtools-hvac-duct-routing` does; don't
   rely on proximity to guess which system a terminal belongs to.
3. **Trace each terminal's chain outward, connector by connector**, using the same technique already
   documented for recovering an orphaned branch in
   [`live-model/mep-trace.md`](../../knowledge/live-model/mep-trace.md) (the trunk-slicing section) — from the
   terminal's own connector, follow `Connector.AllRefs` to whatever it's attached to, then that element's
   *other* connector(s), and so on, until either:
   - it reaches a connector owned by a Mechanical Equipment element (the FCU) — **fully connected**, or
   - it reaches a connector where `IsConnected == false` — **broken here**, record the exact element (Id,
     type, position) where the chain stops, or
   - it loops or dead-ends without ever reaching an FCU or an open connector (shouldn't normally happen,
     but guard against infinite loops with a reasonable step limit and report it as a separate anomaly if
     it occurs.
   **Never stop at the terminal's own `IsConnected` value** — that only reflects the *local* link to
   whatever is immediately attached, not whether the full path is intact. This is the core lesson from the
   session that created this skill.
4. **Report clearly, per terminal, in scope order**: which are fully connected end-to-end, and for any
   that aren't, exactly which element the chain breaks at and where it sits in the model (so the user or a
   follow-up `ajtools-hvac-duct-routing` fix can go straight to it, not re-search for it).
5. **Don't fix anything found broken** — report it, and if the user wants it fixed, that's
   `ajtools-hvac-duct-routing`'s job (e.g. re-tapping an orphaned branch via a fresh `NewTakeoffFitting`,
   already documented there and in `live-model/mep-trace.md`).

## Before running anything

1. **Ping first**: `mcp__aj-tools-aj-ai__ping`. If Revit isn't connected, say so plainly.
2. **Check [`glossary.md`](../../knowledge/glossary.md)** for any ambiguous term in the request.
3. **Check [`live-model/mep-trace.md`](../../knowledge/live-model/mep-trace.md)** for the exact connector-tracing
   pattern and related gotchas — the `IsPointInRoom`-fails-on-Z-mismatch issue (matching an element sitting
   above a room's normal height range back to its room needs a test point at the room's own Z, not the
   element's real Z) applies here too when scoping terminals/ducts by room.
4. **Start from [`scripts/recipes/verify-duct-connectivity.cs`](../../scripts/recipes/verify-duct-connectivity.cs)**
   rather than writing this fresh — set the room scope (or leave it whole-model) in INPUTS before running.

## While running

- This is read-only — no transaction needed, nothing to confirm before running, unlike the build/place
  skills. Run it freely whenever asked.
- If a break is found, don't silently try to fix it as part of this check — report it, then let the user
  decide whether to fix it now (hand off to `ajtools-hvac-duct-routing`) or just note it for later.

## Reply format

Check [`reply-style.md`](../../knowledge/reply-style.md). A compact per-terminal or per-room table (or a
bare count if everything's fine and the user only asked a yes/no question) is usually right — don't dump a
full connector-by-connector trace unless something's actually broken and the detail is needed to act on it.

## After finishing

If a new technical gotcha comes up while tracing (a new fitting type with unexpected connector behavior,
a new kind of break), add it to `live-model/mep-trace.md`. If the trace logic itself needed a fix, update
`verify-duct-connectivity.cs` in place. Same rule as every other AJ Tools skill: one fact,
one file, no duplication.
