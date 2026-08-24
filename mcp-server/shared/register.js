// One front door for registering a tool, and the single table of what each tool is allowed to do.
//
// WHY THIS EXISTS. Until 2026-08-24 all 28 tools were registered with `server.tool(...)` and carried
// NO annotations at all. The consequence is not cosmetic: annotations are how an MCP client tells a
// harmless tool from a dangerous one. With none declared, `delete_elements` and `count_elements` look
// identical to the client, so it must either prompt for everything or trust everything. Declaring
// them lets a client wave through "how many ducts" and always stop on "delete these 400 elements",
// which is the behaviour AGENT-SPEC already asks a session to follow by hand.
//
// `server.tool()` is also deprecated in the SDK we ship against (1.29.0 marks it `@deprecated Use
// registerTool instead`), and `registerTool` is the only form that accepts annotations — so the
// migration and the labels are one job, done here once instead of 28 times.
//
// THE TABLE IS MANDATORY. `defineTool` THROWS on a name that is not listed. A new tool cannot be
// added without deciding, in writing, what it is allowed to do — which is the failure this file is
// meant to prevent, not just the one it is fixing. test/smoke.test.js walks every module, so an
// unlisted tool fails the suite rather than shipping unlabelled.
//
// WHAT THE FOUR HINTS MEAN HERE (they are hints to the client, never enforcement — the real gate is
// still the bridge's own allowDestructive check in bridge-connection.js):
//   readOnlyHint    - does not modify anything. Reading the model, or reading a file on disk.
//   destructiveHint - changes something a plain undo will not comfortably put back. VIEW-only
//                     changes (overrides, hide/isolate) are NOT destructive: they touch how the model
//                     is drawn, never the model, and each has a named reset tool here.
//   idempotentHint  - calling it twice with the same arguments leaves the same end state.
//                     `move_elements` is the clear counter-example: twice means twice as far.
//   openWorldHint   - the effect is not bounded by this tool's own schema. True only for the two
//                     tools that run arbitrary C#.

const READ_ONLY = { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false };
// A view change: real, but scoped to graphics and reversible by its own reset tool.
const VIEW_ONLY = { readOnlyHint: false, destructiveHint: false, idempotentHint: true, openWorldHint: false };
const MODEL_WRITE = { readOnlyHint: false, destructiveHint: true, idempotentHint: true, openWorldHint: false };

export const TOOL_SAFETY = {
  // --- reads the model, or reads disk -------------------------------------------------------------
  ping: READ_ONLY,
  session_start: READ_ONLY,
  model_summary: READ_ONLY,
  list_elements: READ_ONLY,
  count_elements: READ_ONLY,
  report_parameters: READ_ONLY,
  report_length_by_size: READ_ONLY,
  verify_connectivity: READ_ONLY,
  list_revit_instances: READ_ONLY,
  search_brain: READ_ONLY,
  search_graph: READ_ONLY,

  // --- changes how a view LOOKS, never what the model contains ------------------------------------
  hide_elements: VIEW_ONLY,
  unhide_elements: VIEW_ONLY,
  isolate_elements: VIEW_ONLY,
  reset_isolation: VIEW_ONLY,
  set_color: VIEW_ONLY,
  reset_graphic_overrides: VIEW_ONLY,
  set_transparency: VIEW_ONLY,
  grayout: VIEW_ONLY,
  color_by_group: VIEW_ONLY,
  // Sets Revit's current selection. Touches neither the model nor the view's graphics, but it does
  // move something under Ajmal's cursor while he is working, so it is not claimed as read-only.
  select_elements: VIEW_ONLY,

  // --- picks which Revit / which document the rest of the tools talk to ---------------------------
  // Session routing, not a model change. Getting it wrong is still serious — it is how a write lands
  // in the wrong project when two are open (knowledge: pin the document when two projects are open) —
  // but the call itself alters nothing.
  use_revit_instance: VIEW_ONLY,
  use_revit_document: VIEW_ONLY,

  // --- changes the model --------------------------------------------------------------------------
  set_parameter_value: MODEL_WRITE,
  // NOT idempotent: a second identical call moves the elements a second time.
  move_elements: { ...MODEL_WRITE, idempotentHint: false },
  delete_elements: MODEL_WRITE,

  // --- runs arbitrary C#: the effect is whatever the code says ------------------------------------
  run_csharp: { readOnlyHint: false, destructiveHint: true, idempotentHint: false, openWorldHint: true },
  run_fragment: { readOnlyHint: false, destructiveHint: true, idempotentHint: false, openWorldHint: true },
};

// Same argument order as the `server.tool(...)` calls this replaces, so each tool module changes by
// one word and its schema and handler stay exactly where they were.
export function defineTool(server, name, description, inputSchema, handler) {
  const annotations = TOOL_SAFETY[name];
  if (!annotations) {
    throw new Error(
      `${name} has no entry in TOOL_SAFETY (shared/register.js). Add one saying whether it reads, ` +
        `changes a view, or changes the model — an unlabelled tool looks as safe as ping to the client.`
    );
  }
  return server.registerTool(name, { description, inputSchema, annotations }, handler);
}
