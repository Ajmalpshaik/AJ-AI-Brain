// report_length_by_size — "how many ducts, what size, and how much of each?"
//
// AGENT-SPEC 9.4 lists this as one of the standing request shapes, and it was the only one on that
// list with no native tool: count and size were covered by count_elements, but total LENGTH per size
// was not, and that is the number a take-off actually needs.
//
// Two proven fragments composed the way scripts/README.md prescribes — the display-name filter, so
// Ajmal's own word for the category ("Ducts", "Duct Accessories") is what goes in, then the reporter.

import { z } from "zod";
import { runComposed, previewField, defined } from "../shared/fragment-tool.js";
import { defineTool } from "../shared/register.js";

export function register(server) {
  defineTool(server,
    "report_length_by_size",
    "Count AND total length per size group for linear MEP elements — ducts, pipes, cable trays, " +
      "anything with a Size and a Length. This is the take-off answer: count_elements gives the " +
      "number, this gives metres per size. Read-only. Composes filter-by-category-name.cs + " +
      "action-report-length-by-size.cs.",
    {
      category: z
        .string()
        .describe("Category display name, in plain words — 'Ducts', 'Pipes', 'Cable Trays'."),
      sizeParameter: z
        .string()
        .optional()
        .describe("Override the size parameter as a C# BuiltInParameter expression. Omit for the proven default (RBS_CALCULATED_SIZE)."),
      lengthParameter: z
        .string()
        .optional()
        .describe("Override the length parameter as a C# BuiltInParameter expression. Omit for the proven default (CURVE_ELEM_LENGTH)."),
      ...previewField,
    },
    async ({ category, sizeParameter, lengthParameter, preview }) =>
      runComposed({
        describe: `Report ${category} count and length by size`,
        names: ["filters/by-identity/filter-by-category-name.cs", "actions/reporting/action-report-length-by-size.cs"],
        values: defined({ categoryName: category, sizeParam: sizeParameter, lengthParam: lengthParameter }),
        preview,
      })
  );
}
