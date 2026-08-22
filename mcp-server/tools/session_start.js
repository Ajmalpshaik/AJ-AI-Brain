// session_start — the one call that answers everything before touching the model.
//
// Ajmal's rule, 2026-08-20: "everytime while pinging or connection to revit check the all things like
// what is the version of revit what is the model like that all kind of thing that need to check in
// the beginning." AGENT-SPEC 7.1 lists it as a fastest method, and the fragment is named in more
// places across the skills than almost any other.
//
// Zero inputs, read-only, one bridge round trip. It replaces firing several context-*.cs fragments
// to assemble the same picture.
//
// HONEST STATUS: the fragment behind it is NOT yet verified against a real model. That does not make
// it broken — the Brain's rule is warn and keep working — and the composed result says so in its own
// warnings. Run it, check the answer looks like the real project, and mark it proven.

import { runComposed, previewField } from "../shared/fragment-tool.js";

export function register(server) {
  server.tool(
    "session_start",
    "The opening call for any Revit session: which Revit, which API generation, which document, the " +
      "project's real units, model size, linked files, closed worksets, warnings and the active view — " +
      "in one read-only round trip. Call this before the first piece of real work instead of guessing " +
      "or firing several context queries. Takes no inputs.",
    { ...previewField },
    async ({ preview }) =>
      runComposed({
        describe: "Read the Revit session and project state",
        names: ["context/context-session-start.cs"],
        preview,
      })
  );
}
