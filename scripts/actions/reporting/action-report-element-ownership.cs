// ============================================================
// FRAGMENT (action) — action-report-element-ownership.cs
// PURPOSE: Worksharing ownership report for `elements` — who created each element, who owns/borrows it
//          right now, who changed it last (the Worksharing tooltip, in bulk). The "can I edit these, or
//          will Revit ask someone to relinquish" answer.
// ASSUMES: elements (List<Element>) and sb (StringBuilder) exist from a filter above.
// NOT STANDALONE — see scripts/README.md for how to compose. Read-only, model never changes.
// GOTCHA: CheckoutStatus reflects the LAST sync — someone may have borrowed an element since; treat
//         "not owned" as "probably free", not a guarantee (fresh-reads rule applies to people too).
// VERIFIED LIVE 2026-08-07 — evidence in this fragment's row in scripts/README.md. Header corrected
//          2026-08-22; until then it still read "BLOCKED (model isn't workshared) — graceful path only; NOT YET
//          LIVE-VERIFIED (created 2026-07-26 from the round-2 suggestions).", which the README had already
//          superseded. The verification was recorded there and never here.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
int maxRows = 50;   // detail rows cap; the summary always covers everything
// ---- END INPUTS ----

if (!Document.IsWorkshared)
{
    sb.AppendLine("This model is not workshared — every element is freely editable, no ownership exists.");
}
else if (elements.Count == 0)
{
    sb.AppendLine("No elements to report on.");
}
else
{
    int mine = 0, others = 0, free = 0, stale = 0, gone = 0;
    var rows = new List<string>();

    foreach (var el in elements)
    {
        try
        {
            var status = WorksharingUtils.GetCheckoutStatus(Document, el.Id);
            var info = WorksharingUtils.GetWorksharingTooltipInfo(Document, el.Id);
            if (status == CheckoutStatus.OwnedByCurrentUser) mine++;
            else if (status == CheckoutStatus.OwnedByOtherUser) others++;
            else free++;

            // ✱✱ ADDED 2026-08-24 — THE SECOND HALF OF THE SAFETY QUESTION, AND IT WAS MISSING.
            //    Checkout status answers "may I edit this". `GetModelUpdatesStatus` answers the other
            //    one: "is what I am looking at still what is in the central". An element can be FREE
            //    to edit and already changed — or DELETED — by somebody who synced after your last
            //    reload, and editing it then means your change is built on a stale copy and will
            //    conflict at sync. Nothing in this library asked that before.
            string update = "";
            try
            {
                var upd = WorksharingUtils.GetModelUpdatesStatus(Document, el.Id);
                if (upd == ModelUpdatesStatus.UpdatedInCentral) { stale++; update = "; ** CHANGED IN CENTRAL SINCE YOUR LAST RELOAD **"; }
                else if (upd == ModelUpdatesStatus.DeletedInCentral) { gone++; update = "; ** DELETED IN CENTRAL **"; }
            }
            catch { }

            if (rows.Count < maxRows)
                rows.Add($"  - Id {el.Id} '{el.Name}' — {status}; created by {info.Creator}, owner: {(string.IsNullOrEmpty(info.Owner) ? "(none)" : info.Owner)}, last changed by {info.LastChangedBy}{update}");
        }
        catch (Exception exOne)
        {
            rows.Add($"  - Id {el.Id}: lookup failed — {exOne.Message}");
        }
    }

    sb.AppendLine($"Ownership of {elements.Count} element(s): {free} free, {mine} owned by me, {others} owned by OTHERS (those will prompt for relinquish).");
    if (stale > 0 || gone > 0)
    {
        sb.AppendLine($"*** RELOAD LATEST BEFORE EDITING: {stale} element(s) have CHANGED in the central and {gone} have been DELETED there since your last reload.");
        sb.AppendLine("    A free-to-edit element can still be stale. Editing one now builds on an old copy and conflicts at sync.");
    }
    foreach (var r in rows) sb.AppendLine(r);
    if (elements.Count > maxRows) sb.AppendLine($"  ... detail capped at {maxRows} rows; the summary line covers all {elements.Count}.");
}
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
