# Zero to finish — what a complete sprinkler design needs, and where this Brain actually is

> Chunk of [`README.md`](README.md). Written 2026-08-20 answering Ajmal's question directly:
> *"if I receive a plain architectural plan, can I design sprinklers from zero to finish?"*

**Short answer: not yet, and this file says exactly where the holes are** — so the next session picks up
from a list instead of re-deriving one, and so nobody promises a client something the tools cannot do.

His sequencing, and it is the right one: **finish the sprinkler side first, then pipe sizing.** Pipe
sizing is deliberately gated at the bottom of this file.

## The scope levels he asked for

All three route through the same chain; only the entry point changes.

| He says | Entry point | State |
|---|---|---|
| *"the whole plan"* | [`../../scripts/recipes/sprinkler-floor-scope.cs`](../../scripts/recipes/sprinkler-floor-scope.cs) → then per room | **built 2026-08-20**, not yet run |
| *"room one"* | [`../../scripts/recipes/sprinkler-obstruction-survey.cs`](../../scripts/recipes/sprinkler-obstruction-survey.cs) → the chain | built, not yet run |
| *"give me another layout"* | [`../../scripts/recipes/sprinkler-layout-options.cs`](../../scripts/recipes/sprinkler-layout-options.cs) | **built 2026-08-20**, not yet run |

## The full chain, and what is honestly done

| # | Step | Fragment | State |
|---|---|---|---|
| 0 | Which spaces need heads at all | `sprinkler-floor-scope.cs` | **built**, classifier is name-based — see its limits |
| 1 | What is in the room — ceiling, void, beams, columns, bay | `sprinkler-obstruction-survey.cs` | built |
| 2 | Construction type call (unobstructed / obstructed) | — | **a human call, by design** |
| 3 | Head count floor from the area rule | in the grid + scope fragments | built |
| 4 | The grid, derived from the limits | `sprinkler-nfpa-grid.cs` | built |
| 4b | **Several** compliant layouts to choose from | `sprinkler-layout-options.cs` | **built** |
| 5 | Heads vs beams, columns, wide services | `sprinkler-obstruction-check.cs` | built, **beam table UNCONFIRMED** |
| 6 | Move what fails, re-check | `sprinkler-adjust-for-obstructions.cs` | built |
| 7 | The Z, read from what is really above | `sprinkler-deflector-height.cs` | built |
| 8 | Sidewall where pipe cannot get above | `sprinkler-sidewall-layout.cs` | built |
| 9 | Place the families | `sprinkler-place-heads.cs` | built |
| 10 | Audit what really got placed | `sprinkler-compliance-audit.cs` | built |

**Every one of those is unproven against a real model.** That is the single biggest gap in this list and
no amount of new code closes it — it needs one session with Revit open. Until then each fragment carries
its own STATUS block naming which of its calls are proven elsewhere in the library.

## What is genuinely still missing

Ordered by what blocks "zero to finish" hardest.

1. **A live run of the whole chain, once.** Three of the fragments need only a placed Room, which exists.
   Three need a beam and a column, which the API can build. One needs a ceiling. One needs a sprinkler
   family. See the open-items list in [`../brain-log.md`](../brain-log.md).
2. **Hazard class per room.** Today it is one input applied to a whole run. A real floor is mixed, and
   nothing in the model stores the class. Needs a project parameter on Rooms, written once and read by
   every fragment — otherwise every run re-asks and every answer is un-auditable.
3. **The head schedule.** Type, K-factor, temperature rating, response, finish, and the hazard class and
   standard the layout was computed for. `sprinkler-place-heads.cs` places geometry; it does not yet write
   the data that makes a placed head a designed head. The temperature-rating logic to drive it is written
   ([`where-sprinklers-are-required.md`](where-sprinklers-are-required.md)) but not coded.
4. **Multi-room execution in one call.** The scope pass lists the rooms; steps 1–10 still run per room by
   hand. A driver that walks the list and stops on the first room needing a decision would turn a floor
   from a day into an hour — but it must stop and ask, not push through.
5. **Drawing output.** Tags, head schedules on a sheet, the layout as a drawing rather than a report.
   `recipes/tag-elements-in-active-view.cs` exists and is proven; nothing wires it to sprinklers yet.
6. **Sloped and stepped ceilings.** The rules are written; the fragments assume one flat plane per room
   and will mis-read anything else. `sprinkler-deflector-height.cs` flags a room with two ceiling levels
   rather than handling it, which is honest but not finished.
7. **The unconfirmed numbers.** The beam obstruction table above all — it is an input seeded with
   commonly published values and it prints a warning on every run until someone types the adopted
   edition's table in.

## What is deliberately NOT in scope, and will not be

Saying this plainly so it never becomes an implied promise:

- **Hydraulic calculations** — density, remote area, pressure, flow. A licensed fire engineer's work.
- **Hazard classification itself.** The Brain applies the class it is given and names it on every line.
- **Pump, tank and water-supply selection.**
- **Any statement that a design is "compliant".** The Brain reports what was checked, the measured value,
  and the limit it was checked against. QCDD and the project specification sit on top.

## Pipe sizing — gated, on purpose

Ajmal's own sequencing, 2026-08-20: *"after all finishing the sprinkler, you can continue the pipe
sizing. Not routing, pipe sizing only."*

**The gate is item 1 above: the chain has to run against a real model once.** Sizing pipe for a layout
whose head positions have never been verified would build a second unproven layer on an unproven first
one, and the failure would surface at the bottom where it is hardest to diagnose.

When that gate is passed, pipe sizing is its own subject with its own chunk — and worth noting now, while
it is fresh: it is **not** the same job as HVAC duct sizing, the Brain's existing sizing work. Sprinkler
pipe is sized either by a **pipe schedule** (a lookup table: this many heads on this size, by hazard
class) or **hydraulically** (calculated to a density). The schedule method is tractable here and is
genuinely useful. The hydraulic method is not, and it belongs with the same licensed engineer as the rest
of the hydraulics.

That distinction is the first thing to establish when the gate opens — not after.
