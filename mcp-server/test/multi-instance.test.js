// Multi-Revit selection rules.
//
// The rule Ajmal chose, 2026-08-20: ASK, DO NOT GUESS. One Revit open must feel exactly as it always
// did. Two or more, and nothing reaches Revit until he says which. The distinction that makes it work -
// and which this suite found by failing - is between a session the client merely took because it was the
// only one going, and a session he actually named.
//
// %APPDATA% IS REDIRECTED AT THE TOP OF THIS FILE, BEFORE ANY IMPORT THAT COULD PULL IN
// bridge-connection.js, AND THAT ORDER IS LOAD-BEARING. That module reads APPDATA once, at load, and ES
// modules are cached - so redirecting it inside a test is too late if anything imported the module
// first. Getting this wrong is not a test-only inconvenience: on 2026-08-20 an earlier draft of this
// file sent its probe to the REAL running Revit (harmless here - the probe is the expression "x" - but
// the audit log recorded it). Everything below therefore runs against a throwaway folder, and no pipe is
// ever opened: each assertion is about what the client decides BEFORE it would connect. That is why a
// "pipe not found" error counts as SUCCESS in several checks - it means discovery let the call through
// to the connect step, which is precisely what was being tested.

import fs from "node:fs";
import path from "node:path";
import os from "node:os";

const ROOT = fs.mkdtempSync(path.join(os.tmpdir(), "ajai-instances-"));
process.env.APPDATA = ROOT;               // <- must happen before bridge-connection.js is ever loaded
const AJ_DIR = path.join(ROOT, "AJTools");
const BRIDGES = path.join(AJ_DIR, "bridges");
fs.mkdirSync(BRIDGES, { recursive: true });

import { test, after } from "node:test";
import assert from "node:assert/strict";

after(() => fs.rmSync(ROOT, { recursive: true, force: true }));

const REACHED_CONNECT = /pipe not found|Timed out connecting/i;

const write = (pid, title, version, started) =>
  fs.writeFileSync(
    path.join(BRIDGES, `${pid}.json`),
    JSON.stringify({
      pipeName: `AJTools.AjAi.${pid}`,
      token: `tok-${pid}`,
      pid,
      revitVersion: version,
      windowTitle: title,
      startedUtc: started,
    })
  );
const remove = (pid) => fs.rmSync(path.join(BRIDGES, `${pid}.json`), { force: true });

// Structural guard, mirroring the one search_brain has. The big "every tool module registers exactly one
// well-formed tool" test in smoke.test.js walks a HARDCODED list of register functions, so a new tool is
// not covered by it merely by existing - these two were added on 2026-08-20 and sailed through it
// untouched. One file per tool, because brain-status.mjs counts .js files in tools/ and reports that as
// the Brain's native-tool count.
test("the two Revit-instance tools register well-formed, one per module", async () => {
  const fake = () => {
    const registrations = [];
    return {
      registrations,
      tool: (name, description, schema, handler) =>
        registrations.push({ name, description, schema, handler }),
    };
  };

  for (const [modulePath, expectedName] of [
    ["../tools/list-revit-instances.js", "list_revit_instances"],
    ["../tools/use-revit-instance.js", "use_revit_instance"],
  ]) {
    const server = fake();
    const { register } = await import(modulePath);
    register(server);

    assert.equal(server.registrations.length, 1, `${modulePath} must register exactly one tool`);
    const { name, description, schema, handler } = server.registrations[0];
    assert.equal(name, expectedName);
    assert.ok(description && description.length > 10, `${name}: description missing or too short`);
    assert.equal(typeof schema, "object", `${name}: schema must be an object`);
    assert.equal(typeof handler, "function", `${name}: handler must be a function`);
  }
});

test("multi-Revit instance selection", async (t) => {
  const mod = await import("../bridge-connection.js");

  const callErr = async () => {
    try {
      await mod.callBridge('"x"', false);
      return null;
    } catch (e) {
      return e.message;
    }
  };

  await t.test("no Revit connected", async () => {
    assert.equal(mod.listBridgeInstances().length, 0);
    assert.match(await callErr(), /not connected/i);
  });

  await t.test("one Revit is used without asking", async () => {
    write(1111, "Tower-MEP.rvt - Autodesk Revit 2024 - Level 1", "2024", "2026-08-20T10:00:00Z");
    assert.equal(mod.listBridgeInstances().length, 1);
    assert.equal(
      mod.describeInstance(mod.listBridgeInstances()[0]),
      "Revit 2024 - Tower-MEP.rvt (pid 1111)"
    );
    assert.match(await callErr(), REACHED_CONNECT);
    assert.equal(mod.getSelectedPid(), 1111);
  });

  await t.test("a second Revit opening mid-chat stops the auto-pick standing", async () => {
    write(2222, "Villa-Arch.rvt - Autodesk Revit 2020 - Level 0", "2020", "2026-08-20T11:00:00Z");
    assert.equal(mod.listBridgeInstances().length, 2);
    assert.equal(Number(mod.listBridgeInstances()[0].pid), 2222, "newest first");

    const err = await callErr();
    assert.ok(err && !REACHED_CONNECT.test(err), "must not reach Revit: " + err);
    assert.match(err, /Another Revit has been opened/);
    assert.match(err, /Tower-MEP\.rvt/);
    assert.match(err, /Villa-Arch\.rvt/);
    assert.match(err, /Nothing has been sent to Revit/);
  });

  await t.test("his explicit choice pins, and later Revits do not re-ask", async () => {
    assert.equal(Number(mod.selectBridgeInstance(2222).pid), 2222);
    assert.equal(mod.getSelectedPid(), 2222);
    assert.match(await callErr(), REACHED_CONNECT);

    write(3333, "Third.rvt - Autodesk Revit 2027 - Level 2", "2027", "2026-08-20T12:00:00Z");
    assert.match(await callErr(), REACHED_CONNECT, "his choice must survive a third Revit opening");

    assert.throws(() => mod.selectBridgeInstance(9999), /No connected Revit with pid 9999/);
  });

  await t.test("the session he chose closing stops everything - no sliding onto another project", async () => {
    remove(2222);
    const err = await callErr();
    assert.ok(err && !REACHED_CONNECT.test(err), "must not silently switch: " + err);
    assert.match(err, /you chose \(pid 2222\) has closed/);
    assert.match(err, /Third\.rvt/);
    assert.match(err, /Tower-MEP\.rvt/);
  });

  await t.test("an auto-picked session closing just re-picks - nothing of his to contradict", async () => {
    remove(3333);
    remove(1111);
    write(4444, "Only-One.rvt - Autodesk Revit 2024 - Level 1", "2024", "2026-08-20T13:00:00Z");
    assert.match(await callErr(), REACHED_CONNECT);
    assert.equal(mod.getSelectedPid(), 4444);
  });

  await t.test("an older AJ Tools with no bridges folder still connects", () => {
    fs.rmSync(BRIDGES, { recursive: true, force: true });
    fs.writeFileSync(
      path.join(AJ_DIR, "ajai-bridge.json"),
      JSON.stringify({ pipeName: "AJTools.AjAi", token: "legacy" })
    );
    const legacy = mod.listBridgeInstances();
    assert.equal(legacy.length, 1);
    assert.equal(legacy[0].pipeName, "AJTools.AjAi");
    assert.equal(legacy[0].pid, 0, "placeholder pid so it is still selectable");
  });
});
