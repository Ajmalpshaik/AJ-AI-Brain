// One tool per file, like every other module here - brain-status.mjs counts .js files in this folder and
// reports that as the native-tool count, so two tools in one file would quietly under-report the Brain.
//
// WHY THESE TWO TOOLS EXIST: the bridge used to assume exactly one Revit. Every session hosted the same
// named pipe, "AJTools.AjAi", with room for two server instances - one servicing the chat, one already
// listening so preemption is instant. A single Revit therefore used BOTH, and a second Revit's pipe threw
// "All pipe instances are busy" (measured 2026-08-20, not inferred). Each Revit now hosts its own pipe,
// named by process id, and advertises itself in %APPDATA%\AJTools\bridges\<pid>.json.

import { listBridgeInstances, describeInstance, getSelectedPid } from "../bridge-connection.js";
import { asToolResult } from "../shared/tool-result.js";

export function register(server) {
  server.tool(
    "list_revit_instances",
    "List every Revit session currently connected to the AJ AI bridge, with the document each one has " +
      "open. Use this when more than one Revit is running, or when another call reports that several " +
      "are connected, so Ajmal can be shown the choice in words he recognises.",
    {},
    async () => {
      try {
        const instances = listBridgeInstances();
        if (instances.length === 0) {
          return asToolResult({
            success: false,
            error:
              'No Revit is connected. In Revit, open the AJ AI pane and click "Connect AJ AI Bridge".',
          });
        }

        const selected = getSelectedPid();
        return asToolResult({
          success: true,
          output: {
            count: instances.length,
            selectedPid: selected,
            instances: instances.map((i) => ({
              pid: i.pid,
              label: describeInstance(i),
              revitVersion: i.revitVersion || "",
              windowTitle: i.windowTitle || "",
              startedUtc: i.startedUtc || "",
              inUse: selected !== null && Number(i.pid) === Number(selected),
            })),
          },
        });
      } catch (err) {
        return asToolResult({ success: false, error: err.message });
      }
    }
  );
}
