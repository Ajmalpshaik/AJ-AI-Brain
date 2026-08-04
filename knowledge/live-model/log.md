# Live Model — Log

> Part of the live-model knowledge set. Index: [`README.md`](README.md) — go back there to route to another topic.

## Log

Dated, one-line entries for what was actually done on the live model — a session summary, not a
technique (techniques belong in the topic file they apply to, e.g. `mep-trace.md`, `hvac-ducts.md`).
Add a line here whenever a session is worth being able to answer "what did we do on that date?" later.

- 2026-07-26 — Teaching session: the user taught the connection method (check connectors → domain/size →
  real direction → draw). Exercise 1: drew 1000mm stubs from all 40 drawable connectors of 8 FCU-01s
  (16 ducts + 24 pipes, 0 failures — 4 units were mirrored, the direction check caught it). Exercise 2:
  the user kept 1 FCU + 6 terminals; built the full branched supply system (main + 6 tap/branch/drop
  sets), then per the user's live correction extended the main 500mm past the last branch and placed an
  end cap (M_Rectangular Endcap). Everything verified connected, duct warnings clear. Saved as
  `scripts/recipes/connect-equipment-to-air-terminals.cs` + hvac-ducts.md § The user's connection method.
- 2026-07-26 (later) — Sizing lesson by worked example: the user manually split the trunk into 3 segments
  (after each tap column, none after the last) and sized everything square at max 5 m/s / 25mm steps
  (1410→550, 940→450, 470→325, 235→225). Verified his splits: 6/6 terminals BFS-trace to the FCU.
  Rule saved in hvac-duct-sizing.md § Why the trunk gets split.
- 2026-07-26 (later-2) — Applied the taught method solo: the user added 4 FCUs + 24 terminals (two FCUs
  mirrored, supply facing +X — the connector-direction check caught it). Built all 4 systems (main + 6
  tap/branch/drop each + end caps) and split each trunk at 2 stations. Discovery: ConnectTo-only joints
  let Revit silently re-merge one split; replaced all 8 joints with real Union fittings
  (NewUnionFitting). Final verify: 30/30 terminals traced, 10 unions, 0 open ends, every trunk
  1410→940→470 L/s. Model ready for the sizing stage.
- 2026-07-26 (later-3) — First scripted duct sizing, FCU 921552 only (user checking): all 15 ducts sized
  from their own Flow by the 5 m/s square rule (550/450/325 trunk, 225 branches). Discovery: on resize,
  Revit swaps each trunk union into two back-to-back transitions with facing connectors left OPEN (BFS
  fell to 2/6) — joined each facing pair, terminal/equipment transitions auto-inserted fine. Final:
  6/6 traced, 0 open connectors. Full behavior in hvac-duct-sizing.md.
- 2026-07-26 (later-4) — the user rejected the scripted sizing (screenshot: warnings at the FCU) and taught
  the correct way: select everything → Duct/Pipe Sizing → set value → OK. Undid the scripted sizing,
  ran the dialog on FCU 921552 (Velocity 5.0 m/s) — same sizes, 0 warnings. Then researched a code path
  per the user's ask: no API, and the journal proves the button is a toolbar PushButton with no command
  ID, so nothing to PostCommand. A second code attempt with automatic joint repair fragmented the system
  (37→20 elements) and was undone. Settled workflow: script builds/splits/verifies, ONE dialog run sizes
  everything — selected all 3 remaining systems (111 elements) and sized them in a single OK. Final: all
  4 systems 44 elements, 6/6 terminals, 0 warnings, 0 open ends, 550/450/325 + 225.
