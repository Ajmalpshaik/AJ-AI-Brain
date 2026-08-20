// Pins the chat to one Revit session. See list-revit-instances.js for why multiple sessions exist.
//
// The rule Ajmal chose, 2026-08-20: ASK, DO NOT GUESS. One Revit open behaves exactly as it always did -
// it is taken automatically and he is never prompted. Two or more, and nothing is sent to Revit until he
// says which. That mirrors his standing rule about pinning the document when two projects are open, where
// the danger is identical: a multi-step job quietly finishing in the wrong project.
//
// Calling this records a DELIBERATE choice, which behaves differently from the client merely taking the
// only session going: his choice survives other Revits opening, and if the session he named closes, the
// bridge stops and says so rather than sliding onto whatever else happens to be running.

import { z } from "zod";
import { selectBridgeInstance, describeInstance } from "../bridge-connection.js";
import { asToolResult } from "../shared/tool-result.js";

export function register(server) {
  server.tool(
    "use_revit_instance",
    "Pin this chat to one Revit session, by its pid from list_revit_instances. Every later command goes " +
      "to that Revit only. Ask Ajmal which session he means before calling this - never choose for him " +
      "when more than one is open.",
    {
      pid: z
        .number()
        .describe("Process id of the Revit session to use, taken from list_revit_instances."),
    },
    async ({ pid }) => {
      try {
        const chosen = selectBridgeInstance(pid);
        return asToolResult({
          success: true,
          output:
            `Now using ${describeInstance(chosen)}. ` +
            "Every command in this chat goes to that Revit until you switch.",
        });
      } catch (err) {
        return asToolResult({ success: false, error: err.message });
      }
    }
  );
}
