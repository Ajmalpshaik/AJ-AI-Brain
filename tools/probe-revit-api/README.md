# probe-revit-api — read any Revit version's real API, without opening Revit

Answers one question: **does this Revit still have that member, and what is its signature now?**

```
cd tools\probe-revit-api
dotnet run -v q -- 2027 Autodesk.Revit.DB.Mechanical.Zone
dotnet run -v q -- 2024 Autodesk.Revit.DB.IndependentTag
```

First argument is a Revit **year** (or a full install folder); the rest are fully-qualified type names.
It prints every public method and property with its parameter types.

## Why it exists

`[Reflection.Assembly]::LoadFrom` on Revit 2027's `RevitAPI.dll` throws `BadImageFormatException`:
Windows PowerShell 5.1 runs on .NET Framework and **cannot load a .NET 10 assembly**. So the usual
one-liner for "what does this class offer" silently stops working on exactly the versions where the API
has changed most — which is when you need it.

Guessing the replacement API instead is the habit this Brain exists to prevent. On 2026-08-20 this tool
settled `create-hvac-zone.cs` in one run: Revit 2027 has **no zone method left** on
`Autodesk.Revit.Creation.Document`, **no `Zone.AddSpaces`**, and a **read-only `Space.Zone`** — so HVAC
zones cannot be created through the 2027 API at all. That is a capability removal, not a rename, and
no amount of reflection dispatch can paper over it. The fragment now reports that in plain words.

## Safety

`MetadataLoadContext` **only reads metadata** — it never executes Revit code. Revit does not need to be
running, nothing is loaded into a live Revit process, and nothing is modified.

## Related

- [`tools/check-scripts.cmd`](../check-scripts.cmd) — compile-checks all fragments against every Revit
  installed. Use that first; use this when it reports a member that has moved and you need the new shape.
- [`knowledge/revit-version-compatibility.md`](../../knowledge/revit-version-compatibility.md) — what
  this library actually uses, and what changed when.
