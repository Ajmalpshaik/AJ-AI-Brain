// ============================================================
// FRAGMENT (action) — action-transfer-views-between-documents.cs
// PURPOSE: Copy SCHEDULES, LEGENDS, DRAFTING VIEWS or VIEW TEMPLATES from another open Revit document
//          into this one — "bring the standard schedules over from the template project", "copy the
//          legends from last job". Revit's Transfer Project Standards, by script.
// STANDALONE — needs no filter above. Sets its own `sb` and returns.
//
// ✱✱ THE ONE FACT THAT MAKES THIS WORK: the document-to-document `ElementTransformUtils.CopyElements`
//    overload copies the view **SHELL ONLY**. A drafting view or legend arrives EMPTY — right name,
//    right type, nothing drawn in it. Its contents are a SECOND copy, of the elements owned by that
//    view, into the new view. Miss that and the transfer looks like it worked and delivers blank views.
//    Schedules and view templates are definitions rather than drawn content, so they need one copy only.
//
// GOTCHA: BOTH DOCUMENTS MUST BE OPEN IN THE SAME REVIT SESSION. The source is found by document title
//         among `Application.Documents`. If it is not open the fragment lists what IS open and stops.
// ⚠ REAL LIMITATION, NOT A BUG — A DUPLICATE TYPE NAME MAKES THAT VIEW FAIL. Revit resolves a clash
//         (a text type or filled region type of the same name already here) through an
//         `IDuplicateTypeNamesHandler` passed on `CopyPasteOptions`. **That handler has to be a CLASS,
//         and a fragment body cannot declare one** — the bridge wraps every fragment inside a single
//         method, so `class ... : IDuplicateTypeNamesHandler` will not compile here (proved on the
//         compile checker, 2026-08-22). So no handler is set: each view is copied inside its own
//         try/catch and a clashing one is reported by name with the reason, while the rest go through.
//         If a project has many clashes, do that transfer through Revit's own Transfer Project
//         Standards or the add-in's ribbon tool, which can declare the handler. This is the first
//         recorded case of a job the fragment harness structurally cannot do.
// GOTCHA: DRY RUN BY DEFAULT. This adds elements to the current document and there is no partial undo
//         once committed — check the list first.
// GOTCHA: it does NOT skip a view whose name already exists here — Revit will make "Name(1)". Read the
//         dry-run list against your own view names before committing.
// GOTCHA: schedules are filtered to exclude the two internal kinds Revit maintains itself —
//         `IsTitleblockRevisionSchedule` and `IsInternalKeynoteSchedule`. Copying those is meaningless
//         and can misbehave.
// NOTE: `kind = "viewTemplate"` copies the templates themselves (views with IsTemplate == true).
// ⚠ NOT YET RUN AGAINST A REAL MODEL — harvested 2026-08-22 from the add-in's Transfer tools, where the
//   shell-only behaviour is recorded as live-verified. Compile-checked on 2020/2024/2027. This one
//   genuinely needs two open documents to prove, which this Brain's test setup does not have.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
string sourceDocumentTitle = "";     // REQUIRED — the OTHER open document to copy FROM
string kind = "schedule";            // "schedule" | "legend" | "draftingView" | "viewTemplate"
string nameContains = "";            // "" = all of that kind; else only names containing this
bool dryRun = true;                  // true = list what would be copied; false = actually copy
// ---- END INPUTS ----

var sb = new System.Text.StringBuilder();

// ---------- find the source document among the open ones ----------
Document source = null;
var openTitles = new List<string>();
foreach (Document d in Application.Documents)
{
    if (d == null || d.IsLinked) continue;
    openTitles.Add(d.Title);
    if (!string.IsNullOrWhiteSpace(sourceDocumentTitle) &&
        d.Title.IndexOf(sourceDocumentTitle, StringComparison.OrdinalIgnoreCase) >= 0 &&
        !d.Equals(Document))
        source = d;
}

if (string.IsNullOrWhiteSpace(sourceDocumentTitle) || source == null)
{
    sb.AppendLine(string.IsNullOrWhiteSpace(sourceDocumentTitle)
        ? "Set sourceDocumentTitle to the OTHER open document to copy from."
        : $"No open document matching '{sourceDocumentTitle}' (and it must not be this one).");
    sb.AppendLine("Open documents: " + string.Join(" | ", openTitles));
    return sb.ToString();
}

// ---------- collect what to copy ----------
var toCopy = new List<Element>();
string kindLower = kind.ToLowerInvariant();

if (kindLower == "schedule")
{
    toCopy.AddRange(new FilteredElementCollector(source).OfClass(typeof(ViewSchedule)).Cast<ViewSchedule>()
        .Where(v => v != null && !v.IsTemplate &&
                    !v.IsTitleblockRevisionSchedule &&      // Revit's own internal schedules —
                    !v.IsInternalKeynoteSchedule)           // copying them is meaningless
        .OrderBy(v => v.Name).Cast<Element>());
}
else if (kindLower == "viewtemplate")
{
    toCopy.AddRange(new FilteredElementCollector(source).OfClass(typeof(View)).Cast<View>()
        .Where(v => v != null && v.IsTemplate).OrderBy(v => v.Name).Cast<Element>());
}
else
{
    ViewType want = kindLower == "legend" ? ViewType.Legend : ViewType.DraftingView;
    toCopy.AddRange(new FilteredElementCollector(source).OfClass(typeof(View)).Cast<View>()
        .Where(v => v != null && !v.IsTemplate && v.ViewType == want).OrderBy(v => v.Name).Cast<Element>());
}

if (!string.IsNullOrWhiteSpace(nameContains))
    toCopy = toCopy.Where(v => (v.Name ?? "").IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

var existingHere = new HashSet<string>(
    new FilteredElementCollector(Document).OfClass(typeof(View)).Cast<View>()
        .Where(v => v != null).Select(v => v.Name), StringComparer.OrdinalIgnoreCase);

sb.AppendLine((dryRun ? "DRY RUN — nothing copied. " : "") +
              $"{toCopy.Count} {kind}(s) in '{source.Title}'" +
              (string.IsNullOrWhiteSpace(nameContains) ? "" : $" matching '{nameContains}'") + ".");

if (toCopy.Count == 0) return sb.ToString();

int clashes = toCopy.Count(v => existingHere.Contains(v.Name ?? ""));
if (clashes > 0)
    sb.AppendLine($"WARNING: {clashes} of them already exist here by name — Revit will create \"Name(1)\" " +
                  "rather than skipping or replacing. Narrow with nameContains if that is not wanted.");

if (dryRun)
{
    sb.AppendLine("");
    sb.AppendLine("Name | Already here?");
    sb.AppendLine("--- | ---");
    foreach (var v in toCopy.Take(40))
        sb.AppendLine((v.Name ?? "") + " | " + (existingHere.Contains(v.Name ?? "") ? "YES — will duplicate" : "no"));
    if (toCopy.Count > 40) sb.AppendLine("... and " + (toCopy.Count - 40) + " more");
    return sb.ToString();
}

// ---------- copy ----------
bool needsContents = (kindLower == "legend" || kindLower == "draftingview");
int copied = 0, contentsCopied = 0, failed = 0;
var problems = new List<string>();

using (var t = new Transaction(Document, "AJ Tools - Transfer Views Between Documents"))
{
    t.Start();
    try
    {
        // NO IDuplicateTypeNamesHandler — see the header. A fragment body cannot declare the class
        // one would require, so a view whose types clash by name fails on its own and is reported.
        var opts = new CopyPasteOptions();

        foreach (var v in toCopy)
        {
            try
            {
                // 1) the SHELL — this alone leaves a legend/drafting view EMPTY
                var newIds = ElementTransformUtils.CopyElements(
                    source, new List<ElementId> { v.Id }, Document, Transform.Identity, opts);
                if (newIds == null || newIds.Count == 0) { failed++; continue; }
                copied++;

                if (!needsContents) continue;

                // 2) the CONTENTS — the elements OWNED BY that view, into the new view
                var newView = Document.GetElement(newIds.First()) as View;
                if (newView == null) continue;

                var contentIds = new FilteredElementCollector(source, v.Id)
                    .WhereElementIsNotElementType()
                    .ToElementIds()
                    .Where(id => id != v.Id)
                    .ToList();
                if (contentIds.Count == 0) continue;

                try
                {
                    var c = ElementTransformUtils.CopyElements(
                        (View)v, contentIds, newView, Transform.Identity, opts);
                    if (c != null) contentsCopied += c.Count;
                }
                catch (Exception cex)
                {
                    if (problems.Count < 10) problems.Add((v.Name ?? "") + " — shell copied but contents failed: " + cex.Message);
                }
            }
            catch (Exception ex2)
            {
                failed++;
                if (problems.Count < 10)
                    problems.Add((v.Name ?? "") + " — " + ex2.Message +
                        (ex2.Message.IndexOf("type", StringComparison.OrdinalIgnoreCase) >= 0
                            ? "  [likely a DUPLICATE TYPE NAME — see the header; use Revit's own Transfer Project Standards for this one]"
                            : ""));
            }
        }
        t.Commit();
    }
    catch (Exception ex)
    {
        t.RollBack();
        sb.AppendLine($"FAILED — rolled back, nothing copied. Reason: {ex.Message}");
        return sb.ToString();
    }
}

sb.AppendLine($"Copied {copied} {kind}(s) from '{source.Title}'" +
              (needsContents ? $", plus {contentsCopied} element(s) of their contents" : "") +
              (failed > 0 ? $"; {failed} failed" : "") + ".");
if (needsContents && contentsCopied == 0 && copied > 0)
    sb.AppendLine("NOTE: 0 content elements copied — the views may genuinely be empty, but check one. " +
                  "A shell-only copy is exactly what this fragment exists to avoid.");
if (problems.Count > 0)
{
    sb.AppendLine("Problems:");
    foreach (var p in problems) sb.AppendLine("  - " + p);
}

return sb.ToString();
