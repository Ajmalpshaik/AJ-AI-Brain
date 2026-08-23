// ============================================================
// FRAGMENT (action) — action-report-addin-data.cs
// PURPOSE: What OTHER add-ins have written into this model, and onto which elements. Lists every
//          Extensible Storage schema in the file — name, vendor, its fields, whether we are allowed to
//          read it — and counts the elements carrying each one. The honest answer to "there is data on
//          this element that is not a parameter", "which tool stamped these", and "what comes with this
//          file that is not mine".
// UNLIKE OTHER ACTIONS HERE: does NOT consume `elements` — self-contained (declares its own `sb`, ends
//          with its own `return`). READ-ONLY: reads schema definitions and counts carriers, and never
//          writes, changes or clears anything.
//
// ✱✱ EXTENSIBLE STORAGE IS THE FOURTH PLACE DATA HIDES, and this library was only looking in three.
//    Parameters, project information and element properties are visible in Revit's own UI. Extensible
//    Storage is not: an add-in defines a Schema (a shape, identified by a GUID), attaches an Entity of
//    that shape to an element or to an invisible DataStorage element, and the value is then inside the
//    .rvt with nothing in the interface showing it. That is where a tool records "I processed this",
//    where a manufacturer's add-in keeps a product code, and where a coordination tool keeps its ids.
//    Schema.ListSchemas() is the way in; it returns every schema REGISTERED IN THE SESSION.
//
// ✱✱ A SCHEMA CAN REFUSE TO BE READ, AND THAT IS BY DESIGN, NOT AN ERROR. Each one carries a read and a
//    write access level — Public, Vendor or Application. A Vendor-level schema is readable only by code
//    signed with that vendor id, so `schema.ReadAccessGranted()` comes back false here and the FIELDS
//    stay hidden. The schema's existence, name, vendor and carrier count are still visible, which is
//    normally the question anyway ("what is in this file"). DO NOT go looking for a way around that
//    check — the access level is the other vendor's decision about their own data, and defeating it
//    means patching Revit's own memory at run time. This fragment reports the refusal and stops.
//
// ✱✱ SCHEMAS ARE SESSION-WIDE, NOT PER-DOCUMENT. ListSchemas() returns every schema any loaded add-in
//    has registered in this Revit session, including ones with no data in THIS model. The carrier count
//    is what tells the two apart, so a schema with 0 carriers here is listed as "registered, not used in
//    this model" rather than silently dropped.
//
// GOTCHA: counting carriers runs one collector per schema (ExtensibleStorageFilter). On a big model with
//         many schemas that is a real wait — countCarriers = false gives the schema list on its own.
// GOTCHA: document-wide data usually lives on a DataStorage element, not on anything Ajmal can select.
//         Those are counted like any other carrier and named in the per-schema line.
// RELATED: model-health-audit.cs section 10 lists the add-in UPDATERS active in the session — code that
//          is running. This lists the DATA they have left behind.
// ⚠ NOT YET RUN AGAINST A REAL MODEL — written 2026-08-23. Expect most models to show a handful of
//   schemas from whatever add-ins are installed; a file that has never met one will show none.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool countCarriers = true;   // false = list the schemas only, no per-schema collector pass
bool showFields = true;      // false = skip the field list even where reading is allowed
int maxCarrierIdsShown = 10;
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

IList<Autodesk.Revit.DB.ExtensibleStorage.Schema> schemas = null;
try { schemas = Autodesk.Revit.DB.ExtensibleStorage.Schema.ListSchemas(); }
catch (Exception ex)
{
    sb.AppendLine($"Could not list schemas: {ex.Message}");
    return sb.ToString();
}

if (schemas == null || schemas.Count == 0)
{
    sb.AppendLine("No Extensible Storage schemas are registered in this Revit session — no add-in data to report.");
    return sb.ToString();
}

sb.AppendLine($"=== ADD-IN DATA (Extensible Storage) — {schemas.Count} schema(s) registered in this session ===");
sb.AppendLine("Read-only. Nothing was written, changed or cleared.");
sb.AppendLine();

int usedHere = 0;

foreach (var schema in schemas)
{
    string name = "(unnamed)", vendor = "(unknown)", guid = "-", doc = "";
    try { name = schema.SchemaName; } catch { }
    try { vendor = schema.VendorId; } catch { }
    try { guid = schema.GUID.ToString(); } catch { }
    try { doc = schema.Documentation ?? ""; } catch { }

    string readAccess = "?", writeAccess = "?";
    try { readAccess = schema.ReadAccessLevel.ToString(); } catch { }
    try { writeAccess = schema.WriteAccessLevel.ToString(); } catch { }

    bool canRead = false;
    try { canRead = schema.ReadAccessGranted(); } catch { }

    sb.AppendLine($"### {name}");
    sb.AppendLine($"Vendor: {vendor} | GUID: {guid} | Read: {readAccess} | Write: {writeAccess} | Readable by us: {(canRead ? "yes" : "NO — this vendor's data is closed, and that is respected")}");
    if (!string.IsNullOrEmpty(doc)) sb.AppendLine($"Purpose (the author's own words): {doc}");

    // ---- fields, where we are allowed ----
    if (showFields)
    {
        if (!canRead)
        {
            sb.AppendLine("Fields: not shown — read access is not granted for this schema.");
        }
        else
        {
            try
            {
                var fields = schema.ListFields();
                if (fields == null || fields.Count == 0) sb.AppendLine("Fields: none.");
                else
                {
                    sb.AppendLine($"Fields ({fields.Count}):");
                    foreach (var f in fields)
                    {
                        string fname = "?", ftype = "?";
                        try { fname = f.FieldName; } catch { }
                        try { ftype = f.ValueType != null ? f.ValueType.Name : "?"; } catch { }
                        sb.AppendLine($"  {fname} : {ftype}");
                    }
                }
            }
            catch (Exception ex) { sb.AppendLine($"Fields: could not be listed — {ex.Message}"); }
        }
    }

    // ---- who carries it in THIS model ----
    if (countCarriers)
    {
        try
        {
            var carriers = new FilteredElementCollector(Document)
                .WherePasses(new Autodesk.Revit.DB.ExtensibleStorage.ExtensibleStorageFilter(schema.GUID))
                .ToElements();

            if (carriers.Count == 0)
            {
                sb.AppendLine("Carriers in this model: 0 — the schema is registered by an installed add-in but holds no data here.");
            }
            else
            {
                usedHere++;
                // `DataStorage` is a PUBLIC-looking type that is not publicly accessible — naming it in
                // a fragment is a compile error ("inaccessible due to its protection level"), on every
                // Revit here. So the invisible document-wide carrier is recognised by its type NAME
                // rather than by a cast. Same technique as any version-gated member: never name the type.
                var byCat = carriers
                    .GroupBy(e => e.GetType().Name == "DataStorage"
                        ? "DataStorage (document-wide record, not selectable in the model)"
                        : (e.Category != null ? e.Category.Name : e.GetType().Name))
                    .OrderByDescending(g => g.Count());
                sb.AppendLine($"Carriers in this model: {carriers.Count}");
                foreach (var g in byCat)
                {
                    var ids = g.Take(maxCarrierIdsShown).Select(e => e.Id.ToString()).ToList();
                    string more = g.Count() > ids.Count ? $", +{g.Count() - ids.Count} more" : "";
                    sb.AppendLine($"  {g.Key}: {g.Count()} — {string.Join(", ", ids)}{more}");
                }
            }
        }
        catch (Exception ex) { sb.AppendLine($"Carriers: could not be counted — {ex.Message}"); }
    }

    sb.AppendLine();
}

if (countCarriers)
    sb.AppendLine($"{usedHere} of {schemas.Count} schema(s) actually hold data in this model; the rest are registered by installed add-ins only.");

return sb.ToString();
