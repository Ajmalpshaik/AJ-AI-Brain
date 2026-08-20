// What actually goes ON THE WIRE when a project is pinned.
//
// Choosing the Revit is only half the problem. One Revit can hold several open projects, and on the
// Revit side `Document` is `ActiveUIDocument.Document` - the front window, which changes the moment
// Ajmal clicks another project. So a multi-step job could start in one project and finish in another.
// Pinning a title makes every call name the project explicitly.
//
// This suite needs no Revit: it stands up a REAL named-pipe server, points a fake instance file at it,
// and reads the bytes the client sends. That is the only way to prove the payload shape - and the
// payload shape is the whole contract with the add-in.
//
// %APPDATA% is redirected before any import that could load bridge-connection.js, and that order is
// load-bearing: the module reads APPDATA once, at load, and ES modules are cached.

import fs from "node:fs";
import path from "node:path";
import os from "node:os";
import net from "node:net";

const ROOT = fs.mkdtempSync(path.join(os.tmpdir(), "ajai-doctarget-"));
process.env.APPDATA = ROOT;
const BRIDGES = path.join(ROOT, "AJTools", "bridges");
fs.mkdirSync(BRIDGES, { recursive: true });

import { test, after } from "node:test";
import assert from "node:assert/strict";

const PIPE = "AJTools.AjAi.TEST." + process.pid;
const received = [];
let server;
// The client keeps its pipe connection OPEN between calls on purpose (that is what makes a long job
// fast). Closing only the server therefore leaves a live socket holding the event loop up, and the run
// hangs until the test runner kills it - which is a FAILED file even with every check green. So keep
// every accepted socket and destroy them at the end.
const sockets = [];

function startFakePipe() {
  return new Promise((resolve) => {
    server = net.createServer((socket) => {
      sockets.push(socket);
      socket.on("error", () => {});   // destroying a socket mid-flight is expected here, not a fault
      socket.on("data", (chunk) => {
        for (const line of chunk.toString("utf8").split("\n")) {
          if (!line.trim()) continue;
          received.push(JSON.parse(line));
          // Answer in the add-in's shape so the client resolves instead of timing out.
          socket.write(JSON.stringify({ Success: true, Output: "ok", Error: null }) + "\n");
        }
      });
    });
    server.listen(path.join("\\\\.\\pipe", PIPE), resolve);
  });
}

const writeInstance = (pid) =>
  fs.writeFileSync(
    path.join(BRIDGES, `${pid}.json`),
    JSON.stringify({
      pipeName: PIPE,
      token: "tok",
      pid,
      revitVersion: "2020",
      windowTitle: `Autodesk Revit 2020 - [proj${pid}.rvt - Floor Plan]`,
      startedUtc: "2026-08-20T10:00:00Z",
    })
  );

after(() => {
  for (const s of sockets) { try { s.destroy(); } catch {} }
  try { server?.close(); } catch {}
  fs.rmSync(ROOT, { recursive: true, force: true });
});

test("pinning a project changes what is sent, and only then", async (t) => {
  await startFakePipe();
  writeInstance(1111);
  const mod = await import("../bridge-connection.js");

  await t.test("nothing pinned: the payload is byte-identical to what it always was", async () => {
    received.length = 0;
    await mod.callBridge('"x"', false);
    assert.equal(received.length, 1);
    assert.deepEqual(Object.keys(received[0]).sort(), ["allowDestructive", "code", "token"]);
    assert.ok(
      !("document" in received[0]),
      "the field must be ABSENT, not empty - an older AJ Tools must see the payload it always saw"
    );
  });

  await t.test("pinned: the project travels with every call", async () => {
    mod.selectDocument("school");
    assert.equal(mod.getSelectedDocument(), "school");

    received.length = 0;
    await mod.callBridge('"x"', false);
    await mod.callBridge('"y"', false);
    assert.equal(received.length, 2);
    for (const p of received) assert.equal(p.document, "school", "every call must name the project");
  });

  await t.test("unpinning goes back to following the front window", async () => {
    mod.selectDocument(null);
    assert.equal(mod.getSelectedDocument(), null);

    received.length = 0;
    await mod.callBridge('"x"', false);
    assert.ok(!("document" in received[0]));
  });

  await t.test("a blank title counts as unpinned, not as a project named nothing", () => {
    mod.selectDocument("   ");
    assert.equal(mod.getSelectedDocument(), null);
  });

  await t.test("switching Revit drops the project pin", async () => {
    mod.selectDocument("school");
    writeInstance(2222);
    mod.selectBridgeInstance(2222);
    assert.equal(
      mod.getSelectedDocument(),
      null,
      "a title open in one Revit means nothing in another - carrying it over would target the wrong thing"
    );

    received.length = 0;
    await mod.callBridge('"x"', false);
    assert.ok(!("document" in received[0]));
  });

  await t.test("re-selecting the SAME Revit keeps the pin", async () => {
    mod.selectDocument("vila");
    mod.selectBridgeInstance(2222);
    assert.equal(mod.getSelectedDocument(), "vila", "no Revit change, so nothing to invalidate");
  });
});
