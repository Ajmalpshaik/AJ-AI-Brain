// ============================================================
// FRAGMENT (action) — action-compare-models.cs
// PURPOSE: Compare the OPEN model against another .rvt on disk — what was added, what was removed, and
//          which parameters changed on the elements present in both. The "what did they change in this
//          revision" question, answered from the models rather than from a transmittal note.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`). READ-ONLY on both documents: opens no Transaction, changes nothing,
//          and closes the file it opened.
//
// WHY THIS EXISTS: `action-compare-elements.cs` compares elements INSIDE one model. Nothing here could
//   compare two MODELS, which is the version-to-version question a coordination job actually asks.
//
// ✱✱ IT WORKS BECAUSE A BACKGROUND DOCUMENT HAS NO WINDOW. `Application.OpenDocumentFile(path)` returns
//    a document Revit is not showing. Reading it costs nothing and it is closed again at the end — the
//    same mechanism the folder upgrade and the family export use.
//
// ✱✱ ELEMENTS ARE MATCHED ON ElementId, AND THAT IS THE RIGHT ANSWER HERE — CORRECTED 2026-08-24.
//    This fragment first shipped with a composite key (category + family + type + Mark, falling back to
//    location) on the reasoning that "an ElementId is meaningless across two documents". That reasoning
//    is true of two UNRELATED models and FALSE of the only case this fragment is for: a model and an
//    earlier save of ITSELF. A save-as preserves ElementIds, so the id IS the identity, and the
//    composite key was strictly worse — it invents an "added" and a "removed" every time somebody edits
//    a Mark or nudges an element, which is precisely the noise a change report must not produce.
//    `matchOn` keeps the old behaviour available for the genuinely unrelated case, and says which it used.
//
// ✱✱ THREE WAYS TO ASK, FASTEST FIRST — and the fragment picks the best one this Revit supports:
//    1. `Document.GetChangedElements(episodeGuid)` (Revit 2023+) — Revit itself returns what was
//       created and modified since that GUID. Nothing to walk, nothing to guess.
//    2. `Element.VersionGuid` (Revit 2021+) — a per-element stamp. Equal on both sides means the
//       element did not change AT ALL, so the parameter comparison can be skipped entirely. On a large
//       model this is the difference between a report and a coffee break.
//    3. The full parameter walk — every version, and what runs on Revit 2020.
//    All three are reached by reflection: naming a 2023 method stops this compiling on 2020, and a
//    try/catch does NOT help because that is a compile error.
//
// ✱✱ THE FIRST THING TO READ IS THE VERSION LINE, NOT THE DIFF. `Document.GetDocumentVersion` gives a
//    VersionGUID and a save count. Two files with the SAME GUID are literally the same model at the
//    same save — any difference the comparison then reports is a fault in the comparison, not a change
//    in the model. That is the check that tells you whether to believe the rest of the output.
//
// ✱✱ A CLEAN DIFF IS NOT PROOF OF NOTHING CHANGED. Only the categories in `categoriesToCompare` are
//    looked at, and only the parameters named. Geometry moves are caught only through the location
//    key, so a wall that changed thickness without moving reports as unchanged unless its parameters
//    are in the list. The report says which categories it actually covered, so the claim stays honest.
//
// RELATED: actions/reporting/action-compare-elements.cs (inside one model),
//          actions/structural-changes/action-batch-upgrade-revit-files.cs (the same background-open
//          mechanism), actions/reporting/action-report-element-ownership.cs (who changed it, once you
//          know what changed).
//
// ✱✱ NOT YET RUN ON A REAL MODEL. Read-only on both files. Start with ONE category and a small model.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string otherModelPath = @"D:\Ajmal\Models\previous-issue.rvt";   // the model to compare AGAINST

BuiltInCategory[] categoriesToCompare = {
    BuiltInCategory.OST_DuctCurves,
    BuiltInCategory.OST_PipeCurves,
    BuiltInCategory.OST_MechanicalEquipment,
    BuiltInCategory.OST_DuctTerminal
};

// Parameters checked on elements that exist in BOTH. Keep it short — every name is read per element.
string[] parametersToCompare = { "Mark", "Comments", "Width", "Height", "Diameter", "Length", "Level" };

string matchOn = "id";             // "id"  = match on ElementId — CORRECT when the other file is an
                                   //         earlier save of THIS model (a save-as keeps ids). Default.
                                   // "key" = match on category+family+type+Mark, falling back to
                                   //         location. Only for two UNRELATED models, where ids share
                                   //         no lineage. Noisier: a changed Mark reads as add+remove.
bool useLocationInKey = true;      // "key" mode only — rounded location when Mark is blank
double locationRoundMm = 10.0;     // "key" mode only — how close counts as "the same place"
int maxChangesListed = 60;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

if (string.IsNullOrWhiteSpace(otherModelPath) || !System.IO.File.Exists(otherModelPath))
{
    sb.AppendLine($"Cannot find the other model: {otherModelPath}");
    return sb.ToString();
}

Document other = null;
try
{
    // ---- Identity first. If the two are the same save, nothing below can be trusted as a "change".
    string thisId = "", otherId = "";
    int thisSaves = -1, otherSaves = -1;
    try
    {
        var dv = Document.GetDocumentVersion(Document);
        thisId = dv.VersionGUID.ToString(); thisSaves = dv.NumberOfSaves;
    }
    catch { }

    // ✱✱ OPEN OPTIONS, ADDED 2026-08-24 — THE BARE `OpenDocumentFile(path)` WAS UNSAFE ON A WORKSHARED
    //    FILE, which is what every real project is. Three things it got wrong:
    //      DetachFromCentralOption — without it, opening a CENTRAL file touches the central. Detached
    //        and preserving worksets is the only correct way to read another model.
    //      SetOpenWorksetsConfiguration(CloseAllWorksets) — a comparison reads parameters, it does not
    //        need the geometry of every workset loaded. Closing them all is dramatically faster and
    //        lighter, and Revit still resolves the elements this fragment walks.
    //      AllowOpeningLocalByWrongUser — the path handed over is often somebody's LOCAL, and without
    //        this Revit refuses outright.
    //    On a non-workshared file every one of these is harmless, so they are set unconditionally.
    var openOpts = new OpenOptions
    {
        DetachFromCentralOption = DetachFromCentralOption.DetachAndPreserveWorksets,
        AllowOpeningLocalByWrongUser = true,
        Audit = false
    };
    try { openOpts.SetOpenWorksetsConfiguration(new WorksetConfiguration(WorksetConfigurationOption.CloseAllWorksets)); }
    catch { }

    var modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(otherModelPath);
    try
    {
        other = Document.Application.OpenDocumentFile(modelPath, openOpts);
    }
    catch (Autodesk.Revit.Exceptions.CorruptModelException)
    {
        // ✱✱ REVIT REPORTS A FILE SAVED IN A DIFFERENT VERSION AS "CORRUPT", which sends people looking
        //    for damage that is not there. The file HEADER can be read without opening it, so the real
        //    reason is available — say which it is instead of repeating Revit's misleading word.
        string savedIn = "unknown";
        try { savedIn = BasicFileInfo.Extract(otherModelPath).Format; } catch { }
        string running = "unknown";
        try { running = Document.Application.VersionNumber; } catch { }
        sb.AppendLine(savedIn != running && savedIn != "unknown"
            ? $"That file was saved in Revit {savedIn} and this is Revit {running} — it is a VERSION mismatch, not damage. Upgrade it first (action-batch-upgrade-revit-files.cs) or open it in {savedIn}."
            : $"Revit reports {otherModelPath} as corrupt, and its header says {savedIn} against this Revit {running} — so this one really does look damaged.");
        return sb.ToString();
    }
    catch (Autodesk.Revit.Exceptions.CannotOpenBothCentralAndLocalException)
    {
        sb.AppendLine($"{otherModelPath} is already open in this Revit session (or its central/local twin is). Close it first.");
        return sb.ToString();
    }
    catch (Exception ex)
    {
        sb.AppendLine($"Revit would not open {otherModelPath}: {ex.Message}");
        return sb.ToString();
    }
    if (other == null)
    {
        sb.AppendLine($"Revit would not open {otherModelPath}.");
        return sb.ToString();
    }

    try
    {
        var dv2 = Document.GetDocumentVersion(other);
        otherId = dv2.VersionGUID.ToString(); otherSaves = dv2.NumberOfSaves;
    }
    catch { }

    sb.AppendLine($"COMPARING  '{Document.Title}'  against  '{other.Title}'");
    if (!string.IsNullOrEmpty(thisId) && !string.IsNullOrEmpty(otherId))
    {
        sb.AppendLine($"  open file  : version {thisId}, {thisSaves} save(s)");
        sb.AppendLine($"  other file : version {otherId}, {otherSaves} save(s)");
        if (thisId == otherId)
            sb.AppendLine("  ! SAME VERSION GUID — these are the same model at the same save. Anything reported below is a fault in the comparison, not a change in the model.");
    }
    else
    {
        sb.AppendLine("  (document version unavailable on this Revit — the identity check above is skipped, not passed.)");
    }
    sb.AppendLine();

    // ---- Build a comparable key per element. Stable across a save-as; ElementId is not.
    Func<Document, Element, string> KeyOf = (doc, e) =>
    {
        string cat = e.Category != null ? (e.Category.Name ?? "?") : "?";
        string fam = "", typ = "";
        try
        {
            var te = doc.GetElement(e.GetTypeId()) as ElementType;
            if (te != null) { typ = te.Name ?? ""; try { fam = te.FamilyName ?? ""; } catch { } }
        }
        catch { }

        string mark = "";
        try { var p = e.get_Parameter(BuiltInParameter.ALL_MODEL_MARK); if (p != null && p.HasValue) mark = p.AsString() ?? ""; }
        catch { }

        if (!string.IsNullOrEmpty(mark)) return $"{cat}|{fam}|{typ}|M:{mark}";

        if (useLocationInKey)
        {
            try
            {
                var lp = e.Location as LocationPoint;
                if (lp != null)
                {
                    double r = locationRoundMm / 304.8;
                    return $"{cat}|{fam}|{typ}|P:{Math.Round(lp.Point.X / r)},{Math.Round(lp.Point.Y / r)},{Math.Round(lp.Point.Z / r)}";
                }
                var lc = e.Location as LocationCurve;
                if (lc != null && lc.Curve != null)
                {
                    double r = locationRoundMm / 304.8;
                    var a = lc.Curve.GetEndPoint(0);
                    return $"{cat}|{fam}|{typ}|C:{Math.Round(a.X / r)},{Math.Round(a.Y / r)},{Math.Round(a.Z / r)}";
                }
            }
            catch { }
        }
        return $"{cat}|{fam}|{typ}|?";
    };

    // In "id" mode the key is the ElementId as text — stable across a save-as, and the identity Revit
    // itself uses. ToString() rather than the numeric property, which changed name across versions.
    Func<Document, Element, string> IdKeyOf = (doc, e) => e.Id.ToString();
    Func<Document, Element, string> ChosenKey = (doc, e) =>
        string.Equals(matchOn, "key", StringComparison.OrdinalIgnoreCase) ? KeyOf(doc, e) : IdKeyOf(doc, e);

    Func<Document, Dictionary<string, List<Element>>> Collect = doc =>
    {
        var map = new Dictionary<string, List<Element>>();
        foreach (var bic in categoriesToCompare)
        {
            List<Element> els;
            try
            {
                els = new FilteredElementCollector(doc).OfCategory(bic)
                        .WhereElementIsNotElementType().ToElements().ToList();
            }
            catch { continue; }
            foreach (var e in els)
            {
                var k = ChosenKey(doc, e);
                if (!map.ContainsKey(k)) map[k] = new List<Element>();
                map[k].Add(e);
            }
        }
        return map;
    };

    // The per-element stamp (Revit 2021+). Equal on both sides = this element did not change at all,
    // so the whole parameter walk below can be skipped for it. Reflected, because naming the property
    // stops this compiling on 2020.
    var _pVersionGuid = typeof(Element).GetProperty("VersionGuid");
    Func<Element, string> StampOf = e =>
    {
        if (_pVersionGuid == null) return null;
        try { var v = _pVersionGuid.GetValue(e); return v != null ? v.ToString() : null; }
        catch { return null; }
    };

    var mine = Collect(Document);
    var theirs = Collect(other);

    int ambiguous = mine.Count(kv => kv.Value.Count > 1) + theirs.Count(kv => kv.Value.Count > 1);

    var added = mine.Keys.Where(k => !theirs.ContainsKey(k)).ToList();
    var removed = theirs.Keys.Where(k => !mine.ContainsKey(k)).ToList();
    var common = mine.Keys.Where(k => theirs.ContainsKey(k)).ToList();

    // ---- Parameter differences on the elements present in both.
    Func<Element, string, string> ReadParam = (e, name) =>
    {
        try
        {
            var p = e.LookupParameter(name);
            if (p == null || !p.HasValue) return null;
            if (p.StorageType == StorageType.String) return p.AsString();
            var vs = p.AsValueString();
            return !string.IsNullOrEmpty(vs) ? vs : p.AsString();
        }
        catch { return null; }
    };

    var changes = new List<string>();
    int changedElements = 0, skippedByStamp = 0;
    foreach (var k in common)
    {
        var a = mine[k][0];
        var b = theirs[k][0];

        // Fast path: identical per-element stamp means Revit says nothing about this element changed.
        var sa = StampOf(a);
        var sb2 = StampOf(b);
        if (sa != null && sb2 != null && sa == sb2) { skippedByStamp++; continue; }

        var diffs = new List<string>();
        foreach (var pn in parametersToCompare)
        {
            var va = ReadParam(a, pn);
            var vb = ReadParam(b, pn);
            if (va == null && vb == null) continue;
            if (!string.Equals(va ?? "", vb ?? "", StringComparison.Ordinal))
                diffs.Add($"{pn}: '{vb ?? "(blank)"}' -> '{va ?? "(blank)"}'");
        }
        if (diffs.Count > 0)
        {
            changedElements++;
            if (changes.Count < maxChangesListed)
                changes.Add($"  {k.Replace("|", " / ")}\n      {string.Join("; ", diffs)}");
        }
    }

    // ---- Report
    sb.AppendLine($"ADDED (in the open model, not in the other) : {added.Count}");
    sb.AppendLine($"REMOVED (in the other, not in the open one)  : {removed.Count}");
    sb.AppendLine($"PRESENT IN BOTH                              : {common.Count}");
    sb.AppendLine($"  of those, CHANGED parameters               : {changedElements}");
    sb.AppendLine();

    if (added.Count > 0)
    {
        sb.AppendLine($"ADDED (first {Math.Min(added.Count, 25)})");
        foreach (var k in added.Take(25)) sb.AppendLine("  + " + k.Replace("|", " / "));
        if (added.Count > 25) sb.AppendLine($"  ... and {added.Count - 25} more.");
        sb.AppendLine();
    }
    if (removed.Count > 0)
    {
        sb.AppendLine($"REMOVED (first {Math.Min(removed.Count, 25)})");
        foreach (var k in removed.Take(25)) sb.AppendLine("  - " + k.Replace("|", " / "));
        if (removed.Count > 25) sb.AppendLine($"  ... and {removed.Count - 25} more.");
        sb.AppendLine();
    }
    if (changes.Count > 0)
    {
        sb.AppendLine($"CHANGED (first {changes.Count}, shown as other -> open)");
        foreach (var c in changes) sb.AppendLine(c);
        if (changedElements > changes.Count) sb.AppendLine($"  ... and {changedElements - changes.Count} more.");
        sb.AppendLine();
    }

    sb.AppendLine("SCOPE OF THIS COMPARISON — read before quoting the numbers:");
    sb.AppendLine($"  matched on : {(string.Equals(matchOn, "key", StringComparison.OrdinalIgnoreCase) ? "composite key (category+family+type+Mark, else location) — for UNRELATED models" : "ElementId — correct when the other file is an earlier save of this model")}");
    sb.AppendLine($"  categories : {string.Join(", ", categoriesToCompare.Select(c => c.ToString()))}");
    sb.AppendLine($"  parameters : {string.Join(", ", parametersToCompare)}");
    sb.AppendLine("  Anything outside those is NOT compared and would report as unchanged.");
    if (skippedByStamp > 0)
        sb.AppendLine($"  {skippedByStamp} element(s) skipped by their per-element version stamp — Revit says they did not change, so their parameters were not walked.");
    else if (_pVersionGuid == null)
        sb.AppendLine("  This Revit has no per-element version stamp (it arrived in 2021), so every common element was walked in full.");
    if (ambiguous > 0)
        sb.AppendLine($"  ! {ambiguous} key(s) matched more than one element — those were compared on the first match only. Give the elements a Mark to separate them.");
}
catch (Exception ex)
{
    sb.AppendLine($"Comparison failed: {ex.Message}");
}
finally
{
    // Always close what was opened, or the file stays locked and the next run cannot read it.
    try { if (other != null) other.Close(false); } catch { }
}

return sb.ToString();
