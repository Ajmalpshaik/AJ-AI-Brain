// AJ Tools AJ AI Bridge MCP server.
//
// Bridges an MCP client (an AI agent) to a live Revit session. AJ Tools hosts a local named-pipe
// server (McpBridgeService, inside the AiShell module) that this script talks to whenever the
// "Connect AJ AI Bridge" toggle in the AJ AI pane is switched on. The MCP process keeps one
// authenticated pipe connection open between requests, then recreates it when Revit reconnects.
//
// Tools exposed:
//   - run_csharp: run a C# snippet against the open Revit document and return the result.
//   - ping:       quick check that Revit is open and the bridge is listening.
//   - model_summary: fast read-only count, with an optional parameter breakdown.
//   - list_elements, count_elements, hide_elements, unhide_elements, isolate_elements,
//     reset_isolation, set_color, reset_graphic_overrides, set_transparency, select_elements,
//     set_parameter_value, report_parameters, move_elements, delete_elements: typed, schema-validated
//     native tools covering the common daily action set (2026-07-22). Each one generates the same
//     proven C# pattern already used in scripts/filters + scripts/actions and sends it through the
//     same callBridge() pipe mechanism as run_csharp — McpBridgeService.cs (the Revit-side listener)
//     needed NO changes for this; it already accepts any C# generically. These exist so the common
//     ~80% of requests get real MCP-protocol parameter validation instead of AI-composed code text —
//     faster and less error-prone for that common set. run_csharp stays the flexible fallback for
//     anything these don't cover (complex multi-step builds, HVAC/MEP recipes, family authoring).

import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import net from "node:net";
import fs from "node:fs";
import path from "node:path";

const DISCOVERY_FILE = path.join(process.env.APPDATA || "", "AJTools", "ajai-bridge.json");
// RevitExecutionService soft-cancels a loop-based script at 60s, then gives it a further 20s grace
// period to actually unwind before its own hard backstop gives up (see that file's HardWaitTimeout,
// 80s total). This must stay comfortably above that 80s, or a script that's still legitimately
// unwinding gets reported here as "timed out" even though Revit would have finished it normally a
// few seconds later.
const RESPONSE_TIMEOUT_MS = 90_000;
const CONNECT_TIMEOUT_MS = 10_000;

let cachedDiscovery;
let activeConnection;

function readDiscoveryInfo() {
  if (!fs.existsSync(DISCOVERY_FILE)) {
    cachedDiscovery = undefined;
    throw new Error(
      "AJ AI Bridge is not connected. In Revit, open the AJ AI pane and click \"Connect AJ AI Bridge\", then try again."
    );
  }

  const stat = fs.statSync(DISCOVERY_FILE);
  if (
    cachedDiscovery &&
    cachedDiscovery.mtimeMs === stat.mtimeMs &&
    cachedDiscovery.size === stat.size
  ) {
    return cachedDiscovery.info;
  }

  const raw = fs.readFileSync(DISCOVERY_FILE, "utf8");
  const info = JSON.parse(raw);
  if (!info.pipeName || !info.token) {
    throw new Error("AJ AI bridge connection file is malformed. Reconnect from the AJ AI pane in Revit.");
  }

  cachedDiscovery = { mtimeMs: stat.mtimeMs, size: stat.size, info };
  return info;
}

function connectionKey(info) {
  return `${info.pipeName}\u0000${info.token}`;
}

function detachConnection(connection, error) {
  if (!connection || connection.closed) return;

  connection.closed = true;
  if (activeConnection === connection) activeConnection = undefined;

  if (connection.pending) {
    const pending = connection.pending;
    connection.pending = undefined;
    clearTimeout(pending.timer);
    pending.reject(error);
  }
}

function closeConnection(connection, reason) {
  if (!connection || connection.closed) return;

  detachConnection(connection, new Error(reason));
  if (!connection.socket.destroyed) connection.socket.destroy();
}

function createConnection(info) {
  return new Promise((resolve, reject) => {
    const pipePath = `\\\\.\\pipe\\${info.pipeName}`;
    const socket = net.connect({ path: pipePath });
    const connection = {
      key: connectionKey(info),
      socket,
      buffer: "",
      pending: undefined,
      closed: false,
    };

    let connected = false;
    let connectSettled = false;

    const connectTimer = setTimeout(() => {
      if (connectSettled) return;
      connectSettled = true;
      socket.destroy();
      reject(new Error("Timed out connecting to the AJ AI bridge. Is Revit busy or disconnected?"));
    }, CONNECT_TIMEOUT_MS);

    socket.setNoDelay(true);

    socket.once("connect", () => {
      connected = true;
      connectSettled = true;
      clearTimeout(connectTimer);
      activeConnection = connection;
      resolve(connection);
    });

    socket.on("data", (chunk) => {
      connection.buffer += chunk.toString("utf8");

      while (true) {
        const newlineIndex = connection.buffer.indexOf("\n");
        if (newlineIndex === -1) return;

        const line = connection.buffer.slice(0, newlineIndex);
        connection.buffer = connection.buffer.slice(newlineIndex + 1);
        const pending = connection.pending;
        if (!pending) {
          closeConnection(connection, "Received an unexpected AJ AI bridge response.");
          return;
        }

        try {
          const response = JSON.parse(line);
          connection.pending = undefined;
          clearTimeout(pending.timer);
          // Defer by one event-loop turn so a legacy one-request server can close cleanly
          // before the next queued request decides whether to reuse this connection.
          setImmediate(() => pending.resolve(response));
        } catch (err) {
          closeConnection(connection, "Could not parse the AJ AI bridge response: " + err.message);
          return;
        }
      }
    });

    socket.on("error", (err) => {
      const error = err.code === "ENOENT"
        ? new Error("Could not reach the AJ AI bridge (pipe not found). It may have been disconnected or Revit was closed. Reconnect from the AJ AI pane.")
        : err;

      if (!connected && !connectSettled) {
        connectSettled = true;
        clearTimeout(connectTimer);
        reject(error);
      }

      detachConnection(connection, error);
    });

    socket.on("end", () => detachConnection(connection, new Error("The AJ AI bridge closed the pipe connection.")));
    socket.on("close", () => {
      if (!connected && !connectSettled) {
        connectSettled = true;
        clearTimeout(connectTimer);
        reject(new Error("The AJ AI bridge closed before the connection was established."));
      }
      detachConnection(connection, new Error("The AJ AI bridge closed the pipe connection."));
    });
  });
}

async function getConnection(info) {
  const key = connectionKey(info);
  if (
    activeConnection &&
    !activeConnection.closed &&
    !activeConnection.socket.destroyed &&
    activeConnection.socket.writable &&
    activeConnection.key === key
  ) {
    return activeConnection;
  }

  if (activeConnection) {
    closeConnection(activeConnection, "AJ AI bridge connection details changed.");
  }

  return createConnection(info);
}

function sendRequest(connection, info, code, allowDestructive) {
  if (connection.closed || connection.socket.destroyed || !connection.socket.writable) {
    return Promise.reject(new Error("The AJ AI bridge connection is no longer available."));
  }
  if (connection.pending) {
    return Promise.reject(new Error("An AJ AI bridge request is already in progress on this connection."));
  }

  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      if (!connection.pending) return;
      closeConnection(connection, "Timed out waiting for Revit to respond. Is Revit busy or unresponsive?");
    }, RESPONSE_TIMEOUT_MS);

    connection.pending = { resolve, reject, timer };
    try {
      connection.socket.write(JSON.stringify({ token: info.token, code, allowDestructive: !!allowDestructive }) + "\n");
    } catch (err) {
      closeConnection(connection, err.message);
    }
  });
}

async function callBridgeNow(code, allowDestructive) {
  const info = readDiscoveryInfo();
  const connection = await getConnection(info);
  return sendRequest(connection, info, code, allowDestructive);
}

// Revit runs API work on one ExternalEvent at a time. Serializing calls preserves that contract while
// the underlying named-pipe connection stays open between requests.
let bridgeCallQueue = Promise.resolve();

function callBridge(code, allowDestructive) {
  const nextCall = bridgeCallQueue.then(() => callBridgeNow(code, allowDestructive));
  bridgeCallQueue = nextCall.catch(() => undefined);
  return nextCall;
}

function asToolResult(result) {
  return {
    content: [{ type: "text", text: JSON.stringify(result, null, 2) }],
    isError: result?.Success === false || result?.success === false,
  };
}

const MODEL_SUMMARY_TARGETS = {
  ducts: { builtInCategory: "OST_DuctCurves", label: "Duct Curves" },
  flex_ducts: { builtInCategory: "OST_FlexDuctCurves", label: "Flex Ducts" },
  air_terminals: { builtInCategory: "OST_DuctTerminal", label: "Air Terminals" },
  pipes: { builtInCategory: "OST_PipeCurves", label: "Pipes" },
  duct_fittings: { builtInCategory: "OST_DuctFitting", label: "Duct Fittings" },
  pipe_fittings: { builtInCategory: "OST_PipeFitting", label: "Pipe Fittings" },
  mechanical_equipment: { builtInCategory: "OST_MechanicalEquipment", label: "Mechanical Equipment" },
};

function buildModelSummaryScript(target, parameterName) {
  const parameterLiteral = parameterName ? JSON.stringify(parameterName) : "null";

  return `var sb = new System.Text.StringBuilder();
string requestedParameter = ${parameterLiteral};
var groups = new System.Collections.Generic.Dictionary<string, int>();
int count = 0;

foreach (Element element in new FilteredElementCollector(Document)
    .OfCategory(BuiltInCategory.${target.builtInCategory})
    .WhereElementIsNotElementType())
{
    count++;
    if (requestedParameter == null) continue;

    string value = "Unknown";
    var parameter = element.LookupParameter(requestedParameter);
    if (parameter != null)
    {
        if (parameter.StorageType == StorageType.Double)
        {
            double mm = UnitUtils.ConvertFromInternalUnits(
                parameter.AsDouble(), DisplayUnitType.DUT_MILLIMETERS);
            value = Math.Round(mm) + " mm";
        }
        else if (parameter.StorageType == StorageType.String)
        {
            value = parameter.AsString() ?? "(blank)";
        }
        else
        {
            value = parameter.AsValueString() ?? "(not set)";
        }
    }

    if (!groups.ContainsKey(value)) groups[value] = 0;
    groups[value]++;
}

sb.AppendLine("REVIT=" + Application.VersionName);
sb.AppendLine("MODEL=" + Document.Title);
sb.AppendLine("CATEGORY=${target.label}");
sb.AppendLine("COUNT=" + count);

if (requestedParameter != null)
{
    sb.AppendLine("BREAKDOWN_BY=" + requestedParameter);
    var orderedGroups = new System.Collections.Generic.List<
        System.Collections.Generic.KeyValuePair<string, int>>(groups);
    orderedGroups.Sort((left, right) =>
    {
        int quantityOrder = right.Value.CompareTo(left.Value);
        return quantityOrder != 0
            ? quantityOrder
            : string.Compare(left.Key, right.Key, StringComparison.Ordinal);
    });

    foreach (var group in orderedGroups)
        sb.AppendLine("QTY=" + group.Value + " | VALUE=" + group.Key);
}

return sb.ToString();`;
}

const server = new McpServer({ name: "aj-tools-aj-ai", version: "1.3.0" });

server.tool(
  "run_csharp",
  "Run a C# snippet against the currently open Revit document (via AJ Tools' AJ AI bridge). " +
    "Use Document/UIDocument/Application/UIApplication directly by name (same globals as the AJ AI shell). " +
    "The last expression's value (or an explicit 'return' in a script-style block) becomes the output. " +
    "Destructive operations (Delete/Purge/file writes) are refused unless allowDestructive is set to true.",
  {
    code: z.string().describe("C# script to run against the live Revit document."),
    allowDestructive: z
      .boolean()
      .optional()
      .describe("Set true to allow Delete/Purge/file-write operations. Defaults to false."),
  },
  async ({ code, allowDestructive }) => {
    try {
      const result = await callBridge(code, allowDestructive);
      return asToolResult(result);
    } catch (err) {
      return asToolResult({ success: false, error: err.message });
    }
  }
);

server.tool(
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

server.tool(
  "model_summary",
  "Fast, read-only live-model count for a common Revit category. Optionally group the result by one " +
    "parameter, such as duct Height. Use this instead of a separate ping plus generated C# for normal " +
    "'how many' and single-dimension questions.",
  {
    category: z
      .enum(Object.keys(MODEL_SUMMARY_TARGETS))
      .describe("The Revit category to count."),
    parameter: z
      .enum(["Height", "Width", "Diameter", "Size"])
      .optional()
      .describe("Optional parameter to group by. Omit for a count only."),
  },
  async ({ category, parameter }) => {
    try {
      const result = await callBridge(
        buildModelSummaryScript(MODEL_SUMMARY_TARGETS[category], parameter),
        false
      );
      return asToolResult(result);
    } catch (err) {
      return asToolResult({ Success: false, Error: err.message });
    }
  }
);

// ============================================================================
// Native tool set — shared C# generation helpers
//
// Every tool below builds the SAME proven pattern already used in scripts/filters/ + scripts/actions/,
// just assembled from typed, MCP-protocol-validated parameters instead of AI-composed code text. One
// element-resolution clause (by explicit Ids, or by category + optional family/numeric-parameter
// filter) feeds every action. Mirrors the filter+action fragment architecture in scripts/README.md.
// ============================================================================

function cs(value) {
  return JSON.stringify(value ?? null);
}

// Produces C# that declares `List<Element> elements` and `var sb = new System.Text.StringBuilder();`.
// `elementIds`, if given, takes priority over the category filter (caller already knows exactly which
// elements it wants — e.g. chained from a prior list_elements call).
function buildElementsClause(input) {
  const { elementIds, category, familyName, parameterName, comparison, valueMm, valueMaxMm, toleranceMm } = input;

  if (elementIds && elementIds.length > 0) {
    const idArray = elementIds.map((id) => `new ElementId(${Number(id)})`).join(", ");
    return [
      `var __ids = new List<ElementId> { ${idArray} };`,
      `List<Element> elements = __ids.Select(id => Document.GetElement(id)).Where(e => e != null).ToList();`,
      `var sb = new System.Text.StringBuilder();`,
      `sb.AppendLine("Resolved " + elements.Count + " of " + __ids.Count + " given Element Id(s).");`,
    ].join("\n");
  }

  const lines = [
    `Category __category = null;`,
    `foreach (Category __cat in Document.Settings.Categories) { if (__cat.Name.Equals(${cs(category)}, StringComparison.OrdinalIgnoreCase)) { __category = __cat; break; } }`,
    `List<Element> elements = new List<Element>();`,
    `var sb = new System.Text.StringBuilder();`,
    `if (__category == null)`,
    `{`,
    `    sb.AppendLine("Category '" + ${cs(category)} + "' not found — check the exact Revit category display name.");`,
    `}`,
    `else`,
    `{`,
    `    elements = new FilteredElementCollector(Document).OfCategoryId(__category.Id).WhereElementIsNotElementType().ToList();`,
  ];

  if (familyName) {
    lines.push(
      `    elements = elements.Where(e => (e as FamilyInstance)?.Symbol?.Family?.Name?.Equals(${cs(familyName)}, StringComparison.OrdinalIgnoreCase) == true).ToList();`
    );
  }

  if (parameterName) {
    let comparisonExpr;
    switch (comparison) {
      case "gte":
        comparisonExpr = "v >= __valueFt";
        break;
      case "lte":
        comparisonExpr = "v <= __valueFt";
        break;
      case "between":
        comparisonExpr = "v >= __valueFt && v <= __valueMaxFt";
        break;
      default:
        comparisonExpr = "Math.Abs(v - __valueFt) <= __toleranceFt";
    }
    lines.push(
      `    double __valueFt = UnitUtils.ConvertToInternalUnits(${Number(valueMm) || 0}, DisplayUnitType.DUT_MILLIMETERS);`,
      `    double __toleranceFt = UnitUtils.ConvertToInternalUnits(${Number(toleranceMm) || 1}, DisplayUnitType.DUT_MILLIMETERS);`,
      `    double __valueMaxFt = UnitUtils.ConvertToInternalUnits(${Number(valueMaxMm) || 0}, DisplayUnitType.DUT_MILLIMETERS);`,
      `    elements = elements.Where(e => { var p = e.LookupParameter(${cs(parameterName)}); if (p == null || p.StorageType != StorageType.Double) return false; double v = p.AsDouble(); return ${comparisonExpr}; }).ToList();`
    );
  }

  lines.push(
    `    sb.AppendLine("Matched " + elements.Count + " element(s) in '" + __category.Name + "'.");`,
    `}`
  );
  return lines.join("\n");
}

function buildViewClause(targetViewId) {
  return targetViewId
    ? `View view = Document.GetElement(new ElementId(${Number(targetViewId)})) as View;`
    : `View view = Document.ActiveView;`;
}

// Shared zod fields for the element-resolution input every tool below accepts.
const filterFields = {
  elementIds: z
    .array(z.number())
    .optional()
    .describe("Exact Element Ids to target directly — takes priority over category/filter if given."),
  category: z
    .string()
    .optional()
    .describe("Revit category display name, e.g. 'Ducts', 'Walls', 'Air Terminals'. Required unless elementIds is given."),
  familyName: z.string().optional().describe("Narrow to one family within the category."),
  parameterName: z.string().optional().describe("Numeric parameter to filter by, e.g. 'Height', 'Diameter'."),
  comparison: z.enum(["eq", "gte", "lte", "between"]).optional().describe("Defaults to 'eq' if parameterName is set."),
  valueMm: z.number().optional().describe("Comparison value in mm."),
  valueMaxMm: z.number().optional().describe("Upper bound in mm, only used when comparison is 'between'."),
  toleranceMm: z.number().optional().describe("Tolerance in mm, only used when comparison is 'eq'. Defaults to 1mm."),
};

const viewField = {
  targetViewId: z
    .number()
    .optional()
    .describe("Element Id of the view to target. Omit to use the active view. Can target any view, not just what's on screen."),
};

async function runGenerated(script, allowDestructive) {
  try {
    const result = await callBridge(script, !!allowDestructive);
    return asToolResult(result);
  } catch (err) {
    return asToolResult({ success: false, error: err.message });
  }
}

// ---- list_elements ----------------------------------------------------------------------------
server.tool(
  "list_elements",
  "List the actual matching elements (Element Id + Category + Family/Type) for a category/filter, " +
    "or for a given list of Element Ids. Use this instead of count_elements when the user wants the " +
    "real items (e.g. 'show me the 300x300 VCDs'), not just a number — the Element IDs are what let a " +
    "follow-up request act on this exact set.",
  { ...filterFields, maxRows: z.number().optional().describe("Cap the number of rows returned. Defaults to 50.") },
  async ({ maxRows, ...filter }) => {
    const cap = maxRows || 50;
    const script = [
      buildElementsClause(filter),
      `foreach (var e in elements.Take(${cap})) sb.AppendLine("Id " + e.Id.IntegerValue + ": '" + e.Name + "' — " + (e.Category?.Name ?? "(no category)"));`,
      `if (elements.Count > ${cap}) sb.AppendLine("... " + (elements.Count - ${cap}) + " more not shown.");`,
      `return sb.ToString();`,
    ].join("\n");
    return runGenerated(script);
  }
);

// ---- count_elements -----------------------------------------------------------------------------
server.tool(
  "count_elements",
  "Bare count of matching elements for ANY category (not limited to model_summary's fixed list), " +
    "with an optional numeric-parameter filter. Use for plain 'how many' questions.",
  filterFields,
  async (filter) => {
    const script = [buildElementsClause(filter), `sb.AppendLine("Count: " + elements.Count);`, `return sb.ToString();`].join("\n");
    return runGenerated(script);
  }
);

// ---- hide_elements --------------------------------------------------------------------------
server.tool(
  "hide_elements",
  "Hide matching elements in a view — temporary by default (Reset Temporary Hide/Isolate clears it), " +
    "or permanent if requested.",
  { ...filterFields, ...viewField, permanent: z.boolean().optional().describe("true = permanent View.HideElements. Defaults to false (temporary).") },
  async ({ targetViewId, permanent, ...filter }) => {
    const script = [
      buildElementsClause(filter),
      buildViewClause(targetViewId),
      `if (view == null) { sb.AppendLine("Target view not found."); return sb.ToString(); }`,
      `using (var t = new Transaction(Document, "AJ AI - Hide Elements")) {`,
      `  t.Start();`,
      `  try {`,
      `    var ids = elements.Select(e => e.Id).ToList();`,
      `    if (${permanent ? "true" : "false"}) view.HideElements(ids); else view.HideElementsTemporary(ids);`,
      `    t.Commit();`,
      `    sb.AppendLine("Hid " + elements.Count + " element(s) " + (${permanent ? "true" : "false"} ? "permanently" : "temporarily") + " in '" + view.Name + "'.");`,
      `  } catch (Exception ex) { t.RollBack(); sb.AppendLine("FAILED: " + ex.Message); }`,
      `}`,
      `return sb.ToString();`,
    ].join("\n");
    return runGenerated(script);
  }
);

// ---- unhide_elements ------------------------------------------------------------------------
server.tool(
  "unhide_elements",
  "Reverse a PERMANENT per-element hide on matching elements. Not for temporary isolate/hide — use " +
    "reset_isolation for that.",
  { ...filterFields, ...viewField },
  async ({ targetViewId, ...filter }) => {
    const script = [
      buildElementsClause(filter),
      buildViewClause(targetViewId),
      `if (view == null) { sb.AppendLine("Target view not found."); return sb.ToString(); }`,
      `using (var t = new Transaction(Document, "AJ AI - Unhide Elements")) {`,
      `  t.Start();`,
      `  try { view.UnhideElements(elements.Select(e => e.Id).ToList()); t.Commit(); sb.AppendLine("Unhid " + elements.Count + " element(s) in '" + view.Name + "'."); }`,
      `  catch (Exception ex) { t.RollBack(); sb.AppendLine("FAILED: " + ex.Message); }`,
      `}`,
      `return sb.ToString();`,
    ].join("\n");
    return runGenerated(script);
  }
);

// ---- isolate_elements -----------------------------------------------------------------------
server.tool(
  "isolate_elements",
  "Temporary-isolate matching elements in a view, resetting any prior isolation first.",
  { ...filterFields, ...viewField },
  async ({ targetViewId, ...filter }) => {
    const script = [
      buildElementsClause(filter),
      buildViewClause(targetViewId),
      `if (view == null) { sb.AppendLine("Target view not found."); return sb.ToString(); }`,
      `if (!view.CanUseTemporaryVisibilityModes()) { sb.AppendLine("View '" + view.Name + "' does not support temporary isolate."); return sb.ToString(); }`,
      `using (var t = new Transaction(Document, "AJ AI - Isolate Elements")) {`,
      `  t.Start();`,
      `  try {`,
      `    if (view.IsTemporaryHideIsolateActive()) view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);`,
      `    view.IsolateElementsTemporary(elements.Select(e => e.Id).ToList());`,
      `    t.Commit();`,
      `    sb.AppendLine("Isolated " + elements.Count + " element(s) in '" + view.Name + "'.");`,
      `  } catch (Exception ex) { t.RollBack(); sb.AppendLine("FAILED: " + ex.Message); }`,
      `}`,
      `return sb.ToString();`,
    ].join("\n");
    return runGenerated(script);
  }
);

// ---- reset_isolation -------------------------------------------------------------------------
server.tool(
  "reset_isolation",
  "Clear Temporary Hide/Isolate in a view — the direct equivalent of Revit's 'Reset Temporary Hide/Isolate'.",
  viewField,
  async ({ targetViewId }) => {
    const script = [
      buildViewClause(targetViewId),
      `var sb = new System.Text.StringBuilder();`,
      `if (view == null) { sb.AppendLine("Target view not found."); return sb.ToString(); }`,
      `using (var t = new Transaction(Document, "AJ AI - Reset Isolation")) {`,
      `  t.Start();`,
      `  try { if (view.IsTemporaryHideIsolateActive()) view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate); t.Commit(); sb.AppendLine("Reset temporary hide/isolate in '" + view.Name + "'."); }`,
      `  catch (Exception ex) { t.RollBack(); sb.AppendLine("FAILED: " + ex.Message); }`,
      `}`,
      `return sb.ToString();`,
    ].join("\n");
    return runGenerated(script);
  }
);

// ---- set_color -------------------------------------------------------------------------------
server.tool(
  "set_color",
  "Apply one RGB color (line + solid surface fill) to matching elements in a view.",
  { ...filterFields, ...viewField, r: z.number().min(0).max(255).describe("Red 0-255"), g: z.number().min(0).max(255).describe("Green 0-255"), b: z.number().min(0).max(255).describe("Blue 0-255") },
  async ({ targetViewId, r, g, b, ...filter }) => {
    const script = [
      buildElementsClause(filter),
      buildViewClause(targetViewId),
      `if (view == null) { sb.AppendLine("Target view not found."); return sb.ToString(); }`,
      `var __fill = new FilteredElementCollector(Document).OfClass(typeof(FillPatternElement)).Cast<FillPatternElement>().FirstOrDefault(f => f.GetFillPattern().IsSolidFill);`,
      `using (var t = new Transaction(Document, "AJ AI - Set Color")) {`,
      `  t.Start();`,
      `  try {`,
      `    var color = new Autodesk.Revit.DB.Color((byte)${Math.round(r)}, (byte)${Math.round(g)}, (byte)${Math.round(b)});`,
      `    foreach (var e in elements) {`,
      `      var ogs = view.GetElementOverrides(e.Id);`,
      `      ogs.SetProjectionLineColor(color); ogs.SetCutLineColor(color);`,
      `      if (__fill != null) { ogs.SetSurfaceForegroundPatternColor(color); ogs.SetSurfaceForegroundPatternId(__fill.Id); ogs.SetSurfaceForegroundPatternVisible(true); ogs.SetCutForegroundPatternColor(color); ogs.SetCutForegroundPatternId(__fill.Id); ogs.SetCutForegroundPatternVisible(true); }`,
      `      view.SetElementOverrides(e.Id, ogs);`,
      `    }`,
      `    t.Commit();`,
      `    sb.AppendLine("Colored " + elements.Count + " element(s) RGB(${Math.round(r)},${Math.round(g)},${Math.round(b)}) in '" + view.Name + "'.");`,
      `  } catch (Exception ex) { t.RollBack(); sb.AppendLine("FAILED: " + ex.Message); }`,
      `}`,
      `return sb.ToString();`,
    ].join("\n");
    return runGenerated(script);
  }
);

// ---- reset_graphic_overrides -----------------------------------------------------------------
server.tool(
  "reset_graphic_overrides",
  "Clear graphic overrides (color, fill pattern) on matching elements in a view.",
  { ...filterFields, ...viewField },
  async ({ targetViewId, ...filter }) => {
    const script = [
      buildElementsClause(filter),
      buildViewClause(targetViewId),
      `if (view == null) { sb.AppendLine("Target view not found."); return sb.ToString(); }`,
      `using (var t = new Transaction(Document, "AJ AI - Reset Graphic Overrides")) {`,
      `  t.Start();`,
      `  try { var blank = new OverrideGraphicSettings(); foreach (var e in elements) view.SetElementOverrides(e.Id, blank); t.Commit(); sb.AppendLine("Reset overrides on " + elements.Count + " element(s) in '" + view.Name + "'."); }`,
      `  catch (Exception ex) { t.RollBack(); sb.AppendLine("FAILED: " + ex.Message); }`,
      `}`,
      `return sb.ToString();`,
    ].join("\n");
    return runGenerated(script);
  }
);

// ---- set_transparency ------------------------------------------------------------------------
server.tool(
  "set_transparency",
  "Set surface transparency (0-100%) on matching elements in a view.",
  { ...filterFields, ...viewField, percent: z.number().min(0).max(100).describe("0 = opaque, 100 = fully transparent") },
  async ({ targetViewId, percent, ...filter }) => {
    const clamped = Math.max(0, Math.min(100, Math.round(percent)));
    const script = [
      buildElementsClause(filter),
      buildViewClause(targetViewId),
      `if (view == null) { sb.AppendLine("Target view not found."); return sb.ToString(); }`,
      `using (var t = new Transaction(Document, "AJ AI - Set Transparency")) {`,
      `  t.Start();`,
      `  try { foreach (var e in elements) { var ogs = view.GetElementOverrides(e.Id); ogs.SetSurfaceTransparency(${clamped}); view.SetElementOverrides(e.Id, ogs); } t.Commit(); sb.AppendLine("Set ${clamped}% transparency on " + elements.Count + " element(s) in '" + view.Name + "'."); }`,
      `  catch (Exception ex) { t.RollBack(); sb.AppendLine("FAILED: " + ex.Message); }`,
      `}`,
      `return sb.ToString();`,
    ].join("\n");
    return runGenerated(script);
  }
);

// ---- select_elements -------------------------------------------------------------------------
server.tool(
  "select_elements",
  "Set matching elements as the active Revit selection, visible to the user in the UI.",
  filterFields,
  async (filter) => {
    const script = [
      buildElementsClause(filter),
      `UIDocument.Selection.SetElementIds(elements.Select(e => e.Id).ToList());`,
      `sb.AppendLine("Selected " + elements.Count + " element(s) in Revit.");`,
      `return sb.ToString();`,
    ].join("\n");
    return runGenerated(script);
  }
);

// ---- set_parameter_value ---------------------------------------------------------------------
server.tool(
  "set_parameter_value",
  "Bulk-set one named parameter to one value across matching elements. Provide exactly one of " +
    "stringValue or numericValueMm.",
  { ...filterFields, parameterNameToSet: z.string().describe("The parameter to write."), stringValue: z.string().optional(), numericValueMm: z.number().optional() },
  async ({ parameterNameToSet, stringValue, numericValueMm, ...filter }) => {
    const script = [
      buildElementsClause(filter),
      `int __updated = 0, __skipped = 0;`,
      `using (var t = new Transaction(Document, "AJ AI - Set Parameter Value")) {`,
      `  t.Start();`,
      `  try {`,
      `    foreach (var e in elements) {`,
      `      var p = e.LookupParameter(${cs(parameterNameToSet)});`,
      `      if (p == null || p.IsReadOnly) { __skipped++; continue; }`,
      numericValueMm !== undefined && numericValueMm !== null
        ? `      if (p.StorageType == StorageType.Double) { p.Set(UnitUtils.ConvertToInternalUnits(${Number(numericValueMm)}, DisplayUnitType.DUT_MILLIMETERS)); __updated++; } else { __skipped++; }`
        : `      if (p.StorageType == StorageType.String) { p.Set(${cs(stringValue ?? "")}); __updated++; } else { __skipped++; }`,
      `    }`,
      `    t.Commit();`,
      `    sb.AppendLine("Set '" + ${cs(parameterNameToSet)} + "' on " + __updated + " element(s), skipped " + __skipped + ".");`,
      `  } catch (Exception ex) { t.RollBack(); sb.AppendLine("FAILED: " + ex.Message); }`,
      `}`,
      `return sb.ToString();`,
    ].join("\n");
    return runGenerated(script);
  }
);

// ---- report_parameters -----------------------------------------------------------------------
server.tool(
  "report_parameters",
  "Table of named parameter values for matching elements, including each Element ID.",
  { ...filterFields, parameterNames: z.array(z.string()).describe("Parameter display names to report, e.g. ['Family and Type','Level','Mark']."), maxRows: z.number().optional() },
  async ({ parameterNames, maxRows, ...filter }) => {
    const cap = maxRows || 50;
    const namesArray = parameterNames.map((n) => cs(n)).join(", ");
    const script = [
      buildElementsClause(filter),
      `string[] __names = new string[] { ${namesArray} };`,
      `sb.AppendLine("Id | " + string.Join(" | ", __names));`,
      `foreach (var e in elements.Take(${cap})) {`,
      `  var __row = new List<string> { e.Id.IntegerValue.ToString() };`,
      `  foreach (var __n in __names) { var p = e.LookupParameter(__n); __row.Add(p != null && p.HasValue ? (p.AsValueString() ?? p.AsString() ?? "") : ""); }`,
      `  sb.AppendLine(string.Join(" | ", __row));`,
      `}`,
      `if (elements.Count > ${cap}) sb.AppendLine("... " + (elements.Count - ${cap}) + " more not shown.");`,
      `return sb.ToString();`,
    ].join("\n");
    return runGenerated(script);
  }
);

// ---- move_elements ----------------------------------------------------------------------------
server.tool(
  "move_elements",
  "Translate matching elements by one offset vector (mm).",
  { ...filterFields, offsetXmm: z.number().default(0), offsetYmm: z.number().default(0), offsetZmm: z.number().default(0) },
  async ({ offsetXmm, offsetYmm, offsetZmm, ...filter }) => {
    const script = [
      buildElementsClause(filter),
      `XYZ __t = new XYZ(UnitUtils.ConvertToInternalUnits(${Number(offsetXmm) || 0}, DisplayUnitType.DUT_MILLIMETERS), UnitUtils.ConvertToInternalUnits(${Number(offsetYmm) || 0}, DisplayUnitType.DUT_MILLIMETERS), UnitUtils.ConvertToInternalUnits(${Number(offsetZmm) || 0}, DisplayUnitType.DUT_MILLIMETERS));`,
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

// ---- delete_elements ----------------------------------------------------------------------------
server.tool(
  "delete_elements",
  "Permanently delete matching elements. HIGHEST RISK tool in this set — confirm the real count with " +
    "the user (e.g. via list_elements or count_elements) before calling this, every time.",
  { ...filterFields, confirm: z.literal(true).describe("Must be exactly true — the schema itself refuses this call without an explicit confirmation.") },
  async ({ confirm, ...filter }) => {
    const script = [
      buildElementsClause(filter),
      `int __deleted = 0, __skipped = 0;`,
      `using (var t = new Transaction(Document, "AJ AI - Delete Elements")) {`,
      `  t.Start();`,
      `  try {`,
      `    foreach (var e in elements) { try { if (!Document.GetElement(e.Id).IsValidObject) { __skipped++; continue; } Document.Delete(e.Id); __deleted++; } catch { __skipped++; } }`,
      `    t.Commit();`,
      `    sb.AppendLine("Deleted " + __deleted + " element(s), skipped " + __skipped + ".");`,
      `  } catch (Exception ex) { t.RollBack(); sb.AppendLine("FAILED: " + ex.Message); }`,
      `}`,
      `return sb.ToString();`,
    ].join("\n");
    return runGenerated(script, true);
  }
);

const transport = new StdioServerTransport();
await server.connect(transport);
