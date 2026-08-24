// color_by_group — a distinct colour per System Type, Level, Comments, or any parameter he names.
//
// On AGENT-SPEC 11's promotion list since the native tools were first built, and it is the everyday
// half of the colour work: set_color paints one colour on a set, this one splits a set into groups
// and colours each differently, which is what "colour by system" actually means.
//
// DEFAULTS TO "palette" ON PURPOSE. The fragment's random/pastel/neon modes pick a random starting
// hue each run, so the colours cannot be known outside Revit — and ajtools-visual-report requires a
// chart to wear the model's own colours. Only a deterministic mode makes that possible, so the
// deterministic one is the default here rather than something to remember.

import { z } from "zod";
import { runComposed, previewField, defined } from "../shared/fragment-tool.js";
import { defineTool } from "../shared/register.js";

export function register(server) {
  defineTool(server,
    "color_by_group",
    "Give each sub-group within a category its own colour, grouped by the value of any parameter — " +
      "'colour by System Type', 'colour by Level', 'colour by Comments'. Different from set_color, " +
      "which paints ONE colour on everything. The script echoes each group's RGB so a chart can be " +
      "drawn in the model's own colours. Composes filter-by-category-name.cs + action-color-by-group.cs.",
    {
      category: z
        .string()
        .describe("Category display name, in plain words — 'Ducts', 'Pipes', 'Air Terminals'."),
      groupBy: z
        .string()
        .describe("Parameter whose value defines the groups — 'System Type', 'Level', 'Comments', or a BuiltInParameter name such as RBS_SYSTEM_NAME_PARAM."),
      colorMode: z
        .enum(["palette", "gradient", "random", "pastel", "neon"])
        .optional()
        .describe("Defaults to 'palette'. Keep it unless he asks otherwise — palette and gradient are repeatable, the other three pick a random starting hue each run so the colours cannot be reused in a chart."),
      targetViewId: z
        .number()
        .optional()
        .describe("Element Id of the view to colour. Omit for the active view."),
      ...previewField,
    },
    async ({ category, groupBy, colorMode, targetViewId, preview }) =>
      runComposed({
        describe: `Colour ${category} by ${groupBy}`,
        names: ["filters/by-identity/filter-by-category-name.cs", "actions/color-graphics/action-color-by-group.cs"],
        values: defined({
          categoryName: category,
          groupByParameterName: groupBy,
          colorMode: colorMode ?? "palette",
          targetViewIdInt: targetViewId,
        }),
        preview,
      })
  );
}
