import { callBridge } from "../bridge-connection.js";
import { asToolResult } from "../shared/tool-result.js";
import { defineTool } from "../shared/register.js";

export function register(server) {
  defineTool(server,
    "ping",
    "Check whether Revit is open and the AJ AI bridge is connected and responding.",
    {},
    async () => {
      try {
        const result = await callBridge('"pong"', false);
        return asToolResult(result);
      } catch (err) {
        return asToolResult({ success: false, error: err.message });
      }
    }
  );
}
