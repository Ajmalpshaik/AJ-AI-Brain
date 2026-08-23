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

This does **not** give per-warning control — you cannot say "swallow overlap but stop on anything else".
For that, three things remain, in order of preference:

1. **Avoid the warning.** Usually possible and always better. A floor per room overlaps because rooms
   share a wall centreline; building on the room's finish face instead of its centre boundary removes
   the cause rather than hiding the effect.
2. **One transaction per item**, inside its own try/catch, so a failure loses one element rather than the
   batch — the pattern used in
   [`action-create-from-room-boundaries.cs`](../../scripts/actions/structural-changes/action-create-from-room-boundaries.cs).
3. **Do it in the Revit UI**, where a person can answer. Say so plainly rather than shipping a script
   that half-works.

## How to recognise this class of problem

If a fragment appears to run, reports success, and the model is unchanged — and the operation is one
Revit normally warns about — suspect a swallowed or auto-resolved failure before suspecting the code.
Read the element back and compare. The rule is the Brain's standing one: **verify by reading the model,
never by trusting the call returned without throwing.**

Related: [`core.md`](core.md) for the transaction rules, and
[`../revit-version-compatibility.md`](../revit-version-compatibility.md) for the other family of
"compiles here, not there" traps.
