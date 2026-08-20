// ============================================================
// FRAGMENT (action) — action-add-project-parameter.cs
// PURPOSE: Create a NEW parameter (via a shared parameter definition) and bind it to one or more
//          categories — "add a project parameter called AJ_Status to Duct Accessories" — genuinely
//          different job from every other parameters-naming/ fragment, which all edit VALUES of a
//          parameter that already exists. This one creates the parameter itself.
// DOES NOT consume `elements` — operates on the document's shared parameter file and category bindings,
//          not a model element set.
// VERSION NOTE: the spec argument is resolved at RUN time, so one source runs on 2020 through 2027.
// Revit 2022 swapped ExternalDefinitionCreationOptions' second argument from the old enum to a
// ForgeTypeId, and 2024 made the old enum inaccessible - so neither name can appear in the source. The
// constructor is chosen by its own real parameter type and the spec looked up by leaf name ("Text").
// The parameter GROUP is resolved the same way: 2027 removed the old group enum for GroupTypeId, so
// neither name appears in the source. Verified to compile on Revit 2020, 2024 and 2027.
// GOTCHA: needs Application.SharedParametersFilename already pointing at a real, writable shared parameter
//         .txt file — if none is set, this creates one at sharedParamFileFallbackPath instead of failing.
// Verification status: see this fragment's row in scripts/README.md (the single source of truth for this).
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string groupName = "AJ Tools Parameters"; // shared parameter file GROUP (not the schedule "parameter group")
string parameterName = "AJ_Status";
bool isInstanceParameter = true; // true = Instance binding, false = Type binding
BuiltInCategory[] targetCategories = { BuiltInCategory.OST_DuctAccessory };
string sharedParamFileFallbackPath = @"C:\Temp\AJTools_SharedParameters.txt"; // used only if none is already set
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
var app = Document.Application;

// ---- VERSION-PROOF ParameterBindings.Insert / ReInsert ---------------------------------------------
// The third argument is the parameter GROUP. Revit 2027 removed the old group enum in favour of
// GroupTypeId, so that enum cannot be named in source that must also run on 2020. Both the method and
// the group value are resolved by name; "PG_DATA" maps to GroupTypeId.Data by stripping PG_ and
// Title-casing, which is the rule for every group member this library uses.
Func<string, System.Reflection.MethodInfo> _bindM = which => typeof(BindingMap).GetMethods()
    .Where(m => m.Name == which && m.GetParameters().Length == 3
             && m.GetParameters()[0].ParameterType == typeof(Definition))
    .OrderBy(m => m.GetParameters()[2].ParameterType.Name == "ForgeTypeId" ? 0 : 1)
    .FirstOrDefault();

Func<System.Type, string, object> _groupAs = (gt, nm) =>
{
    if (gt.Name != "ForgeTypeId") return Enum.Parse(gt, nm);
    var core = nm.StartsWith("PG_") ? nm.Substring(3) : nm;
    var camel = string.Join("", core.Split('_')
        .Select(w => w.Length == 0 ? w : w.Substring(0, 1).ToUpper() + w.Substring(1).ToLower()));
    var gidT = typeof(Document).Assembly.GetType("Autodesk.Revit.DB.GroupTypeId");
    var pr = gidT == null ? null : gidT.GetProperty(camel,
        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
    if (pr == null) throw new InvalidOperationException("No GroupTypeId." + camel + " on this Revit (from '" + nm + "').");
    return pr.GetValue(null);
};

Func<string, Definition, Binding, string, bool> BindParam = (which, def, bind, grp) =>
{
    var m = _bindM(which);
    if (m == null) throw new InvalidOperationException("This Revit has no BindingMap." + which + "(Definition, Binding, group).");
    return (bool)m.Invoke(Document.ParameterBindings,
        new object[] { def, bind, _groupAs(m.GetParameters()[2].ParameterType, grp) });
};
// ---------------------------------------------------------------------------------------------------


// ---- VERSION-PROOF ExternalDefinitionCreationOptions ----------------------------------------------
// Picked by the constructor's own second parameter type, not by testing whether ForgeTypeId exists -
// Revit 2021 ships ForgeTypeId but this constructor there still takes the old enum.
var _edcoCtor = typeof(ExternalDefinitionCreationOptions).GetConstructors()
    .Where(c => c.GetParameters().Length == 2 && c.GetParameters()[0].ParameterType == typeof(string))
    .OrderBy(c => c.GetParameters()[1].ParameterType.Name == "ForgeTypeId" ? 0 : 1)
    .FirstOrDefault();
if (_edcoCtor == null) throw new InvalidOperationException("This Revit has no ExternalDefinitionCreationOptions(string, spec) constructor.");
var _edcoSpecT = _edcoCtor.GetParameters()[1].ParameterType;

// The leaf name is the same in both worlds ("Text"); SpecTypeId files Text under String.
Func<string, object> _specFor = nm =>
{
    if (_edcoSpecT.Name != "ForgeTypeId") return Enum.Parse(_edcoSpecT, nm);
    var _asm = typeof(Document).Assembly;
    var _fl = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;
    foreach (var tn in new[] { "Autodesk.Revit.DB.SpecTypeId", "Autodesk.Revit.DB.SpecTypeId+String", "Autodesk.Revit.DB.SpecTypeId+Boolean" })
    {
        var ty = _asm.GetType(tn);
        var pr = ty == null ? null : ty.GetProperty(nm, _fl);
        if (pr != null) return pr.GetValue(null);
    }
    throw new InvalidOperationException("No SpecTypeId named '" + nm + "' on this Revit.");
};
Func<string, string, ExternalDefinitionCreationOptions> MakeDefOptions = (nm, spec) =>
    (ExternalDefinitionCreationOptions)_edcoCtor.Invoke(new object[] { nm, _specFor(spec) });
// ---------------------------------------------------------------------------------------------------


if (string.IsNullOrEmpty(app.SharedParametersFilename))
{
    if (!System.IO.File.Exists(sharedParamFileFallbackPath))
    {
        System.IO.File.WriteAllText(sharedParamFileFallbackPath, "# This is a Revit shared parameter file.\n# Do not edit manually.\n*META\tVERSION\tMINVERSION\nMETA\t2\t1\n*GROUP\tID\tNAME\n*PARAM\tGUID\tNAME\tDATATYPE\tDATACATEGORY\tGROUP\tVISIBLE\tDESCRIPTION\tUSERMODIFIABLE\n");
    }
    app.SharedParametersFilename = sharedParamFileFallbackPath;
}

var defFile = app.OpenSharedParameterFile();
if (defFile == null)
{
    sb.AppendLine($"Could not open/create a shared parameter file at '{app.SharedParametersFilename}'.");
}
else
{
    var group = defFile.Groups.get_Item(groupName) ?? defFile.Groups.Create(groupName);
    var existingDef = group.Definitions.get_Item(parameterName);

    using (var t = new Transaction(Document, "AJ Tools - Add Project Parameter"))
    {
        t.Start();
        try
        {
            ExternalDefinition definition = existingDef as ExternalDefinition;
            if (definition == null)
            {
                var options = MakeDefOptions(parameterName, "Text");
                definition = group.Definitions.Create(options) as ExternalDefinition;
            }

            var catSet = app.Create.NewCategorySet();
            var addedCategories = new List<string>();
            foreach (var bic in targetCategories)
            {
                var cat = Document.Settings.Categories.get_Item(bic);
                if (cat != null) { catSet.Insert(cat); addedCategories.Add(cat.Name); }
            }

            if (catSet.IsEmpty)
            {
                t.RollBack();
                sb.AppendLine("None of the given categories resolved to a real Category — nothing bound.");
            }
            else
            {
                ElementBinding binding = isInstanceParameter
                    ? (ElementBinding)app.Create.NewInstanceBinding(catSet)
                    : app.Create.NewTypeBinding(catSet);

                bool inserted = BindParam("Insert", definition, binding, "PG_DATA");
                if (!inserted) inserted = BindParam("ReInsert", definition, binding, "PG_DATA");

                t.Commit();
                sb.AppendLine($"{(inserted ? "Bound" : "FAILED to bind")} parameter '{parameterName}' ({(isInstanceParameter ? "Instance" : "Type")}) to categories: {string.Join(", ", addedCategories)}.");
            }
        }
        catch (Exception ex)
        {
            t.RollBack();
            sb.AppendLine($"FAILED to add project parameter — rolled back, nothing changed. Reason: {ex.Message}");
        }
    }
}
return sb.ToString();
