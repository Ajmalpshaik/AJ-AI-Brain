// Pick WHICH PROJECT inside the chosen Revit a script acts on.
//
// WHY THIS EXISTS, and why it is a separate problem from use_revit_instance: choosing the Revit is only
// half of it. One Revit can hold several open projects, and on the Revit side `Document` is
// `ActiveUIDocument.Document` - the front window. That changes the moment Ajmal clicks another project,
// so a multi-step job can start in one project and finish in another with nothing to warn either of us.
// Long-standing behaviour, recorded before this fix as "the bridge writes to whatever Revit has in
// FRONT and it changes between calls".
//
// Pinning a title makes every later call name that project explicitly. A title that is not open is an
// ERROR from Revit listing what IS open - never a quiet fall back to the front window, because falling
// back is exactly the failure being prevented.
//
// Pass no title (or an empty one) to go back to "whichever project is in front".

import { z } from "zod";
import { callBridge, selectDocument, getSelectedDocument } from "../bridge-connection.js";
import { asToolResult } from "../shared/tool-result.js";
import { defineTool } from "../shared/register.js";

export function register(server) {
  defineTool(server,
    "use_revit_document",
    "Pin this chat to ONE open project inside the Revit session already selected, by its title (the " +
      "file name without .rvt). Every later command acts on that project instead of whichever window " +
      "is in front. Call with no title to go back to the front window. Use this whenever one Revit has " +
      "more than one project open - ask Ajmal which he means rather than choosing for him.",
    {
      title: z
        .string()
        .optional()
        .describe(
          "Title of the open project, e.g. \"school\". Omit or leave empty to follow the front window again."
        ),
    },
    async ({ title }) => {
      try {
        const wanted = title && title.trim() ? title.trim() : null;

        if (!wanted) {
          selectDocument(null);
          return asToolResult({
            success: true,
            output: "Unpinned. Commands now act on whichever project is in front in that Revit.",
          });
        }

        // Pin first, then let Revit answer whether it is really open - Revit is the only thing that
        // knows. If it is not open the error names every project that IS, and the pin is rolled back so
        // the chat is not left aimed at something unreachable.
        const previous = getSelectedDocument();
        selectDocument(wanted);
        const check = await callBridge("Document.Title", false);

        const text = JSON.stringify(check ?? "");
        if (/No open project called/i.test(text)) {
          selectDocument(previous);
          return asToolResult({ success: false, error: check?.Error || text });
        }

        return asToolResult({
          success: true,
          output:
            `Now working in "${wanted}". Every command in this chat acts on that project ` +
            "until you switch, whichever window happens to be in front.",
        });
      } catch (err) {
        return asToolResult({ success: false, error: err.message });
      }
    }
  );
}
