// ============================================================
// PRELUDE (shared toolkit) — prelude.cs
// PURPOSE: The helper functions that 150+ fragments currently each re-implement — transactions with
//          rollback, mm/feet conversion, view targeting, parameter lookup, level resolution. Paste this
//          ONCE at the very top of a composed script, before the filter fragment.
//
//          The point is NOT shorter code, it is ONE PLACE TO BE WRONG. Today 80 fragments each name
//          `DisplayUnitType` directly, so supporting Revit 2021+ (where it is deprecated, and 2022+
//          where it is gone) means editing 80 files and getting all 80 right. Below it is named twice,
//          in one place, with the replacement line sitting right next to it.
//
// COMPOSITION ORDER: prelude → filter (or creator) → action(s) → `return sb.ToString();`
//          This is ADDITIVE. It declares no name any existing fragment declares — in particular it does
//          NOT declare `sb` or `elements`, because the filter fragments already do (see
//          filter-by-category.cs, which does `var sb = new System.Text.StringBuilder();` itself).
//          That is why every helper here that reports takes the StringBuilder as its first argument
//          instead of capturing one: it has to compose with un-migrated fragments unchanged.
//
// STATUS: ✓ LIVE-VERIFIED 2026-08-07 — every helper below, all PASS, via
//          examples/prelude-smoke-test.cs against a real model through this bridge:
//            units      1000mm -> 3.28084 ft -> 1000.00 mm (exact round trip)
//            view       active view resolved, and by-Id resolved to the same view
//            collect    8 walls
//            params     ParamText gave '(blank)' for an empty Comments and '(no such parameter)'
//                       for a name that does not exist — the distinction this helper exists for
//            level      LevelIdOf on a Wall -> 'Level 1'
//            sizesort   unknown | 100ø | 250ø | 300x150 | 500x400 | 1200x600 (the ø survives)
//            tx-commit  opened and committed
//            tx-rollback a deliberate throw was caught, rolled back and REPORTED, not thrown to
//                       the bridge — the behaviour AGENT-SPEC §2.5 requires
//            tx-group   opened and assimilated
//          Written 2026-08-04 in a session with no Revit and no C# compiler, so it sat unproven for
//          three days; it is now safe to compose in front of any fragment.
// ============================================================


// ---- UNITS ---------------------------------------------------------------------------------------
// THE ONE PLACE the whole library names a Revit unit API. Revit 2020 uses DisplayUnitType; 2021
// deprecated it in favour of ForgeTypeId/UnitTypeId and 2022 removed it outright. Porting the library
// to a newer Revit = editing these two lambdas, not 80 fragments.
//
//   Revit 2021+ replacement (swap both lines, nothing else in the library changes):
//     Func<double, ForgeTypeId, double> ToInternal = (v, u) => UnitUtils.ConvertToInternalUnits(v, u);
//     Func<double, ForgeTypeId, double> FromInternal = (v, u) => UnitUtils.ConvertFromInternalUnits(v, u);
//     ... and pass UnitTypeId.Millimeters where DisplayUnitType.DUT_MILLIMETERS is passed today.
Func<double, DisplayUnitType, double> ToInternal = (value, unit) => UnitUtils.ConvertToInternalUnits(value, unit);
Func<double, DisplayUnitType, double> FromInternal = (value, unit) => UnitUtils.ConvertFromInternalUnits(value, unit);

// The user speaks mm, Revit's API is feet — convert explicitly both ways, never leave raw feet in a
// reply (START-HERE.md rule 3). These two cover 144 of the library's 160 unit calls.
Func<double, double> ToFeet = mm => ToInternal(mm, DisplayUnitType.DUT_MILLIMETERS);
Func<double, double> ToMm = ft => FromInternal(ft, DisplayUnitType.DUT_MILLIMETERS);


// ---- TRANSACTIONS --------------------------------------------------------------------------------
// AGENT-SPEC §2.5: every write wraps its Transaction in try/catch that calls .RollBack() and appends a
// clear reason on failure — never let an exception through as a bare error. 150 fragments carry their
// own copy of that shape; this is the same shape, once.
//
// Local names are `tx`/`tg` rather than the conventional `t`/`group` deliberately: a fragment pasted
// after this prelude is free to declare its own `t` without a shadowing conflict.
Action<System.Text.StringBuilder, string, Action> InTransaction = (outSb, name, body) =>
{
    using (var tx = new Transaction(Document, "AJ Tools - " + name))
    {
        tx.Start();
        try
        {
            body();
            tx.Commit();
        }
        catch (Exception ex)
        {
            tx.RollBack();
            outSb.AppendLine($"FAILED ({name}) — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
};

// For a multi-step sequence with real dependencies between steps (draw a duct, then cap it): one
// TransactionGroup, Assimilate() only on full success, so a mid-sequence failure can never leave a
// half-built result behind. Pattern lifted from draw-main-duct-with-cap.cs, which is live-proven.
Action<System.Text.StringBuilder, string, Action> InTransactionGroup = (outSb, name, body) =>
{
    using (var tg = new TransactionGroup(Document, "AJ Tools - " + name))
    {
        tg.Start();
        try
        {
            body();
            tg.Assimilate();
        }
        catch (Exception ex)
        {
            tg.RollBack();
            outSb.AppendLine($"FAILED ({name}) — whole sequence rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
};


// ---- VIEW TARGETING ------------------------------------------------------------------------------
// AGENT-SPEC §3.5: view is a variable, never hardcoded, on every graphics/visibility action — null
// means the active view, an Id targets any view including one not currently on screen. Returns null if
// the Id exists but is not a View, so the caller can report that rather than throwing.
// (`new ElementId(long)` does not compile on this API surface — the int overload is the only one.)
Func<int?, View> ResolveView = viewIdInt =>
    viewIdInt.HasValue
        ? Document.GetElement(new ElementId(viewIdInt.Value)) as View
        : Document.ActiveView;


// ---- PARAMETERS ----------------------------------------------------------------------------------
// Instance first, then falling back to the type. Returns NULL when the parameter genuinely does not
// exist on this element — which is exactly the distinction a name-based report silently loses:
// core.md records that a report for a parameter that does not exist returns a BLANK column rather than
// an error, so "Level" on a duct reads as missing data when the real name is "Reference Level". On a
// takeoff that is a silent wrong answer.
Func<Element, string, Parameter> ParamOf = (e, name) =>
{
    var p = e.LookupParameter(name);
    if (p != null) return p;
    var typeElem = Document.GetElement(e.GetTypeId());
    return typeElem == null ? null : typeElem.LookupParameter(name);
};

// Display text for a parameter, keeping missing and blank visibly different. Never returns "" for a
// parameter that does not exist — that conflation is the bug above.
Func<Element, string, string> ParamText = (e, name) =>
{
    var p = ParamOf(e, name);
    if (p == null) return "(no such parameter)";
    if (!p.HasValue) return "(blank)";
    switch (p.StorageType)
    {
        case StorageType.String: return p.AsString() ?? "(blank)";
        case StorageType.Integer: return p.AsInteger().ToString();
        case StorageType.Double: return p.AsValueString() ?? p.AsDouble().ToString();
        case StorageType.ElementId: return p.AsValueString() ?? p.AsElementId().ToString();
        default: return p.AsValueString() ?? "(unreadable)";
    }
};


// ---- LEVEL ---------------------------------------------------------------------------------------
// There is no single universal "get this element's level" API — Wall/Floor/FamilyInstance each store it
// differently, so this walks a fallback chain. Lifted verbatim from filter-by-category.cs, where it is
// already live-proven; it lives here now so the next filter that needs it doesn't copy it again.
Func<Element, ElementId> LevelIdOf = e =>
{
    if (e is Wall wallEl) return wallEl.LevelId;
    if (e.LevelId != ElementId.InvalidElementId) return e.LevelId;
    // RBS_START_LEVEL_PARAM is the MEP-curve one and MUST stay in this chain:
    // on a Duct/Pipe/CableTray/Conduit the other four are NOT PRESENT at all
    // (proved live 2026-08-06 — all four returned "parameter not present",
    // RBS_START_LEVEL_PARAM returned Level 1). Without it every MEP curve
    // resolves to InvalidElementId, so a level filter silently matches NOTHING
    // rather than erroring — the confidently-wrong failure, not a crash.
    var lp = e.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM)
        ?? e.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM)
        ?? e.get_Parameter(BuiltInParameter.LEVEL_PARAM)
        ?? e.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM)
        ?? e.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM);
    return lp == null ? ElementId.InvalidElementId : lp.AsElementId();
};


// ---- COLLECTING ----------------------------------------------------------------------------------
// The plain "every instance of one category" collector, which 136 fragments open by hand.
// WARNING carried over from AGENT-SPEC §6.1: FilteredElementCollector.UnionWith() does NOT preserve
// either side's quick filters, so a .WhereElementIsNotElementType() applied per-piece before a union is
// silently lost and TYPE elements leak into the result. Apply it ONCE, after all UnionWith calls.
Func<BuiltInCategory, List<Element>> CollectOf = cat =>
    new FilteredElementCollector(Document)
        .OfCategory(cat)
        .WhereElementIsNotElementType()
        .ToList();


// ---- REPORT FORMAT -------------------------------------------------------------------------------
// reply-style.md, the user's standing rule (2026-07-18): a size breakdown is sorted by SIZE ASCENDING,
// never by quantity or length. Sorting by this key applies the rule instead of relying on remembering
// it. Strips non-numeric decoration so "250ø", "ø250", "DN200" and "450x250" all parse; a blank or
// "unknown" size sorts first at 0,0 rather than throwing. Kept pure ASCII on purpose — a literal
// diameter symbol in a fragment was silently double-encoded once, after which every round size sorted
// as 0 (brain-log 2026-08-04).
Func<string, Tuple<double, double>> SizeSortKey = s =>
{
    var cleaned = new string((s ?? "").Select(c => char.IsDigit(c) || c == '.' || c == 'x' || c == 'X' ? c : ' ').ToArray());
    var parts = cleaned.Replace('X', 'x').Split(new[] { 'x' }, StringSplitOptions.RemoveEmptyEntries);
    double a = 0, b = 0;
    if (parts.Length > 0) double.TryParse(parts[0], out a);
    if (parts.Length > 1) double.TryParse(parts[1], out b); else b = a;
    return Tuple.Create(a, b);
};

// ---- end of prelude — paste the filter fragment next ----------------------------------------------
