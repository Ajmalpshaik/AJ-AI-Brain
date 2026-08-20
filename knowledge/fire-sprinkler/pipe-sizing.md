# Sizing sprinkler pipe — and first, whether you are allowed to

> Chunk of [`README.md`](README.md). Head layout is [`layout-method.md`](layout-method.md).
> **Routing is not here** — Ajmal's instruction, 2026-08-20: *"not routing, pipe sizing only."*

There are exactly two ways to size sprinkler pipe, and they are not alternatives you pick on preference.

| | **Pipe schedule** | **Hydraulic calculation** |
|---|---|---|
| How | look the size up in a table by how many heads it serves | calculate flow and pressure through every node to a design density |
| Needs | a head count per segment | density, design area, C-factor, elevations, a water supply curve |
| Who | a competent designer with the table | **a licensed fire protection engineer** |
| **In this Brain** | **built** — it is a lookup, and the model holds the head counts | **out of scope, permanently** |

**The pipe schedule method is heavily restricted, and that restriction is the first thing to check** — not the table. Reaching for the table on a job where the method is not permitted produces sizes that look authoritative and are inadmissible.

## The gate — when the pipe schedule method is permitted at all

`[UNCONFIRMED — corroborated across sources, but this is edition-sensitive and it changed materially in 2025. Confirm against your adopted edition before relying on it.]`

- **New installations of 5,000 ft² (465 m²) or less.**
- **Larger than that, only** where the required flows are available at a **minimum 50 psi (3.4 bar) residual pressure at the highest sprinkler**. That is a water-supply fact, not a drawing fact — somebody has to produce a real supply curve.
- **Light and ordinary hazard only.** **Extra hazard cannot use a pipe schedule.**
- Older editions also allowed **additions and modifications to existing pipe-schedule systems**. **The 2025 edition removes that**, along with the allowance for existing extra-hazard pipe-schedule systems. If your project is on 2025, an extension to an old pipe-schedule system is now a hydraulic job.

Read what that means for real work here: **a Qatar project of any normal size is over 465 m² and will not have a guaranteed 50 psi at the top head.** So on most of Ajmal's jobs the honest answer is *"the pipe schedule method does not apply — this needs hydraulic calculation by the fire engineer."*

That sentence is a deliverable. It is worth more than a table of sizes that cannot be used.

## Where the schedule method still earns its place

- Small standalone buildings and fit-outs under 465 m².
- **Sanity-checking somebody else's drawing.** A 1" pipe feeding fifteen heads is wrong under any method, and the schedule catches it in seconds.
- **Early-stage sizing** to get pipe into a model for coordination, clearly marked as provisional, before the hydraulic calculation returns.
- Estimating, take-offs, and clash coordination where an approximate size beats no size.

Every one of those is legitimate **as long as the output says what it is.** The fragment prints the method it used and the gate result on every run.

## The tables

`[UNCONFIRMED — the whole of both tables.]` The retrievable sources gave the method's limits clearly and its numbers not at all, the same wall the beam obstruction table hit. These are the values commonly published for NFPA 13. **Type your edition's real tables over them before use.**

**Light hazard** — maximum sprinklers served by each pipe size:

| Pipe | Max sprinklers |
|---|---|
| 1 in (25 mm) | 2 |
| 1¼ in (32 mm) | 3 |
| 1½ in (40 mm) | 5 |
| 2 in (50 mm) | 10 |
| 2½ in (65 mm) | 30 |
| 3 in (80 mm) | 60 |
| 3½ in (90 mm) | 100 |
| 4 in (100 mm) | no limit in the schedule |

**Ordinary hazard** — same idea, and it diverges from light hazard from 2½ in upward:

| Pipe | Max sprinklers |
|---|---|
| 1 in (25 mm) | 2 |
| 1¼ in (32 mm) | 3 |
| 1½ in (40 mm) | 5 |
| 2 in (50 mm) | 10 |
| 2½ in (65 mm) | 20 |
| 3 in (80 mm) | 40 |
| 3½ in (90 mm) | 65 |
| 4 in (100 mm) | 100 |
| 5 in (125 mm) | 160 |
| 6 in (150 mm) | 275 |
| 8 in (200 mm) | 400 |

Note the shape: **the two tables are identical up to 2 in and separate above it.** Sizing a small branch does not require knowing which class you are in; sizing a main absolutely does.

## The rules that come with the table

- **Branch lines are limited to 8 sprinklers on each side of a cross main** in light and ordinary hazard, with conditions under which that can be increased `[UNCONFIRMED]`. A segment can satisfy the size table and still break this — they are two separate checks.
- **Sprinklers above AND below a ceiling** are both fed by the same branch, so **both count** toward that branch's total. This is the one most often missed, and it is exactly the two-layer case from [`concealed-spaces.md`](concealed-spaces.md): put heads in the ceiling void and every branch below suddenly serves twice what the drawing shows.
- **A looped or gridded system has no "downstream"**, so a count-based schedule cannot be applied to it at all. Those systems must be hydraulically calculated. If the model's pipework forms a loop, that is a finding, not an obstacle to work around.

## What the Revit model can and cannot give you

**Can:** the number of sprinklers downstream of any pipe segment, by walking the connector network — which is exactly the input the schedule table wants. That is why this half is tractable here.

**Cannot:** pressure, flow, elevation losses, C-factor, or the water supply. Nothing in the model knows what is at the far end of the incoming main.

Two modelling cautions carried from the rest of this Brain, both learned the hard way:

- **`Connector.IsConnected` describes intent, not physical reality.** The proven approach clusters by connector-origin proximity instead — see `recipes/trace-mep-circuits.cs`, live-verified.
- **A "size" in the model is a nominal diameter on a Pipe Type.** Whether it matches the real internal bore, and whether the project uses the same nominal series the table assumes, is a project fact to confirm once.

[`scripts/recipes/sprinkler-pipe-schedule-size.cs`](../../scripts/recipes/sprinkler-pipe-schedule-size.cs) does the count-and-look-up, checks the gate first, and reports required against modelled size per segment.

## What is deliberately not here, and will not be

Hydraulic calculation, density and design area, Hazen-Williams friction loss, pressure at the remote head, pump and tank sizing, and water-supply analysis. All of it is licensed design work. This Brain sizes by schedule and says plainly when the schedule does not apply — it does not approximate the other method.

Routing is also out, at Ajmal's own instruction. The fragment sizes pipe that already exists in the model; it does not draw any.
