---
name: ajtools-mep-trace
description: Trace which equipment/pipe/duct actually connects to which, physically through the model, when Revit's own connectivity (or the tag/naming convention) can't be trusted — and optionally color-code the result. Use this whenever the user asks things like "which outdoor unit does this indoor unit actually connect to", "trace this pipe to where it ends", "does X really connect to Y", "color the pipes going to each unit differently", or any request to verify real physical MEP connectivity rather than assuming it from names or tags. Do NOT use this for simple counts/sizes/schedules with no connectivity question involved — that's ajtools-live-model. Do NOT use this to check whether HVAC ductwork this project already built (FCU/main duct/branches/terminals) is still fully connected end-to-end after the fact — that's ajtools-mep-connectivity-verify, a narrower and cheaper check for a known, already-built system rather than figuring out unknown physical wiring. This is specifically for "what connects to what, physically" when it's genuinely unknown/ambiguous.
---

# AJ Tools — MEP Connectivity Trace

The lesson that led to this skill: CRAC indoor units in this project are tagged in a way that *looks*
like it tells you which outdoor condensers they connect to (`CAC001A` looks like it should pair with
`ACU001A1`/`ACU001A2`) — but the real wiring is cross-connected (`CAC001A` actually connects to
`ACU001B1`/`ACU001B2`). Naming conventions describe intent or a labeling scheme; they don't prove physical
connectivity. This skill exists to verify the real thing instead of reporting a plausible guess as fact.

## Before running anything

1. **Ping first**: `mcp__aj-tools-aj-ai__ping`. If Revit isn't connected, say so plainly — a trace
   needs the live model, there's no static fallback.
2. **Check [`glossary.md`](../../knowledge/glossary.md)** for the system-name mapping and for any pairing
   already traced and recorded (e.g. the CRAC A↔B pattern) — don't re-trace what's already confirmed.
3. **Check [`live-model/mep-trace.md`](../../knowledge/live-model/mep-trace.md)** for the bulk-clustering trace
   method and the color-coding pattern before writing new C#.
4. **Start from [`scripts/recipes/trace-mep-circuits.cs`](../../scripts/recipes/trace-mep-circuits.cs)**
   (its own filtering step follows the same pattern as
   [`filters/by-relationship/filter-by-system-name.cs`](../../scripts/filters/by-relationship/filter-by-system-name.cs) — matches one
   specific System instance's own name, not the System Type/classification; kept inline here since the
   clustering logic right after it is specific to this recipe) rather than writing this fresh — update
   its system-name filter and tolerance in INPUTS before running.

## How to work: plan, split, then execute

**The pipe/system type is always a variable input, never hardcoded.** The user will name a different system
almost every time ("refrigerant", "CDP", "water supply", ...) — check
[`glossary.md`](../../knowledge/glossary.md) for the mapping from their word to the actual Revit
system-type name(s) to filter on before starting.

1. **Collect the whole filtered set first, not just one path.** The preferred method here: gather every
   pipe/fitting matching the requested system type in one pass, rather than
   tracing a single named starting unit outward. The full bulk-clustering algorithm — group by touching
   connector ends, find each group's open ends, match those to the nearest equipment — is documented in
   [`live-model/mep-trace.md`](../../knowledge/live-model/mep-trace.md); read it rather than re-deriving it.
2. **Try the fast path first**: check whether Revit's own `MEPSystem.Name` grouping is already a real
   physical grouping (sample `Connector.IsConnected` between a few same-system elements — it's usually
   `true` internally, only the last hop to equipment tends to be `false`). If so, group by system name
   directly instead of manually walking connectors.
3. **If that doesn't hold** (connectors aren't reliably `true` within a system, as in this project's
   refrigerant piping originally), fall back to the **geometric trace** — matching connector *positions*
   directly, ignoring the `IsConnected` flag. Full method (tolerance, fallback-on-dead-end, hop cap) is in
   `live-model/mep-trace.md`.
4. **Report what you actually found**, even if it contradicts what the naming would suggest — that
   contradiction is often the useful finding, not a mistake to paper over. Check `glossary.md` first in
   case this exact pairing was already traced and recorded (e.g. the CRAC A↔B pattern) — no need to
   re-trace something already confirmed.
5. **If asked to color-code the result**: apply per-element `OverrideGraphicSettings` (line color AND
   solid surface fill — see `live-model/mep-trace.md` for the exact pattern; the user specifically wants both, not
   just line color), one distinct color per circuit/group, so multiple runs stay visually distinguishable
   at once.

## Reply format

Check [`reply-style.md`](../../knowledge/reply-style.md). For a trace result, a compact table (one row per
circuit: system, from-equipment, to-equipment, verified how) is usually right; a bare one-line answer if
the user asked a yes/no "does X connect to Y" question. Always say whether each pairing was geometrically
verified or resolved from an already-documented pattern.

## After finishing

- If the trace reveals a real, verified connectivity pattern (not just for this one instance but something
  that will hold for similar equipment), add it to [`glossary.md`](../../knowledge/glossary.md) — that's
  what turned a one-off finding into something the next session already knows.
- If the trace technique itself needed a tweak (different tolerance, a new category to include, a new kind
  of dead-end), update the method in [`live-model/mep-trace.md`](../../knowledge/live-model/mep-trace.md) **and**
  `trace-mep-circuits.cs` in place, rather than letting the improvement live only in this conversation.
- **Never assume a pattern found for one system automatically holds for every other similar system** —
  confirm each one by tracing, at least the first time. Once several instances confirm the same pattern
  (as happened here — all 4 CRAC systems showed the same A↔B cross-connection), it's reasonable to say so
  explicitly and treat it as an established, documented fact going forward — that's exactly what
  `glossary.md` is for.
