// ============================================================
// SCRIPT (context) — harvest-revit-api.cs
// PURPOSE: Dump the ENTIRE Revit API of the running Revit to disk, as a corpus for the SEPARATE API
//          index in `api-index/`. Read-only, changes nothing in the model. Run once per Revit version.
//
// WHY REFLECTION AND NOT revitapidocs.com (Ajmal asked for the site, 2026-08-20):
//   * This reads the RevitAPI.dll THAT IS ACTUALLY LOADED, so what you get is exactly the API your Revit
//     has — not a website's guess at a version. After the 2026-08-20 migration that matters more than
//     ever: the whole point is knowing what exists in 2020 vs 2024 vs 2027.
//   * No scraping, no third-party site, no licence question, and nothing to go stale.
//   * It works offline and needs no download.
//
// WHERE IT WRITES: `api-index/corpus/<Namespace>.md`, one file per namespace, inside the Brain folder.
//          Git-ignored — it is large, version-specific and rebuildable, exactly like `chroma-db/`.
//
// WHAT IT DOES NOT DO: it does not touch the Brain's own index. The corpus feeds a SEPARATE collection
//          (`api-index/index-api.cmd`) that `ask-brain-hybrid` never reads. That separation is the whole
//          reason this is safe to build — see knowledge/revit-api-surface.md for the measured argument.
//
// SIZE: ~1,700 public types and 30,000+ members. Expect a few MB and roughly a minute.
// ============================================================

// ---- INPUTS (edit every time) ----
string outputRoot = @"D:\Ajmal\AJ AI Brain\api-index\corpus";   // must be inside the Brain folder
bool includeUI    = true;      // also harvest RevitAPIUI.dll (TaskDialog, Selection, UIDocument...)
// ----------------------------------

var sb = new System.Text.StringBuilder();

var assemblies = new List<System.Reflection.Assembly> { typeof(Element).Assembly };
if (includeUI)
{
    try { assemblies.Add(typeof(Autodesk.Revit.UI.TaskDialog).Assembly); }
    catch (Exception ex) { sb.AppendLine($"NOTE  RevitAPIUI not harvested — {ex.Message}"); }
}

// XML doc comments sit beside the DLL when Autodesk ships them. Absent is fine: signatures alone are
// still the thing you cannot get wrong, and a missing summary is visible rather than invented.
Func<System.Reflection.Assembly, Dictionary<string, string>> loadDocs = asm =>
{
    var map = new Dictionary<string, string>();
    try
    {
        string xml = System.IO.Path.ChangeExtension(asm.Location, ".xml");
        if (!System.IO.File.Exists(xml)) return map;
        var doc = System.Xml.Linq.XDocument.Load(xml);
        foreach (var m in doc.Descendants("member"))
        {
            var name = (string)m.Attribute("name");
            var summary = m.Element("summary");
            if (name != null && summary != null)
                map[name] = System.Text.RegularExpressions.Regex.Replace(summary.Value, @"\s+", " ").Trim();
        }
    }
    catch { }
    return map;
};

Func<Type, string> pretty = t =>
{
    if (t == null) return "void";
    string n = t.Name;
    if (t.IsGenericType) n = n.Split('`')[0] + "<" + string.Join(", ", t.GetGenericArguments().Select(a => a.Name)) + ">";
    return n;
};

System.IO.Directory.CreateDirectory(outputRoot);
var byNamespace = new Dictionary<string, System.Text.StringBuilder>();
int typeCount = 0, memberCount = 0, docHits = 0;

foreach (var asm in assemblies)
{
    var docs = loadDocs(asm);
    Type[] types;
    try { types = asm.GetExportedTypes(); }
    catch (Exception ex) { sb.AppendLine($"SKIP  {asm.GetName().Name}: {ex.Message}"); continue; }

    foreach (var t in types.OrderBy(x => x.FullName))
    {
        string ns = string.IsNullOrEmpty(t.Namespace) ? "(global)" : t.Namespace;
        if (!byNamespace.ContainsKey(ns)) byNamespace[ns] = new System.Text.StringBuilder($"# {ns}\n");
        var into = byNamespace[ns];
        typeCount++;

        string kind = t.IsEnum ? "enum" : t.IsInterface ? "interface" : t.IsValueType ? "struct" : "class";
        into.AppendLine();
        into.AppendLine($"## {t.Name}  ({kind})");
        string tdoc; if (docs.TryGetValue("T:" + t.FullName, out tdoc)) { into.AppendLine(tdoc); docHits++; }
        if (t.BaseType != null && t.BaseType != typeof(object) && !t.IsEnum)
            into.AppendLine($"Inherits: {pretty(t.BaseType)}");
        into.AppendLine($"Full name: `{t.FullName}`");

        try
        {
            if (t.IsEnum)
            {
                var names = Enum.GetNames(t);
                memberCount += names.Length;
                into.AppendLine($"Values ({names.Length}): " + string.Join(", ", names));
            }
            else
            {
                var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
                          | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly;

                var props = t.GetProperties(flags).OrderBy(p => p.Name).ToList();
                if (props.Count > 0)
                {
                    into.AppendLine();
                    into.AppendLine("Properties:");
                    foreach (var p in props)
                    {
                        memberCount++;
                        string d; docs.TryGetValue($"P:{t.FullName}.{p.Name}", out d);
                        into.AppendLine($"- `{pretty(p.PropertyType)} {p.Name}`"
                            + (p.CanWrite ? " (get/set)" : " (get)")
                            + (string.IsNullOrEmpty(d) ? "" : " — " + d));
                        if (!string.IsNullOrEmpty(d)) docHits++;
                    }
                }

                var methods = t.GetMethods(flags).Where(m => !m.IsSpecialName).OrderBy(m => m.Name).ToList();
                if (methods.Count > 0)
                {
                    into.AppendLine();
                    into.AppendLine("Methods:");
                    foreach (var m in methods)
                    {
                        memberCount++;
                        string args = string.Join(", ", m.GetParameters().Select(pa => pretty(pa.ParameterType) + " " + pa.Name));
                        string d; docs.TryGetValue($"M:{t.FullName}.{m.Name}", out d);
                        into.AppendLine($"- `{pretty(m.ReturnType)} {m.Name}({args})`"
                            + (string.IsNullOrEmpty(d) ? "" : " — " + d));
                        if (!string.IsNullOrEmpty(d)) docHits++;
                    }
                }
            }
        }
        catch (Exception ex) { into.AppendLine($"- (members unreadable: {ex.Message})"); }
    }
}

int written = 0; long bytes = 0;
foreach (var kv in byNamespace)
{
    string safe = kv.Key.Replace(".", "-");
    string path = System.IO.Path.Combine(outputRoot, safe + ".md");
    System.IO.File.WriteAllText(path, kv.Value.ToString(), new System.Text.UTF8Encoding(false));
    written++; bytes += new System.IO.FileInfo(path).Length;
}

sb.AppendLine($"Harvested Revit {Application.VersionNumber} ({Application.VersionName}, build {Application.VersionBuild})");
sb.AppendLine($"  assemblies : {string.Join(", ", assemblies.Select(a => a.GetName().Name))}");
sb.AppendLine($"  types      : {typeCount}");
sb.AppendLine($"  members    : {memberCount}");
sb.AppendLine($"  with docs  : {docHits}" + (docHits == 0 ? "   <- no RevitAPI.xml beside the DLL; signatures only, which is still the part you cannot guess" : ""));
sb.AppendLine($"  files      : {written} namespace file(s), {bytes / 1024 / 1024.0:F1} MB");
sb.AppendLine($"  written to : {outputRoot}");
sb.AppendLine();
sb.AppendLine("Next: api-index\\index-api.cmd   (builds the SEPARATE collection — the Brain's own search never reads it)");
return sb.ToString();
