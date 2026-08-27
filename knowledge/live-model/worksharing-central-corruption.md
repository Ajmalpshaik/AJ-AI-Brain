# An element that will not delete — stale local, or a corrupt central?

← back to [`README.md`](README.md)

**The symptom.** A dialog that must be cleared before Revit will continue:

> Can't edit the element. It was deleted in the Central Model.
> Workset 'Workset1' : (Deleted element) : id = NNNNNNN

The element is visible on screen, it has real geometry, and it cannot be deleted, moved or edited. It
survives Synchronize, Reload Latest and Relinquish All Mine. First seen 2026-08-27 on a BIM 360
cloud-workshared model, where it had already cost days.

**The trap.** This message reads like "your local is out of date", and the instinct is to fix the local
— sync, reload, fresh local, clear the collaboration cache. On a genuinely corrupt central every one of
those does nothing, because they all re-read the same broken central. Two of them were tried on
2026-08-27 before the right question got asked, and both were wrong.

## The three checks that separate the two causes

Run all three. Individually each one is suggestive; together they are conclusive.

**1. Ask Revit what it thinks the central says.** Read-only, and the whole-model sweep is effectively
free — **10,443 elements in 0.1 s**, measured 2026-08-27, so never sample, always scan everything:

```csharp
var upd = WorksharingUtils.GetModelUpdatesStatus(Document, id);   // DeletedInCentral / UpdatedInCentral / CurrentWithCentral
var ck  = WorksharingUtils.GetCheckoutStatus(Document, id);       // NotOwned / OwnedByCurrentUser / OwnedByOtherUser
```

`DeletedInCentral` + `NotOwned` is the signature. `action-report-element-ownership.cs` already reports
both in bulk. Scan the whole model at the same time and count how many elements are affected: **one
isolated element points at corruption; many point at a genuinely stale local.**

**2. Check how old the local actually is.** The cloud cache is named by GUID, not by model name, so get
the GUID from Revit itself:

```csharp
var mp = Document.GetCloudModelPath();
mp.GetProjectGUID();   // the folder
mp.GetModelGUID();     // the .rvt filename
```

Then read the **birth** time of the cache file, not its modified time (`stat --printf '%w'` in Git Bash):

```
<CollaborationCache>/<account>/<ProjectGUID>/<ModelGUID>.rvt              the local
<CollaborationCache>/<account>/<ProjectGUID>/<ModelGUID>_backup/          its backup
<CollaborationCache>/<account>/<ProjectGUID>/CentralCache/<ModelGUID>.rvt Revit's copy of central
<CollaborationCache>/<account>/<ProjectGUID>/LinkedModels/...             again, if others link it
```

**If the cache was born minutes ago and the element is still there, it came down inside a fresh
download — the central is holding it.** That single fact ends the argument. On 2026-08-27 the cache was
born at 17:11:29 and the check ran at 17:38; nothing local could have been stale.

Note that `CollaborationCache` may be a **symlink** — on Ajmal's PC it redirects from `AppData\Local` to
`D:\RevitUserAppdata\`. Follow it rather than assuming the default path. And that folder holds every
model of the project (30 of them, ~7.6 GB, on that job): **never delete the project folder, only the
files carrying the one model's GUID.**

**3. Ask whether anyone else sees it.** Two users on two machines with two independent locals seeing the
same ghost cannot be a local-side fault. This is the cheapest check of the three and it was the one that
settled it — the user volunteered it, and it overturned a conclusion already given confidently.

## What a corrupt central actually is

The central records both facts at once: *the element exists*, and *the element is deleted*. A sync
recorded the deletion in the status table without removing the element record. Every download carries
both down.

So the element can never be deleted from any local, ever: deleting requires checking it out first, and
the checkout asks the central, which answers "already deleted". **There is no client-side move that
wins.** Stop trying — that is the whole lesson, and days were spent not knowing it.

Corroborating signs seen in the real case: `Document.GetWarnings()` returned **0** — the warning list is
clean because this sits below the layer warnings describe — and exactly 1 element of 10,439 was
affected, with 0 invalid objects. Isolated damage, invisible to every normal check.

## Do not attempt the delete through the bridge

`Document.Delete()` hits the same checkout wall, and on 2026-08-27 it **hung the bridge**: Revit raised
its modal dialog and sat waiting for a click, and the next call came back
*"Another script is still running."* The user had to clear the dialog by hand. A failed API delete costs
him a real interruption, so once `DeletedInCentral` is confirmed, report it instead of trying.

## The fix — NOT VERIFIED HERE

Repairing a corrupt central is a **central-side** job, done by the model's owner or the BIM lead with
everyone out of the model:

1. **Audit** — File → Open, pick the cloud model, tick **Audit** before opening, let it finish, then
   Synchronize with Central immediately. Audit rebuilds the internal element tables, which is the layer
   that is inconsistent.
2. Failing that, **restore the previous version** from BIM 360 version history (loses work done since),
   or **recreate the central** from a detached copy (heavy — breaks links and everyone's locals).

**None of this was proven on 2026-08-27.** The user resolved it another way and the method was not
recorded, so treat step 1 as the reasoned next move, not as a tested recipe. If you run it, come back
and write down what happened.

## Before it gets this far

The neighbours matter and can send you the wrong way. In the real case the ghost was a cable tray tee
whose three trays were still connected, and one of them was borrowed by the user — which produced a
perfectly plausible "you are holding it yourself" theory. He deleted all three trays; the ghost stayed.
**Connected neighbours can explain a stuck element, so check them — but a ghost that survives their
removal is not about them.** Re-read after every change rather than defending the first theory.
