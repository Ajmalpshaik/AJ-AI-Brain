// ============================================================
// FRAGMENT (action) — action-add-project-parameter.cs
// PURPOSE: Create a NEW parameter (via a shared parameter definition) and bind it to one or more
//          categories — "add a project parameter called AJ_Status to Duct Accessories" — genuinely
//          different job from every other parameters-naming/ fragment, which all edit VALUES of a
//          parameter that already exists. This one creates the parameter itself.
// DOES NOT consume `elements` — operates on the document's shared parameter file and category bindings,
//          not a model element set.
// CONFIDENCE NOTE, READ BEFORE USING: written against the modern (~2022+) ForgeTypeId-based API
// (SpecTypeId, GroupTypeId). Older Revit versions use the legacy ParameterType enum and
// BuiltInParameterGroup instead — if this doesn't compile, that's almost certainly why; swap
// SpecTypeId.String.Text for ParameterType.Text and GroupTypeId.Data for BuiltInParameterGroup.PG_DATA
// (or the appropriate group) for your version. This is the second-least-certain fragment in this library
// after action-add-schedule-calculated-field.cs's mode="combined" — real API-surface uncertainty across
// Revit versions, not a guess about behavior.
// GOTCHA: needs Application.SharedParametersFilename already pointing at a real, writable shared parameter
//         .txt file — if none is set, this creates one at sharedParamFileFallbackPath instead of failing.
// NOT YET LIVE-VERIFIED.
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
                var options = new ExternalDefinitionCreationOptions(parameterName, SpecTypeId.String.Text);
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

                bool inserted = Document.ParameterBindings.Insert(definition, binding, GroupTypeId.Data);
                if (!inserted) inserted = Document.ParameterBindings.ReInsert(definition, binding, GroupTypeId.Data);

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
