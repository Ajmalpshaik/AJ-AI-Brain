// ============================================================
// SCRIPT (context) — context-project-units.cs
// PURPOSE: Report every unit spec that's actually valid for this document's discipline (Length, Area,
//          HVAC Airflow, Piping Flow, etc.) and what display unit each is currently set to — e.g. is
//          this project in mm or m, CFM or L/s. Read-only, zero input, safe to call anytime.
// VERSION: one source, Revit 2020 through 2027. This is the fragment the unit API change hit hardest:
//          Revit 2021 introduced ForgeTypeId and 2022 REMOVED the whole old surface, so `UnitType`,
//          `FormatOptions.DisplayUnits`, `FormatOptions.UnitSymbol` and `UnitSymbolType` are all gone
//          on 2024 - four names that cannot appear in the source. Everything here is therefore reached
//          by NAME at run time: the spec list, the two FormatOptions readers, and the label lookup.
//          The old path is still taken on 2020, where the new names do not exist either.
// ============================================================

var sb = new System.Text.StringBuilder();
Units units = Document.GetUnits();

var _asm  = typeof(Document).Assembly;
var _flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;

// LabelUtils gained GetLabelForSpec/GetLabelForUnit/GetLabelForSymbol and lost the single GetLabelFor.
// `kind` names the one we want first; anything that accepts the value is tried before giving up on
// ToString(), because a wrong-but-accepting overload throws rather than returning a wrong label.
Func<object, string, string> Label = (v, kind) =>
{
    if (v == null) return "";
    var vt = v.GetType();
    var cands = typeof(LabelUtils).GetMethods()
        .Where(m => m.Name.StartsWith("GetLabelFor") && m.GetParameters().Length == 1
                 && m.GetParameters()[0].ParameterType.IsAssignableFrom(vt))
        .OrderBy(m => m.Name == "GetLabelFor" + kind ? 0 : (m.Name == "GetLabelFor" ? 1 : 2));
    foreach (var m in cands) { try { return (string)m.Invoke(null, new object[] { v }); } catch { } }
    return v.ToString();
};

// A ForgeTypeId with an empty TypeId means "no symbol"; the old enum spelled that UST_NONE.
Func<object, bool> NoSymbol = s =>
{
    if (s == null) return true;
    var tp = s.GetType().GetProperty("TypeId");
    if (tp != null) return string.IsNullOrEmpty((string)tp.GetValue(s) ?? "");
    return s.ToString() == "UST_NONE";
};

// The spec list: GetAllMeasurableSpecs() from 2021, the UnitType enum before that.
var _getAllSpecs = typeof(UnitUtils).GetMethod("GetAllMeasurableSpecs", Type.EmptyTypes);
List<object> specs;
if (_getAllSpecs != null)
    specs = ((System.Collections.IEnumerable)_getAllSpecs.Invoke(null, null)).Cast<object>().ToList();
else
{
    var _unitTypeT = _asm.GetType("Autodesk.Revit.DB.UnitType");
    if (_unitTypeT == null) throw new InvalidOperationException("This Revit has neither GetAllMeasurableSpecs() nor UnitType.");
    specs = Enum.GetValues(_unitTypeT).Cast<object>().ToList();
}
if (specs.Count == 0) throw new InvalidOperationException("No measurable specs reported by this Revit.");

// GetFormatOptions takes whichever type the spec list is made of.
var _getFO = typeof(Units).GetMethod("GetFormatOptions", new[] { specs[0].GetType() });
if (_getFO == null) throw new InvalidOperationException("No Units.GetFormatOptions(" + specs[0].GetType().Name + ") on this Revit.");

// Display unit and symbol: methods from 2021, plain properties before that.
var _getUnitTypeId   = typeof(FormatOptions).GetMethod("GetUnitTypeId",   Type.EmptyTypes);
var _getSymbolTypeId = typeof(FormatOptions).GetMethod("GetSymbolTypeId", Type.EmptyTypes);
var _displayUnitsP   = typeof(FormatOptions).GetProperty("DisplayUnits");
var _unitSymbolP     = typeof(FormatOptions).GetProperty("UnitSymbol");

Func<FormatOptions, object> UnitOf = fo =>
    _getUnitTypeId != null ? _getUnitTypeId.Invoke(fo, null)
    : (_displayUnitsP != null ? _displayUnitsP.GetValue(fo) : null);
Func<FormatOptions, object> SymbolOf = fo =>
    _getSymbolTypeId != null ? _getSymbolTypeId.Invoke(fo, null)
    : (_unitSymbolP != null ? _unitSymbolP.GetValue(fo) : null);

int reported = 0;
foreach (var spec in specs)
{
    FormatOptions fo;
    try { fo = (FormatOptions)_getFO.Invoke(units, new object[] { spec }); }
    catch { continue; } // not a valid spec for this document — skip, don't report
    if (fo == null) continue;

    string specName = Label(spec, "Spec");
    string unitName = Label(UnitOf(fo), "Unit");
    object symRaw   = SymbolOf(fo);
    string symbol   = NoSymbol(symRaw) ? "" : Label(symRaw, "Symbol");

    sb.AppendLine($"{specName} -> {unitName}" + (string.IsNullOrEmpty(symbol) ? "" : $" ({symbol})"));
    reported++;
}

sb.AppendLine("---");
sb.AppendLine($"{reported} unit spec(s) reported (only specs valid for this document's discipline are included).");
return sb.ToString();
