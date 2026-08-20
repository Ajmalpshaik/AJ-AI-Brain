# Which spaces get heads at all — the question before the layout

> Chunk of [`README.md`](README.md). Once a space needs heads, the method is
> [`layout-method.md`](layout-method.md). Which standard decides these answers:
> [`nfpa-vs-en12845.md`](nfpa-vs-en12845.md).

Every other chunk in this folder assumes you already know which room you are laying out. On a plain
architectural plan you do not. **This is the first question, and getting it wrong is more expensive than
any spacing error** — a room with no heads that needed them is a life-safety failure, and a room full of
heads that never needed them is money and coordination spent for nothing.

## The default is: everything gets sprinklers

Start from *"the whole floor is protected"* and take spaces out only where a rule lets you. Never the
other way round.

And the sentence that kills most site arguments before they start, which the standard says almost in
these words: **sprinklers shall not be omitted from a room merely because it is damp, because it is of
fire-rated construction, or because it contains electrical equipment.** `[UNCONFIRMED wording, but this
rule is stated consistently across sources.]`

That single line covers the three excuses heard most often on site. "It's an electrical room" is not, on
its own, a reason.

## Where omission is actually permitted

`[Every row UNCONFIRMED — search snippets only, 2026-08-20. These are also heavily edition- and
AHJ-dependent, more so than the spacing rules. Confirm each one before using it to delete a head.]`

| Space | The allowance, roughly | Watch for |
|---|---|---|
| **Small closets** | smallest dimension **≤ 3 ft (914 mm)** *and* floor area **≤ 24 ft² (2.2 m²)**, with noncombustible or limited-combustible surfaces | all three conditions, not any one |
| **Bathrooms in dwelling/sleeping units** | **≤ 55 ft² (5.1 m²)**, in Group R, noncombustible or limited-combustible walls and ceilings including behind the shower or tub, 15-minute thermal barrier | this is a *residential* allowance; it does not travel to an office toilet |
| **Elevator machine rooms, machinery and control spaces, traction hoistways** | permitted where the specific conditions in the standard are met | the conditions are the whole rule; "it's a lift room" is not the rule |
| **Stair shafts of noncombustible construction** | heads only **at the top and under the first accessible landing** — not a full layout in the shaft | it is a reduction, not an omission |
| **Concealed spaces / ceiling voids** | its own rule set, and the two standards disagree | [`concealed-spaces.md`](concealed-spaces.md) |
| **Spaces protected by an approved automatic detection system** | permitted in some codes, as a trade | this is a **building-code** trade (IBC/IFC route), not an NFPA 13 layout decision. It is above this Brain's pay grade — flag it, do not apply it |

**None of these is a checkbox.** Each carries conditions on construction and finish, and the AHJ can
refuse any of them. On Ajmal's projects that is QCDD, and QCDD's own General Fire Safety Requirements sit
on top.

## The practical rule for a whole-floor pass

When sweeping an architectural plan, sort every space into three buckets, not two:

1. **Needs heads** — the default, and most of the plan.
2. **Reduced or special treatment** — stairs, shafts, voids, canopies. Not "no heads", *different* heads.
3. **Ask** — anything matching an omission rule above. Never auto-omit; produce the list and let a
   competent person rule on it.

[`scripts/recipes/sprinkler-floor-scope.cs`](../../scripts/recipes/sprinkler-floor-scope.cs) does exactly
that sort across a level, by room name and area, and **puts everything it is unsure about in bucket 3
rather than quietly dropping it.** A room silently omitted is the failure mode this whole file exists to
prevent, so the fragment is built to over-report rather than under-report.

## Temperature rating — the other per-space decision

Not a spacing question, but it is decided per space at the same moment, and it belongs on the head
schedule. Choosing it wrong means a head that opens late, or one that opens on a hot day with no fire.

| Classification | Rating | Max ambient ceiling temperature | Where |
|---|---|---|---|
| **Ordinary** | 135–170 °F (57–77 °C) | **100 °F (38 °C)** | the normal case — offices, rooms, corridors |
| **Intermediate** | 175–225 °F (79–107 °C) | higher | under skylights, in attics, plant and mechanical rooms, near kitchen equipment |
| **High** | 250–300 °F (121–149 °C) | higher again | close to real heat sources |

`[UNCONFIRMED — ratings corroborated across sources; the ambient limits are the ones to confirm.]`

The rule of thumb behind the table: the rating must sit a safe margin — commonly quoted as about 70 °F
(≈ 39 °C) — above the **maximum expected ambient at ceiling level**, not above room temperature. A plant
room soffit, a glazed atrium in Doha, or a ceiling void with uninsulated hot pipework can all sit far
above the room below.

**Clearances from heat sources** are a real, measurable rule and they belong in the obstruction pass:
for a non-LED light fitting, commonly quoted as **6 in (152 mm)** minimum for an ordinary-temperature
head at 0–250 W and **12 in (305 mm)** at 250–499 W, halving to 3 in and 6 in if the head is
intermediate-rated `[UNCONFIRMED]`. Measured in a straight line from the nearest edge of the heat source
to the nearest edge of the sprinkler.

Qatar is the reason to take this seriously rather than defaulting everything to ordinary: a rooftop plant
room, a car park soffit or a glazed space can run well past 38 °C ambient for months. **Ask what the
design ambient is at ceiling level** in any space that is not air-conditioned, and record the answer.

## What this Brain will and will not decide

- It **will** sweep a floor, classify every room, apply the area rule, and tell you what it is unsure
  about.
- It **will not** decide that a space is exempt. Every candidate goes on a list for a person.
- It **will not** apply the building-code detection trade — that is IBC/IFC territory and a different
  conversation with the AHJ.
- Hazard class is still an input per space, and on a mixed floor it genuinely varies room to room. A
  plant room and an office on the same level are not the same class.
