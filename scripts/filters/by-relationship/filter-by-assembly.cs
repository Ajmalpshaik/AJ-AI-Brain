// ============================================================
// FRAGMENT (filter) — filter-by-assembly.cs
// PURPOSE: Every member element of a specific Revit Assembly (AssemblyInstance) — for prefabrication /
//          assembly-based workflows.
// PRODUCES: elements (List<Element>), sb (StringBuilder, one summary line appended)
// NOT STANDALONE — see scripts/README.md for how to compose with an action fragment.
//          To test this filter alone, add `return sb.ToString();` as your own last line.
// STATUS: not yet live-verified against a real model containing Assemblies.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int assemblyElementIdInt = 0; // set this OR assemblyName below — Id is checked first if both are set
string assemblyName = "";
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();
List<Element> elements = new List<Element>(); // declared outside the branch so it's visible to any action fragment pasted below

AssemblyInstance assembly = null;
if (assemblyElementIdInt != 0)
{
    assembly = Document.GetElement(new ElementId(assemblyElementIdInt)) as AssemblyInstance;
}
if (assembly == null && !string.IsNullOrEmpty(assemblyName))
{
    assembly = new FilteredElementCollector(Document)
        .OfClass(typeof(AssemblyInstance))
        .Cast<AssemblyInstance>()
        .FirstOrDefault(a => a.Name.Equals(assemblyName, StringComparison.OrdinalIgnoreCase));
}

if (assembly == null)
{
    sb.AppendLine("Assembly not found — set assemblyElementIdInt or assemblyName to a real AssemblyInstance.");
}
else
{
    elements = assembly.GetMemberIds()
        .Select(id => Document.GetElement(id))
        .Where(e => e != null)
        .ToList();

    sb.AppendLine($"Filtered {elements.Count} member element(s) of assembly '{assembly.Name}' (Id {assembly.Id}).");
}
// ---- continue with one or more action fragments below, or add return sb.ToString(); to stop here ----
