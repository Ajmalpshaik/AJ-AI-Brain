// ============================================================
// FRAGMENT (action) — action-report-connectors.cs
// PURPOSE: Report every MEP connector on the elements in `elements` — domain, shape, size (mm), origin
//          (mm), facing direction (BasisZ), and what each is REALLY connected to. This packages step 1
//          of the user's own connection method (check connectors exist → domain/size → real direction →
//          only then draw) as one reusable read — see ../../../knowledge/live-model/hvac-ducts.md.
// ASSUMES: elements (List<Element>, MEPCurves and/or FamilyInstances with MEP connectors) and sb exist
//          from a filter above.
// NOT STANDALONE — see scripts/README.md for how to compose. Read-only, model never changes.
// GOTCHA: IsConnected can be true with no physical partner (proven wrong in real sessions) — that's why
//         the report also lists the actual connected refs' owner elements; trust the refs, not the flag.
// VERIFIED LIVE 2026-08-06 — evidence in this fragment's row in scripts/README.md. Header corrected
//          2026-08-22; until then it still read "NOT YET LIVE-VERIFIED as a fragment — the underlying reads are the
//          live-proven core of connect-equipment-to-air-terminals.cs (2026-07-26); packaged 2026-07-26, round 3.",
//          which the README had already superseded. The verification was recorded there and never here.
// ============================================================

// ---- INPUTS (edit every time — never treat these as fixed defaults) ----
bool onlyOpenConnectors = false;   // true = report only unconnected connectors (the "open ends" question)
// ---- END INPUTS ----

Func<double, double> toMm = v => v * 304.8;
Func<XYZ, string> fmtPt = p => $"({toMm(p.X):F0}, {toMm(p.Y):F0}, {toMm(p.Z):F0})";
Func<XYZ, string> fmtDir = d => $"({d.X:F2}, {d.Y:F2}, {d.Z:F2})";

int reported = 0, noConnectorElements = 0;

foreach (var el in elements)
{
    ConnectorManager cm = null;
    var mepCurve = el as MEPCurve;
    if (mepCurve != null) cm = mepCurve.ConnectorManager;
    else
    {
        var fi = el as FamilyInstance;
        if (fi != null && fi.MEPModel != null) cm = fi.MEPModel.ConnectorManager;
    }

    if (cm == null) { noConnectorElements++; continue; }

    var lines = new List<string>();
    foreach (Connector c in cm.Connectors)
    {
        bool physicallyConnected = false;
        var partners = new List<string>();
        foreach (Connector refC in c.AllRefs)
        {
            // skip logical system refs — only physical partners count (the trace technique's rule)
            if (refC.Owner == null || refC.Owner.Id == el.Id) continue;
            if (refC.Owner is MEPSystem) continue;
            physicallyConnected = true;
            partners.Add($"{refC.Owner.Name} (Id {refC.Owner.Id})");
        }

        if (onlyOpenConnectors && physicallyConnected) continue;

        string size;
        try
        {
            size = c.Shape == ConnectorProfileType.Round
                ? $"dia {toMm(c.Radius * 2):F0} mm"
                : $"{toMm(c.Width):F0}x{toMm(c.Height):F0} mm";
        }
        catch { size = "(no size)"; }

        lines.Add($"    [{c.Id}] {c.Domain}, {c.Shape}, {size}, origin {fmtPt(c.Origin)}, facing {fmtDir(c.CoordinateSystem.BasisZ)}, " +
            (physicallyConnected ? $"CONNECTED to {string.Join(" + ", partners)}" : "OPEN") +
            $" (IsConnected flag: {c.IsConnected})");
    }

    if (lines.Count > 0)
    {
        sb.AppendLine($"- '{el.Name}' (Id {el.Id}) — {lines.Count} connector(s){(onlyOpenConnectors ? " open" : "")}:");
        foreach (var l in lines) sb.AppendLine(l);
        reported++;
    }
}

sb.AppendLine($"Connector report: {reported} element(s) shown{(onlyOpenConnectors ? " (open connectors only)" : "")}, {noConnectorElements} of {elements.Count} had no MEP connectors.");
// ---- continue with another action fragment below, or add return sb.ToString(); to finish ----
