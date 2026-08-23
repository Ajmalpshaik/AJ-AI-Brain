// ============================================================
// FRAGMENT (action) — action-report-global-parameters.cs
// PURPOSE: List every GLOBAL PARAMETER in the project — name, value, whether it is driven by a formula,
//          whether it is reporting, and WHAT IT DRIVES: the dimensions and element parameters labelled
//          by it. Optionally set one global parameter's value and show what moved.
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`).
//
// ✱✱ A GLOBAL PARAMETER IS NOT A PROJECT PARAMETER, and mixing them up wastes a session. A project
//    parameter is a COLUMN — every door gets its own value. A global parameter is a SINGLE VALUE for the
//    whole model that can be attached to dimensions and to element parameters, so changing it moves
//    geometry. "Corridor width = 1800" as one number that eight dimensions obey is a global parameter;
//    nothing in this library could see them, which meant a model driven by them looked, from here, like
//    a model where things moved for no reason.
//
// ✱✱ THE INTERESTING PART IS GetLabeledDimensions(), NOT THE VALUE. A global parameter's value is one
//    number. What matters is which DIMENSIONS are labelled by it — that is the list of things that move
//    when it changes, and it is the reason "why did that wall shift when I edited a number" has an
//    answer. Reported per parameter below. (Element parameters associated with a global parameter are
//    found from the other end, via `parameter.GetAssociatedGlobalParameter()` on the element, not from
//    the global parameter — so the count here is dimensions, and it says so.)
//
// ✱✱ NOT EVERY MODEL ALLOWS THEM. GlobalParametersManager.AreGlobalParametersAllowed(doc) is false in a
//    family document and in some template states. Asked first, and reported plainly, because an empty
//    list otherwise reads as "this model has none" when the truth is "this kind of document cannot".
//
// GOTCHA: SETTING A VALUE MOVES GEOMETRY. That is the entire point of a global parameter, so setValueFor
//         is off by default and the fragment prints the before/after value and the number of labelled
//         references it drives, so the size of the change is visible before it is made.
// GOTCHA: a global parameter driven by a FORMULA refuses SetValue — its value comes from the formula.
//         Reported as such rather than as a failure.
// GOTCHA: a REPORTING global parameter reads a dimension from the model and cannot be set at all — it is
//         an output, not an input.
// GOTCHA: creating a global parameter is deliberately NOT here. GlobalParameter.Create changed signature
//         at Revit 2022 (ParameterType became ForgeTypeId), so a create path would need reflection for a
//         job that is two clicks in Manage > Global Parameters and is done once per project.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Most MEP models have none; check on one that
//   uses them (an architectural model with driven corridor widths is the usual case) before concluding
//   the fragment is wrong.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string setValueFor = null;      // name of ONE global parameter to change; null = report only
double newNumberValue = 0;      // used when that parameter holds a number (mm for a length)
string newTextValue = null;     // used when it holds text; leave null for a number
bool dryRun = true;             // true = say what would change, change nothing
// ---- END INPUTS ----

const double MM_PER_FOOT = 304.8;
var sb = new System.Text.StringBuilder();

bool allowed = false;
try { allowed = GlobalParametersManager.AreGlobalParametersAllowed(Document); } catch { }
if (!allowed)
{
    sb.AppendLine("This document does not allow global parameters (a family document, or a template state that forbids them). Nothing to report — this is not the same as 'the model has none'.");
    return sb.ToString();
}

IList<ElementId> ordered = null;
try { ordered = GlobalParametersManager.GetGlobalParametersOrdered(Document); } catch { }
if (ordered == null || ordered.Count == 0)
{
    sb.AppendLine("This model has NO global parameters.");
    return sb.ToString();
}

sb.AppendLine($"=== GLOBAL PARAMETERS — {ordered.Count} in the project, in Revit's own order ===");
sb.AppendLine("Name | Value | Kind | Drives (labelled references)");
sb.AppendLine("--- | --- | --- | ---");

var byName = new Dictionary<string, GlobalParameter>(StringComparer.OrdinalIgnoreCase);

foreach (var id in ordered)
{
    var gp = Document.GetElement(id) as GlobalParameter;
    if (gp == null) continue;
    byName[gp.Name] = gp;

    string valueText = "(unreadable)";
    try
    {
        var v = gp.GetValue();
        var dv = v as DoubleParameterValue;
        var sv = v as StringParameterValue;
        var iv = v as IntegerParameterValue;
        var ev = v as ElementIdParameterValue;
        if (dv != null)
        {
            // Internal units are feet for a length. Both are shown because a global parameter can hold
            // an angle or a plain number too, and guessing which would be a lie either way.
            valueText = $"{dv.Value:F4} internal ({dv.Value * MM_PER_FOOT:F1} if it is a length, in mm)";
        }
        else if (sv != null) valueText = "\"" + (sv.Value ?? "") + "\"";
        else if (iv != null) valueText = iv.Value.ToString();
        else if (ev != null) valueText = "Element " + ev.Value.ToString();
        else if (v != null) valueText = v.GetType().Name;
    }
    catch (Exception ex) { valueText = "(unreadable: " + ex.Message + ")"; }

    var kinds = new List<string>();
    try { if (gp.IsReporting) kinds.Add("reporting (reads the model, cannot be set)"); } catch { }
    try { if (gp.GetFormula() != null && gp.GetFormula() != "") kinds.Add("formula: " + gp.GetFormula()); } catch { }
    if (kinds.Count == 0) kinds.Add("plain value");

    int labelCount = 0;
    string labelSample = "";
    try
    {
        var labels = gp.GetLabeledDimensions();
        if (labels != null)
        {
            labelCount = labels.Count;
            labelSample = string.Join(", ", labels.Take(6).Select(l =>
            {
                var el = Document.GetElement(l);
                return el == null ? l.ToString() : $"{el.Category?.Name ?? el.GetType().Name} {l}";
            }));
            if (labelCount > 6) labelSample += $", +{labelCount - 6} more";
        }
    }
    catch { labelSample = "(labels unreadable)"; }

    sb.AppendLine($"{gp.Name} | {valueText} | {string.Join("; ", kinds)} | {labelCount}{(labelCount > 0 ? " — " + labelSample : "")}");
}

// ---- optional: change one ----
if (string.IsNullOrEmpty(setValueFor))
{
    sb.AppendLine();
    sb.AppendLine("Report only. To change one, set setValueFor to its name (and dryRun = false when the preview looks right).");
    return sb.ToString();
}

GlobalParameter target;
if (!byName.TryGetValue(setValueFor, out target))
{
    sb.AppendLine();
    sb.AppendLine($"No global parameter named '{setValueFor}'. Names above are exact.");
    return sb.ToString();
}

bool isReporting = false;
try { isReporting = target.IsReporting; } catch { }
string formula = null;
try { formula = target.GetFormula(); } catch { }

if (isReporting)
{
    sb.AppendLine();
    sb.AppendLine($"'{setValueFor}' is a REPORTING parameter — it reads a dimension out of the model and cannot be set.");
    return sb.ToString();
}
if (!string.IsNullOrEmpty(formula))
{
    sb.AppendLine();
    sb.AppendLine($"'{setValueFor}' is driven by the formula `{formula}` — its value comes from that, and SetValue will be refused. Change the formula in Manage > Global Parameters.");
    return sb.ToString();
}

int drives = 0;
try { var l = target.GetLabeledDimensions(); if (l != null) drives = l.Count; } catch { }

sb.AppendLine();
sb.AppendLine($"About to change '{setValueFor}', which drives {drives} labelled reference(s) — that many dimensions/parameters will move.");

if (dryRun)
{
    sb.AppendLine("DRY RUN — nothing changed. Set dryRun = false to apply.");
    return sb.ToString();
}

using (var t = new Transaction(Document, "AJ Tools - Set Global Parameter"))
{
    t.Start();
    try
    {
        ParameterValue newValue;
        if (newTextValue != null) newValue = new StringParameterValue(newTextValue);
        else newValue = new DoubleParameterValue(newNumberValue / MM_PER_FOOT); // mm in, feet stored

        target.SetValue(newValue);
        t.Commit();
        sb.AppendLine(newTextValue != null
            ? $"Set '{setValueFor}' to \"{newTextValue}\"."
            : $"Set '{setValueFor}' to {newNumberValue} mm ({newNumberValue / MM_PER_FOOT:F4} internal).");
        sb.AppendLine("Look at the model — anything labelled by it has moved.");
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED, rolled back — nothing changed: {ex.Message}");
    }
}

return sb.ToString();
