// verify_connectivity — is the ductwork REALLY still joined up?
//
// It has its own skill (ajtools-mep-connectivity-verify) and is named across the skills more than
// almost any other recipe, because it answers a question Revit itself answers wrongly: the fragment
// NEVER stops at a terminal's own IsConnected flag, which reflects only the local link and not the
// full path out to the FCU. That is the whole reason the recipe exists, and the reason a native tool
// for it is worth more than the tokens it saves — it makes the correct method the easy one.

import { z } from "zod";
import { runComposed, previewField, defined } from "../shared/fragment-tool.js";

export function register(server) {
  server.tool(
    "verify_connectivity",
    "Trace every air terminal's full connector chain out to its FCU (riser → elbow → branch → takeoff " +
      "→ main trunk → FCU) and report exactly where any silent break is. Read-only. Use this to check " +
      "ductwork already built is still fully connected — it deliberately does NOT trust Revit's own " +
      "IsConnected flag, which only describes the local link. Runs scripts/recipes/verify-duct-connectivity.cs.",
    {
      roomId: z
        .number()
        .optional()
        .describe("Element Id of one room to check. Omit to check the whole model."),
      maxHops: z
        .number()
        .int()
        .optional()
        .describe("How far to walk each chain before giving up. Omit for the fragment's proven value."),
      ...previewField,
    },
    async ({ roomId, maxHops, preview }) =>
      runComposed({
        describe: "Check the ductwork is still fully connected",
        names: ["recipes/verify-duct-connectivity.cs"],
        values: defined({ roomId, maxHops }),
        preview,
      })
  );
}
