# Revit warnings during a scripted change — and why the usual fix cannot be used here

Recorded 2026-08-23.

## The problem

Any bulk creation raises Revit's own warnings. Creating a floor per room raises *"Highlighted floors
overlap"* on every room that shares a wall with the last one. Joining, cutting and moving all have their
equivalents. In the Revit UI a person clicks the warning away. **A script has nobody to click it.**

## The fix that every Revit example uses — and that this Brain cannot use

The documented answer is a **failure preprocessor**: a class implementing `IFailuresPreprocessor`,
handed to the transaction, which is called back for each warning and deletes the ones you choose.

**It cannot be written in a fragment.** The bridge wraps every fragment body inside a single method, and
C# does not allow a class declaration inside a method body in this context. `class X : IFailuresPreprocessor`
does not compile here. This was first proved on 2026-08-22 against `IDuplicateTypeNamesHandler` in
[`action-transfer-views-between-documents.cs`](../../scripts/actions/sheets-views/action-transfer-views-between-documents.cs)
and it is the same wall for every callback interface Revit offers — `ISelectionFilter`,
`IFailuresPreprocessor`, `IDuplicateTypeNamesHandler`, `IExportContext`.

**So: any technique whose answer is "implement this interface" is out of reach of a fragment, and needs
a different route.** That is a property of the harness, not of Revit, and it is worth knowing before
spending a round trip discovering it.

## What DOES work — settings, not an interface

`FailureHandlingOptions` is a plain settings object with setters, and it covers most of what the
preprocessor was wanted for:

```csharp
using (var t = new Transaction(Document, "..."))
{
    t.Start();
    var fo = t.GetFailureHandlingOptions();
    fo.SetForcedModalHandling(false);   // do not stop and put a dialog on screen
    fo.SetClearAfterRollback(true);     // do not leave stale warnings behind if this rolls back
    t.SetFailureHandlingOptions(fo);
    // ... the work ...
    t.Commit();
}
```

- `SetForcedModalHandling(false)` is the one that matters. It tells Revit to resolve failures without a
  modal dialog, so a warning that would have blocked simply resolves its default way and the script
  keeps going.
- `SetClearAfterRollback(true)` matters when the work is in a loop: without it a rolled-back attempt can
  leave its warning attached to the document.

## Before wishing for the preprocessor: a generic "swallow everything" handler can DELETE elements

Added 2026-08-23, from reading a mature platform's own failure handler. This is the part that makes the
harness limit less painful than it first looks.

A general-purpose swallower cannot decide what a failure *means*, so it defers to Revit:

- `failure.HasResolutions()` is false and the severity is Warning → the handler just dismisses it
  (`DeleteWarning`). Harmless.
- Otherwise it asks Revit for `GetDefaultResolutionType()` and applies that. **Revit's default wins**,
  and the available resolutions include `DeleteElements`, `DetachElements` and `UnlockConstraints`.

Writers of these handlers keep an ordered list — MoveElements, CreateElements, DetachElements,
FixElements, UnlockConstraints, SkipElements, DeleteElements, QuitEditMode, SetValue, SaveDocument —
described as "least destructive to most". **That ordering is not the safety net it appears to be**,
because the default is checked FIRST and used if it matches, whatever its position in the list. So for
any failure whose Revit default is `DeleteElements`, a "just swallow the warnings" handler deletes
model elements, silently, inside a transaction that then commits.

**So the honest framing is not "the harness stops us doing the right thing".** A blanket swallower is a
loaded gun in any harness. What is actually wanted is nearly always narrower — *ignore this one known
warning during this one bulk operation* — and that is what `SetForcedModalHandling(false)` plus a
per-item try/catch gives you, without ever handing Revit permission to resolve by deleting.

## Per-warning control

`FailureHandlingOptions` does **not** give per-warning control — you cannot say "swallow overlap but
stop on anything else". For that, three things remain, in order of preference:

1. **Avoid the warning.** Usually possible and always better. A floor per room overlaps because rooms
   share a wall centreline; building on the room's finish face instead of its centre boundary removes
   the cause rather than hiding the effect.
2. **One transaction per item**, inside its own try/catch, so a failure loses one element rather than the
   batch — the pattern used in
   [`action-create-from-room-boundaries.cs`](../../scripts/actions/structural-changes/action-create-from-room-boundaries.cs).
3. **Do it in the Revit UI**, where a person can answer. Say so plainly rather than shipping a script
   that half-works.

## Before recording anything as impossible: check whether Revit already ships the implementation

Added 2026-08-23, and it changes the scope of this whole note. The limit here is real but narrower than
it was written: **a fragment cannot DECLARE a class, so it cannot write its own implementation of an
interface. It can still USE one that already exists.** And for some of these interfaces, Autodesk ships
one.

The proven case is family loading. `Document.LoadFamily(path, IFamilyLoadOptions, out family)` needs an
`IFamilyLoadOptions`, which looked out of reach, so [`load-family.cs`](../../scripts/creators/load-family.cs)
recorded that overwriting an existing family "needs an IFamilyLoadOptions implementation" and stopped
there. But:

```csharp
UIDocument.GetRevitUIFamilyLoadOptions()   // static — returns Revit's OWN implementation
```

That is the handler behind File > Load Family. Handing it to `LoadFamily` makes Revit ask the question
in its own dialog, so the reload path is available from a fragment *and* nobody's content gets silently
overwritten — a better outcome than either "impossible" or a handler we wrote ourselves. It is reached
by reflection in that fragment so one source still compiles on every Revit here.

**So the rule is a two-step, not a verdict:**

1. Does Revit ship an implementation of this interface? Look for a `Get…Options()` / `Get…Handler()`
   static, usually on `UIDocument` or `UIApplication`. If yes, the technique is available.
2. Only if there is none is it genuinely out of reach — and then say which interface, so the next
   session does not re-derive it.

The two recorded as out of reach still are: `IFailuresPreprocessor` (Revit ships no general
implementation, and per the section above a blanket one is dangerous anyway) and
`IDuplicateTypeNamesHandler`. `ISelectionFilter` belongs in the same family — but it is moot here,
because a bridge fragment never picks interactively.

## How to recognise this class of problem

If a fragment appears to run, reports success, and the model is unchanged — and the operation is one
Revit normally warns about — suspect a swallowed or auto-resolved failure before suspecting the code.
Read the element back and compare. The rule is the Brain's standing one: **verify by reading the model,
never by trusting the call returned without throwing.**

Related: [`core.md`](core.md) for the transaction rules, and
[`../revit-version-compatibility.md`](../revit-version-compatibility.md) for the other family of
"compiles here, not there" traps.
