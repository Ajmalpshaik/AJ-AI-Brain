import { z } from "zod";
import { filterFields, buildElementsClause, runGenerated } from "../shared/element-filter.js";
import { mmToFtExpr } from "../shared/units.js";
import { defineTool } from "../shared/register.js";

export function register(server) {
  defineTool(server,
    "move_elements",
    "Translate matching elements by one offset vector (mm).",
    { ...filterFields, offsetXmm: z.number().default(0), offsetYmm: z.number().default(0), offsetZmm: z.number().default(0) },
    async ({ offsetXmm, offsetYmm, offsetZmm, ...filter }) => {
      const script = [
        buildElementsClause(filter),
        `XYZ __t = new XYZ(${mmToFtExpr(offsetXmm)}, ${mmToFtExpr(offsetYmm)}, ${mmToFtExpr(offsetZmm)});`,
        `int __moved = 0, __skipped = 0;`,
        `using (var t = new Transaction(Document, "AJ AI - Move Elements")) {`,
        `  t.Start();`,
        `  try { foreach (var e in elements) { try { ElementTransformUtils.MoveElement(Document, e.Id, __t); __moved++; } catch { __skipped++; } } t.Commit(); sb.AppendLine("Moved " + __moved + " element(s), skipped " + __skipped + "."); }`,
        `  catch (Exception ex) { t.RollBack(); sb.AppendLine("FAILED: " + ex.Message); }`,
        `}`,
        `return sb.ToString();`,
      ].join("\n");
      return runGenerated(script);
    }
  );
}
