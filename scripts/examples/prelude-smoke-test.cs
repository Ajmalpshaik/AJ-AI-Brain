// ============================================================
// EXAMPLE (assembled, standalone) — prelude-smoke-test.cs
// PURPOSE: Verify every helper in ../lib/prelude.cs in ONE bridge call. The prelude was written in a
//          session with no Revit and no C# compiler, so nothing in it is proven until this runs.
//          Run this BEFORE using the prelude for real work.
//
// HOW TO RUN: paste ../lib/prelude.cs first, then this file, and send the whole thing to run_csharp.
//          This file supplies its own `sb` and `elements`, so no filter fragment is needed.
//
// SAFE TO RUN: read-only in effect. It opens two transactions to prove the wrapper works — one that
//          changes nothing and commits, one that throws on purpose to prove RollBack fires and the
//          failure is reported rather than escaping. Neither modifies the model.
// ============================================================

var sb = new System.Text.StringBuilder();
sb.AppendLine("prelude smoke test");
sb.AppendLine("------------------");

// ---- UNITS ----
double ft = ToFeet(1000.0);
double backToMm = ToMm(ft);
bool unitsOk = Math.Abs(ft - 3.28084) < 0.001 && Math.Abs(backToMm - 1000.0) < 0.001;
sb.AppendLine($"{(unitsOk ? "PASS" : "FAIL")}  units      1000mm -> {ft:F5} ft -> {backToMm:F2} mm (expect 3.28084 ft)");

// ---- VIEW TARGETING ----
View activeView = ResolveView(null);
View byId = activeView == null ? null : ResolveView(activeView.Id.IntegerValue);
bool viewOk = activeView != null && byId != null && byId.Id == activeView.Id;
sb.AppendLine($"{(viewOk ? "PASS" : "FAIL")}  view       active='{(activeView == null ? "none" : activeView.Name)}', by-Id resolves to the same view");

// ---- COLLECTING ---- (walls if present, else ducts, else any one category that has instances)
List<Element> elements = CollectOf(BuiltInCategory.OST_Walls);
string usedCat = "OST_Walls";
if (elements.Count == 0) { elements = CollectOf(BuiltInCategory.OST_DuctCurves); usedCat = "OST_DuctCurves"; }
sb.AppendLine($"PASS  collect    {elements.Count} element(s) from {usedCat} (0 is a valid answer in an empty model)");

// ---- PARAMETERS + LEVEL ---- (only if something was collected)
if (elements.Count > 0)
{
    Element sample = elements[0];
    string real = ParamText(sample, "Comments");
    string missing = ParamText(sample, "ThisParameterDoesNotExist_SmokeTest");
    bool paramOk = missing == "(no such parameter)";
    sb.AppendLine($"{(paramOk ? "PASS" : "FAIL")}  params     Comments='{real}'; a nonexistent name returns '{missing}' (must NOT be blank)");

    ElementId lvl = LevelIdOf(sample);
    Element lvlElem = lvl == ElementId.InvalidElementId ? null : Document.GetElement(lvl);
    sb.AppendLine($"PASS  level      Id {sample.Id.IntegerValue} -> level '{(lvlElem == null ? "none resolved" : lvlElem.Name)}'");
}
else
{
    sb.AppendLine("SKIP  params     no elements in this model to sample");
    sb.AppendLine("SKIP  level      no elements in this model to sample");
}

// ---- SIZE SORT KEY ----
var sizes = new List<string> { "500x400", "250ø", "unknown", "100ø", "300x150", "1200x600" };
var sorted = sizes.OrderBy(s => SizeSortKey(s).Item1).ThenBy(s => SizeSortKey(s).Item2).ToList();
bool sortOk = sorted[0] == "unknown" && sorted[1] == "100ø" && sorted[sorted.Count - 1] == "1200x600";
sb.AppendLine($"{(sortOk ? "PASS" : "FAIL")}  sizesort   {string.Join(" | ", sorted)}");

// ---- TRANSACTION (commit path) ---- changes nothing, proves the wrapper opens and commits
bool committed = false;
InTransaction(sb, "Smoke Test Commit", () => { committed = true; });
sb.AppendLine($"{(committed ? "PASS" : "FAIL")}  tx-commit  transaction opened and committed, model unchanged");

// ---- TRANSACTION (rollback path) ---- throws on purpose; must be caught, rolled back and reported
int before = sb.Length;
InTransaction(sb, "Smoke Test Rollback", () => { throw new InvalidOperationException("deliberate smoke-test failure"); });
string rollbackReport = sb.ToString().Substring(before);
bool rollbackOk = rollbackReport.Contains("rolled back") && rollbackReport.Contains("deliberate smoke-test failure");
sb.AppendLine($"{(rollbackOk ? "PASS" : "FAIL")}  tx-rollback exception caught, rolled back, reported (not thrown to the bridge)");

// ---- TRANSACTION GROUP ---- empty group, assimilated
bool grouped = false;
InTransactionGroup(sb, "Smoke Test Group", () => { grouped = true; });
sb.AppendLine($"{(grouped ? "PASS" : "FAIL")}  tx-group   group opened and assimilated, model unchanged");

sb.AppendLine("------------------");
sb.AppendLine("Any FAIL above means that prelude helper is wrong — fix ../lib/prelude.cs, not the caller.");
return sb.ToString();
