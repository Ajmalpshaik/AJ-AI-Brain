// grayout — Ajmal's own drawing standard, as one call.
//
// "Do the grayout" / "do the grayout for MEP" is a phrase he actually says, and START-HERE.md's
// routing table settles the question this tool exists to remove: "his own standard, values already
// settled; don't re-ask them."
//
// The fragment behind it declares THIRTY-THREE inputs — every grey, pen weight and transparency in
// the standard. Every one of them is already the right value, so the old flow was: find the recipe,
// read 33 declarations, change none of them, and paste it. This collapses that to naming a view, or
// naming nothing at all.
//
// It carries no C#. scripts/recipes/mep-grayout.cs stays the one copy of the standard, so changing
// the standard means editing the fragment and this tool follows automatically.

import { z } from "zod";
import { runComposed, previewField, defined } from "../shared/fragment-tool.js";

export function register(server) {
  server.tool(
    "grayout",
    "Apply Ajmal's MEP grayout standard to a view: architectural/structural background drops to flat " +
      "grey, services come forward in black, insulation reads as a quiet dashed wrapper. His own " +
      "settled standard — every colour, pen weight and transparency is already correct, so do NOT " +
      "ask him for them. Runs scripts/recipes/mep-grayout.cs unchanged.",
    {
      targetViewId: z
        .number()
        .optional()
        .describe("Element Id of the view to grey out. Omit for the active view. Can target a view not currently open."),
      ...previewField,
    },
    async ({ targetViewId, preview }) =>
      runComposed({
        describe: "Apply the MEP grayout standard to this view",
        names: ["recipes/mep-grayout.cs"],
        values: defined({ targetViewIdInt: targetViewId }),
        preview,
      })
  );
}
