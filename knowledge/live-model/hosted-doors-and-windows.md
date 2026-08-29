# Placing doors and windows by script — the host, the facing, and how to check it

Learned 2026-08-25, placing 13 doors into a 14-room corridor layout from one door Ajmal had placed by
hand. Everything below was measured on that job, not reasoned about.

## A door needs the HOSTED overload, and the obvious fragment does not use it

`creators/create-point-based-element.cs` is the fragment that surfaces for "place a family instance", and
its own PURPOSE line says *door, window, piece of equipment, furniture*. It calls:

```csharp
Document.Create.NewFamilyInstance(pt, symbol, level, StructuralType.NonStructural)
```

That is the **un-hosted** overload. A door placed with it is not in the wall — it does not cut an opening
and it is not tied to the wall. For a door or a window you need the wall passed in:

```csharp
Document.Create.NewFamilyInstance(pt, symbol, wall, level, StructuralType.NonStructural)
//                                             ^^^^ the host
```

So **do not reach for `create-point-based-element.cs` for doors or windows** despite what its description
says. It is right for free-standing point families (equipment, furniture, air terminals placed on a
ceiling face is a different case again). Write the hosted call, or extend that fragment.

`FamilySymbol.Activate()` first if `IsActive` is false, then `Document.Regenerate()`, or the placement
throws.

## The facing comes from the WALL, not from which side the room is on

This is the part that costs time. When two rows of rooms share a corridor, both corridor walls run the
same direction, so **every door placed in them comes out facing the same way** — which means one row
opens into its rooms and the other opens into the corridor.

Measured on the job: 7 doors in the corridor's north wall and 7 in the south wall, all placed by the same
loop. The 7 south-wall doors came out correct, the 7 north-wall doors came out backwards. Nothing in the
placement call was different; the rooms are simply on opposite sides.

**So placement is never finished at placement.** Read the facing back, work out which room is actually on
the facing side, and flip the ones that are wrong:

- `actions/move-copy-rotate/action-flip-elements.cs` with `flipFacing = true` — which way it opens
- the same fragment with `flipHand = true` — which side the leaf swings from

They are separate flips and both may be needed. Compose with
`filters/by-identity/filter-by-id-list.cs` and pass only the ids that failed the check.

## Check by ROOM CONTAINMENT, never by coordinate

A symmetric corridor layout will happily confirm a wrong answer if the check matches on X. This has
already produced one false all-clear in this Brain — an FCU verification that matched by X alone reported
18 of 18 correct when 9 were the wrong unit.

The check that actually works: probe a point offset from the door along its facing direction and ask the
document what room is there.

```csharp
var p = new XYZ(loc.X + facing.X*probe, loc.Y + facing.Y*probe, loc.Z + 1.0);
var room = doc.GetRoomAtPoint(p);      // null outside any room
```

`probe` of 900 mm and a `+1.0` ft lift off the floor both matter — at floor level and too close to the
wall the point can land in the wall or outside the room's boundary and come back `null`.

Then assert both directions: the facing side must be the room, and it must NOT be the corridor. Counting
"doors that found a room" alone passes a door that opens into the corridor, because the corridor is a
room too.

Second check worth running: walk every room and confirm exactly one door opens into it. That catches the
room you skipped, which a per-door check never will.

## Ajmal's door placement convention

His hand-placed reference door sat **857 mm from the centreline of the room's right-hand dividing wall**.
With a 914 mm door in a 200 mm wall that is **300 mm clear between the wall face and the door opening** —
a normal door-to-corner gap, not a centred door. Copy the offset from his door rather than centring, and
measure it from the dividing wall, not from the room's location point: Revit puts the room's location
point wherever it likes, and on that job it was 1400 mm off the geometric centre.

Mirror the OFFSET for the opposite row so the two rows read as a mirrored pair — but **do not mirror the
hand**. That was written here as "the correct symmetric result" and Ajmal corrected it the same hour, from
a plan screenshot: *"SEE THIS HANDELS THE BELOW DORS ITS WRONG SIDE AM I RITHG DOOR HANDILE MAKE IT THAT
WALL SIDE"*. **The handle goes against the near wall on every door, both rows** — hand `(1,0)` throughout
when the door sits to the left of its right-hand dividing wall. A door hinged away from the corner swings
across the room's open side, which is wrong regardless of which row it is in. Geometric symmetry is not
the rule; the handle-to-wall relationship is.

That is the general lesson: **a script can satisfy every geometric check and still be wrong to the eye.**
All 14 doors passed "opens into its room" before this was caught. Draw the plan and look at it, or ask.

## Related

- [`element-identity.md`](element-identity.md) — why an element's name or tag is not proof of what it is
- [`../../scripts/README.md`](../../scripts/README.md) — `action-flip-elements.cs`, `filter-by-id-list.cs`
- [`../glossary.md`](../glossary.md) — Ajmal's own words for these jobs
