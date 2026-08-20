# Changing a loadable family's category — what survives, what is lost

Proven live on 2026-08-20, converting `TCM_FAL_T001_FreshAirIntakeLouvre` from **Duct Accessories** to
**Air Terminals** on `4355-BHVD-3D-60P00-BL006A.rvt` (Revit 2020.2.9). Three instances,
HWL-00082/83/84. Snapshot before and after:
[`job-log/snapshots/2026-08-20-HWL-fresh-air-louvre-params.md`](../../job-log/snapshots/2026-08-20-HWL-fresh-air-louvre-params.md).

## The instances are converted in place — they are NOT deleted

The expectation going in was that Revit would delete the instances, because a Duct Accessory cannot
remain one once the family is an Air Terminal. **That did not happen.** All three kept their original
element IDs (8092750, 8161681, 8403332), their placement, and their duct connections. Reload, don't
re-place.

Say this before the change, not after: the safe assumption is still to snapshot first, because the
cost of being wrong is retyping dozens of project parameters by hand.

## What survives

**61 of 64 filled instance parameters survived**, including every project/site parameter that would
have been painful to retype — the whole `LV00`–`LV03` WBS set, `MM_*` drawing references, Client Tag,
Item Tag, Equipment Tag, CWA, Discipline, Sub-Discipline, WBS, Port Area Code, Main Item Tag,
STI_C_Position, Comments, Mark.

**Family parameters always survive.** `V_Airflow` here is defined inside the `.rfa`, not in the
project — check with `Document.ParameterBindings`: if the name is not in the bindings iterator, it is
a family parameter and it travels with the family regardless of category.

## What is lost — two separate causes, only one is a real loss

**1. Built-ins that belong to the old category.** Seven parameters simply stopped existing:
`Free Size`, `Overall Size`, `Insulation Thickness`, `Lining Thickness`, `Pressure Drop`,
`Loss Method`, `Use Annotation Scale`. These are Duct-Accessory built-ins; an Air Terminal has no
such parameter. All were read-only or auto-calculated. **Not a loss — nothing to restore.**

**2. A project parameter not bound to the new category.** `ID_Level Name` was bound to exactly one
category — Duct Accessories. The moment the element became an Air Terminal the parameter, and its
value, were gone. **This is the real casualty and the one to check for in advance.**

> **The check to run BEFORE any category change**, so nothing is a surprise: walk
> `Document.ParameterBindings`, and for every project parameter the element currently carries a value
> for, test whether its `ElementBinding.Categories` includes the *destination* category. Anything bound
> to the old category but not the new one will be silently dropped. Adding the destination category to
> that project parameter's binding first prevents the loss entirely.

## The category change does not fix a connector-level modelling error

The louvre family assigns its airflow to **one specific connector**. On all three instances the
connector carrying the 2247 m³/h was the *free* one, and the connector actually joined to ductwork
read **0**. Converting the category did not change this, and it should not be expected to — connector
flow assignment lives inside the family.

The visible symptom in the project: each louvre ends up on its own single-element duct system
reporting **0 m³/h**, while the louvre itself claims 2247. Chasing the system flow is the wrong end of
the problem; fix which connector carries the flow, inside the family.

## Side effects worth checking after the change

- **Ducts may be replaced, not kept.** The three ducts recorded before the change
  (8092770, 8161689, 8403337) were deleted and replaced by new elements with new IDs. Record duct IDs
  in the snapshot so this is detectable.
- **Duct systems fragment.** New single-element systems appeared (FA 3, FA 7, FA 8) alongside the
  originals (FA 2, FA 4, FA 5). Re-check the system list afterwards; a system left holding nothing is
  worth purging.
- **An instance can shift.** HWL-00082 moved 53.3 mm in Y; the other two did not move at all. Compare
  `LocationPoint` against the snapshot rather than assuming placement held.
- **The built-in `Flow` parameter appears.** Air Terminals have `RBS_DUCT_FLOW_PARAM`; Duct Accessories
  do not. Here the family drove it straight from `V_Airflow`, so it read 2247 m³/h immediately — but
  that is the family being well built, not automatic behaviour. On a family with no such link it
  arrives empty.

## Confirmed a second time, same day, different model

`4355-BHVD-3D-60P00-BL003A` held **10 louvres**: 2 sand trap already correct in Air Terminals
(`TCM_STL_T001_J004/J005`), 3 fresh air in Duct Accessories, 5 exhaust in Mechanical Equipment. Ajmal
converted all 8 wrong ones — **from both source categories** — and every element again kept its ID.

Two things this second run adds:

- **Mechanical Equipment → Air Terminals behaves the same as Duct Accessories → Air Terminals.** The
  in-place conversion is not specific to one source category.
- **A model can be partly right already.** BL003A had 2 of 10 correctly categorised from the start,
  where BL006A had 0 of 7. Always report "already correct" separately from "wrong" — colouring or
  listing all louvres as problems when a fifth of them are fine wastes the user's attention and makes
  the finding look careless.

## How to verify, quickly

Read back the original element IDs first — if they resolve, the instances were converted in place and
the whole snapshot can be compared parameter by parameter. Compare against the pre-change snapshot on
three axes: parameter still exists, parameter still has its value, and placement/connections unchanged.
