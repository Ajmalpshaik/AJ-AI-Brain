# A fitting is the size of the BIGGEST pipe on it — and the generic families cannot do that

**Ajmal's rule, 2026-08-23, in his own words:**

> *"all the time what is the bieest size of connect pipe that will be the fittings full size am i right ??"*

He is right. A tee where a 50 mm run meets a 32 mm branch is a **50×50×32 reducing tee**: the body is
full size at the largest connection, and only the outlet reduces. One fitting, no separate reducer.

Use this as a **read-back test after any resize**: for every fitting, compare the size it holds against
the largest pipe touching it. They should match. Where they do not, the model has an equal-bore fitting
with reducers bolted on, which is what a squeezed or lumpy junction looks like on screen.

## Two ways it goes wrong, and they are different problems

### 1. The fitting was born at the old size and never updated (a real fault — rebuild)

Changing a pipe's `RBS_PIPE_DIAMETER_PARAM` on **already-connected** pipework does **not** resize the
fittings. Revit keeps the fitting at its original size and inserts a `Transition` on every leg that no
longer matches.

Measured 2026-08-23 on a 9-head sprinkler tree: 22 pipes drawn at 25 mm (nipples 15 mm), fitted with
8 tees and 5 elbows, then resized to schedule sizes (50 / 32 / 25). Result — **fittings stayed at 25 mm,
the branch-end elbows stayed at 15 mm, and Revit added 28 transitions**, going from 41 elements where 22
would do. The entry bend became `50 pipe → reducer → 25 elbow → reducer → 50 pipe`. It reads as a lump
on screen and it is genuinely wrong.

**Fix: delete the fittings and the pipes and redraw at the final size, so the fittings are BORN correct.**
Patching leaves the junk behind. The rebuild took 41 fittings down to 22 and 28 transitions down to 9.

**So: size the pipe BEFORE fitting it up, never after.** That is the whole lesson.

### 2. The fitting family is single-size (not a fault — content)

Revit's out-of-box `M_Tee - Generic` reports **`Minimum Size` == `Maximum Size`** on a placed instance,
and all three connectors carry one diameter. It **cannot** hold three different sizes, so a reducing tee
is impossible with it however the pipes are drawn — Revit's only legal answer is an equal tee plus a
transition. That combination is real pipework practice, so it is not an error; it is just not what the
rule asks for.

**How to tell the two apart in one read:** an instance whose `Minimum Size` equals its `Maximum Size` is a
single-size family — case 2, load better content. An instance whose size simply disagrees with its
neighbouring pipes while the family *can* span sizes — case 1, rebuild.

**Fix for case 2:** load a reducing-tee family and set it on the Pipe Type under
`RoutingPreferenceManager` → **Junctions**. On a real fire job that is the grooved or threaded sprinkler
fitting family, not the generic one. Check what is actually assigned before blaming the geometry:

```
pipeType.RoutingPreferenceManager.GetNumberOfRules(RoutingPreferenceRuleGroupType.Junctions)
```

## Reading it back

Walk each fitting's `ConnectorManager`, take `max(connector.Radius*2)`, and compare it against the
largest pipe found through `connector.AllRefs`. Equal means correct. On the rebuilt tree that returned
**13 correct, 9 mismatched**, and every one of the 9 was case 2 — the generic tee doing the only thing it
can.

## Related

- [`insulation-follows-host.md`](insulation-follows-host.md) — the other "the model has a second element
  you forgot about" trap
- [`core.md`](core.md) — Revit snapping a diameter to the Pipe Type's allowed list on `Set()`
- [`../fire-sprinkler/pipe-sizing.md`](../fire-sprinkler/pipe-sizing.md) — where these sizes came from,
  and the gate that decides whether they may be issued at all
