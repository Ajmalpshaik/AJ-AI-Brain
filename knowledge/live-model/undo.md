# Live Model — Undoing a mistake

> Part of the live-model knowledge set. Index: [`README.md`](README.md) — go back there to route to another topic.

## Undoing a mistake — use Revit's real Undo, don't hand-write a delete/fix script
When the user flags that the last live-model action was wrong (they say "mistake", "undo", or "previous"), revert
it with Revit's own native Undo command instead of writing a script to delete/recreate elements. This keeps
the model's real undo history clean and is guaranteed to fully reverse exactly what the last transaction did,
including anything a manual "delete what I just created" script might miss (renamed/modified existing
elements, not just newly-created ones).

```csharp
var uiapp = new UIApplication(Application);
var undoId = RevitCommandId.LookupPostableCommandId(PostableCommand.Undo);
uiapp.PostCommand(undoId);
```

Confirmed working via `run_csharp` — reverted an entire multi-element placement transaction in one call;
verified after with a fresh element count. Not flagged as destructive by the `allowDestructive` gate, since
it isn't Delete/Purge/a file write — it's a normal Revit UI command.

**Also remember**: sometimes the user will undo something themselves in Revit (Ctrl+Z) and just tell you
afterward ("I already undid the previous one") rather than asking you to do it. Treat that as ground truth
about the current model state — re-check with a fresh query rather than assuming your last tool-call
result (e.g. an element count or a placement report from earlier in the conversation) still reflects what's
actually in the model now.

**Only one `PostCommand` per bridge call**: posting `PostableCommand.Undo` twice in the same `run_csharp`
call fails outright ("Revit does not support more than one command are posted"), even though each individual
post works fine on its own. For a live test that needs to reverse several separate transactions (e.g. a
recipe that commits sheet-creation, then viewport-placement, then schedule-placement as 3 separate
transactions), post one Undo, let it finish, then post the next one in its own separate bridge call — never
batch them.

