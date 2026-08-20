# Hazard classification — the input every other number depends on

> Chunk of [`README.md`](README.md). What the classes do to spacing:
> [`../nfpa13-sprinkler-spacing.md`](../nfpa13-sprinkler-spacing.md). Why the NFPA and EN class names do
> not translate: [`nfpa-vs-en12845.md`](nfpa-vs-en12845.md).

Every fragment in this Brain refuses to run without a hazard class, and until now the Brain only carried a
four-row table of typical occupancies. This is the full picture: what the classes are, what defines them,
and **how the decision is actually made** — Ajmal's ask, 2026-08-20.

**Get this wrong and everything downstream is wrong together**, silently and consistently: the area per
head, the spacing, the head count, the pipe sizes. Nothing in the output looks odd, because everything was
computed correctly from the wrong starting point. It is the highest-leverage single input in the whole
subject.

## The NFPA 13 classes

`[UNCONFIRMED — corroborated across several summaries, but confirm the examples and the density/area
figures against your adopted edition.]`

| Class | What defines it | Typical occupancies | Density | Design area |
|---|---|---|---|---|
| **Light Hazard** | low quantity **and** low combustibility; low expected heat release rate | offices, schools, churches, hospitals, hotels and residential, museums, theatres (not stages), library reading rooms | 0.10 gpm/ft² | 1,500 ft² |
| **Ordinary Hazard Group 1** | combustibility **low**, quantity **moderate**, **stockpiles not above 8 ft (2.4 m)** | laundries, restaurant service areas, bakeries, canneries, electronics plants | 0.15 gpm/ft² | 1,500 ft² |
| **Ordinary Hazard Group 2** | combustibility **moderate to high**, quantity **moderate to high** | dry cleaners, car repair, auditorium stages, woodworking, post offices, library stack rooms, machine shops, **car parks** | 0.20 gpm/ft² | 1,500 ft² |
| **Extra Hazard Group 1** | high combustible content, processes with heat/flame but **little or no flammable liquid** | printing (high flash-point inks), plywood manufacture, sawmills, rubber processing | 0.30 gpm/ft² | 2,500 ft² |
| **Extra Hazard Group 2** | as EH1 **plus** moderate to substantial **flammable or combustible liquids** | spray painting, dipping and coating, solvent cleaning, plastics processing | 0.40 gpm/ft² | 2,500 ft² |

**Storage is not on this list on purpose.** Once you are protecting stored goods above the thresholds, you
leave the occupancy-hazard system entirely and enter NFPA 13's storage chapters, with commodity
classification, storage height, rack configuration and their own design curves. See "Storage" below.

The density and area columns are what the class *means* numerically. **Doing anything with them —
calculating flow, pressure, or a remote area — is out of this Brain's scope** and belongs with the fire
protection engineer. They are here so the class is understood, not so it gets calculated.

## The BS EN 12845 classes

Different system, different granularity — **the names do not translate**, which is covered in
[`nfpa-vs-en12845.md`](nfpa-vs-en12845.md) and is the trap worth repeating here.

| Class | Examples | Density | Area of operation |
|---|---|---|---|
| **LH** — Light Hazard | schools, offices, prisons; **no single compartment over 126 m²** | 2.25 mm/min | 84 m² |
| **OH1** | hospitals, hotels, restaurants | 5.0 mm/min | 72 m² |
| **OH2** | **car parks**, museums, dairies, metalworking, assembly | 5.0 mm/min | 144 m² |
| **OH3** | department stores, shopping centres, furniture showrooms, woodworking, printing | 5.0 mm/min | 216 m² |
| **OH4** | chemical plants, paint and varnish production | 5.0 mm/min | 360 m² |
| **HHP1–HHP4** — High Hazard **Process** | flammable liquids, spray application, heavy process risk | per class | per class |
| **HHS** — High Hazard **Storage** | its own rule set, by storage configuration | per class | per class |

`[UNCONFIRMED.]` Note the shape difference: **EN holds the density constant across OH1–OH4 and grows the
area of operation instead**; NFPA moves the density. The LH compartment limit of 126 m² is a real gate —
a light-hazard space bigger than that is not light hazard under EN, regardless of what is in it.

## How the decision is actually made

Three questions, in this order. They are about **contents and process**, never about the room's name.

1. **How much combustible material is in here, and how combustible is it?** Both, together. A room with a
   little of something very combustible and a room with a lot of something barely combustible can land in
   the same class.
2. **How fast would it burn, and how big would the fire get?** The classes are ultimately about expected
   heat release rate and fire growth — that is what the density is buying.
3. **Is anything stacked, and how high?** The single sharpest line in the whole system, below.

### The 8 ft (2.4 m) line

**Ordinary Hazard Group 1 caps stockpiles at 8 ft (2.4 m).** Above that, the space is either OH2 or it has
left occupancy classification altogether for the storage chapters. This one dimension is the most useful
single question to ask on a walk-through, and it is measurable rather than a judgement.

It is also the one most often broken *after handover*. A store designed at OH1 with 2 m of racking becomes
non-compliant the day someone adds a third tier. Worth saying to a client in writing.

### Mixed occupancy — the rule that gets misapplied

A building is rarely one class. `[UNCONFIRMED, and genuinely nuanced — get the engineer's ruling.]`

- Each area is generally designed to **its own** class.
- Where a **small higher-hazard area** sits inside a lower-hazard building, there are allowances: broadly,
  if the higher-hazard area is smaller than the design area for that class, the system may in many cases
  be designed to the lower class. If it is larger, the higher class governs.
- Later editions added a "phantom flow" relaxation for exactly this situation, easing the water-supply
  demand where the higher-hazard area is small.

**What this means practically:** *"the whole floor is ordinary hazard"* is almost always a simplification,
and the plant room, the store and the kitchen inside it are usually not. But **do not use the mixed-
occupancy allowance yourself** — it is a hydraulic and water-supply judgement. Identify the mix, show it,
and let the engineer rule.

### Storage — a different chapter, not a harder class

If the job involves racking, palletised goods, or piled stock above the thresholds, **occupancy hazard
classification stops being the right tool.** NFPA 13's storage chapters bring commodity classification
(Class I–IV, plastics), storage height, rack type, and in-rack sprinklers. That is a different design
exercise and it is out of scope here. Recognising that you have crossed into it is *in* scope, and the
8 ft line is how you notice.

## The one that will catch you on a Qatar job

**Car parks have been reclassified.** NFPA 13 treated parking garages as **Ordinary Hazard Group 1** for
decades. Recent editions moved them to **Ordinary Hazard Group 2** — 0.20 gpm/ft² over 1,500 ft² instead
of 0.15 `[UNCONFIRMED — confirm against your edition, and note this is exactly the kind of change an
edition brings]`. Modern vehicles carry far more plastic, and EV batteries are pushing the question
further still.

Why it matters for Ajmal specifically: car parks are constant on his projects, and **an old habit or an
old office template will still say OH1.** The area-per-head figure does not change between OH1 and OH2
(both 130 ft² / 12.1 m²), so **the layout looks identical and the hydraulic demand is a third higher.**
A spacing check will not catch this. Only the class label will.

EN 12845 also puts car parks at **OH2**, for what it is worth — the two standards happen to agree here,
which is a coincidence of naming, not a mapping.

## On Ajmal's projects

- **QCDD enforces the NFPA suite**, so the NFPA classes are the default — but the project specification
  can call up BS EN 12845, and then the class names change meaning entirely.
- **The class is stated by the fire engineer or the specification.** This Brain applies it and names it on
  every output; it does not decide it. If nobody can say, that is a finding to report, not a gap to fill.
- **Record it per room, not per project.** A floor with an office, a plant room, a store and a car park
  ramp holds three or four classes. See below.

## What the Brain does with it

- Every fragment takes `hazardLabel` and prints it on every line of output. A head count with no class
  named is meaningless, and it is the number that ends up on a drawing.
- [`../../scripts/recipes/sprinkler-set-room-hazard.cs`](../../scripts/recipes/sprinkler-set-room-hazard.cs)
  writes the decided class onto each Room as a project parameter and reads it back, so the decision is
  recorded in the model once instead of re-asked every session — and so a mixed floor stops being
  flattened to one class.
- [`../../scripts/recipes/sprinkler-floor-scope.cs`](../../scripts/recipes/sprinkler-floor-scope.cs) says
  explicitly, on every run, that it applied ONE class to every room, because on a mixed floor that
  assumption is usually wrong and needs to be visible rather than buried.

**What it will never do is decide the class.** That is a licensed judgement about contents, process and
fire growth, and no amount of model geometry substitutes for it.
