// AJ Tools AJ AI Bridge MCP server — entry point.
//
// Bridges an MCP client (an AI agent) to a live Revit session. AJ Tools hosts a local named-pipe
// server (McpBridgeService, inside the AiShell module) that this script talks to whenever the
// "Connect AJ AI Bridge" toggle in the AJ AI pane is switched on.
//
// This file only wires things together. See:
//   - bridge-connection.js  — the named-pipe plumbing
//   - shared/               — helpers every native tool reuses
//   - tools/README.md       — index of all 17 Revit tools, one file each
//   - brain-tools/          — tools that do NOT touch Revit (search_brain), kept out of tools/
//                             so brain-status.mjs keeps counting Revit tools truthfully

import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";

import { register as registerRunCsharp } from "./tools/run-csharp.js";
import { register as registerPing } from "./tools/ping.js";
import { register as registerListRevitInstances } from "./tools/list-revit-instances.js";
import { register as registerUseRevitInstance } from "./tools/use-revit-instance.js";
import { register as registerModelSummary } from "./tools/model-summary.js";
import { register as registerListElements } from "./tools/list-elements.js";
import { register as registerCountElements } from "./tools/count-elements.js";
import { register as registerHideElements } from "./tools/hide-elements.js";
import { register as registerUnhideElements } from "./tools/unhide-elements.js";
import { register as registerIsolateElements } from "./tools/isolate-elements.js";
import { register as registerResetIsolation } from "./tools/reset-isolation.js";
import { register as registerSetColor } from "./tools/set-color.js";
import { register as registerResetGraphicOverrides } from "./tools/reset-graphic-overrides.js";
import { register as registerSetTransparency } from "./tools/set-transparency.js";
import { register as registerSelectElements } from "./tools/select-elements.js";
import { register as registerSetParameterValue } from "./tools/set-parameter-value.js";
import { register as registerReportParameters } from "./tools/report-parameters.js";
import { register as registerMoveElements } from "./tools/move-elements.js";
import { register as registerDeleteElements } from "./tools/delete-elements.js";

// Not Revit tools — see brain-tools/search-brain.js for why they live outside tools/.
import { register as registerSearchBrain } from "./brain-tools/search-brain.js";
import { register as registerSearchGraph } from "./brain-tools/search-graph.js";

const server = new McpServer({ name: "aj-tools-aj-ai", version: "1.4.0" });

registerRunCsharp(server);
registerPing(server);
registerListRevitInstances(server);
registerUseRevitInstance(server);
registerModelSummary(server);
registerListElements(server);
registerCountElements(server);
registerHideElements(server);
registerUnhideElements(server);
registerIsolateElements(server);
registerResetIsolation(server);
registerSetColor(server);
registerResetGraphicOverrides(server);
registerSetTransparency(server);
registerSelectElements(server);
registerSetParameterValue(server);
registerReportParameters(server);
registerMoveElements(server);
registerDeleteElements(server);

registerSearchBrain(server);
registerSearchGraph(server);

const transport = new StdioServerTransport();
await server.connect(transport);
