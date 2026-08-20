// Low-level plumbing for talking to the AJ AI Bridge named pipe: discovery-file lookup, connection
// lifecycle, request queueing. Every tool file calls the one exported function, `callBridge(code,
// allowDestructive)` — nothing else here is meant to be imported directly.

import net from "node:net";
import fs from "node:fs";
import path from "node:path";

const AJTOOLS_DIR = path.join(process.env.APPDATA || "", "AJTools");
// Legacy single-instance pointer. Revit still writes it, so an older client keeps working; this one
// only falls back to it when the per-instance folder is missing entirely (an older AJ Tools build).
const DISCOVERY_FILE = path.join(AJTOOLS_DIR, "ajai-bridge.json");
// One file per live Revit, named <pid>.json. Written by McpBridgeService.WriteDiscoveryFile().
const INSTANCES_DIR = path.join(AJTOOLS_DIR, "bridges");
// RevitExecutionService soft-cancels a loop-based script at 60s, then gives it a further 20s grace
// period to actually unwind before its own hard backstop gives up (see that file's HardWaitTimeout,
// 80s total). This must stay comfortably above that 80s, or a script that's still legitimately
// unwinding gets reported here as "timed out" even though Revit would have finished it normally a
// few seconds later.
const RESPONSE_TIMEOUT_MS = 90_000;
const CONNECT_TIMEOUT_MS = 10_000;

let cachedDiscovery;
let activeConnection;

// Which Revit this MCP session is talking to. Null means "not chosen yet", which only matters when
// more than one Revit is connected.
let selectedPid = null;
// Was that choice AJMAL'S, or did the client just take the only session going? The difference decides
// what happens when the world changes underneath it, and getting it wrong is the whole risk here:
//   - auto-picked, then a second Revit opens  -> ASK, because he never actually chose this one
//   - auto-picked, and it closes              -> quietly take the remaining one, nothing was chosen
//   - he picked it, and more Revits open      -> keep his choice, do not nag
//   - he picked it, and it closes             -> STOP and say so, never slide onto a different project
// Found by test, not by reasoning: without this flag, opening a second Revit mid-chat left every later
// command silently going to the first one.
let selectedWasExplicit = false;

// Which PROJECT inside that Revit, by title. Null = whichever is in front, which is what every call
// did before 2026-08-20. Choosing the Revit is only half the problem: one Revit can hold several open
// projects, and `Document` on the Revit side is `ActiveUIDocument.Document` - the front window, which
// changes when Ajmal clicks another project. So a job could start in one and finish in another.
let selectedDocument = null;

const NOT_CONNECTED =
  'AJ AI Bridge is not connected. In Revit, open the AJ AI pane and click "Connect AJ AI Bridge", then try again.';

function readInstanceFile(file) {
  try {
    const info = JSON.parse(fs.readFileSync(file, "utf8"));
    if (!info || !info.pipeName || !info.token) return undefined;
    if (info.pid == null) {
      // A pre-multi-instance AJ Tools wrote this. Treat it as one nameless instance rather than
      // discarding it, so an old add-in against a new client still connects.
      info.pid = 0;
    }
    return info;
  } catch {
    // Deleted mid-read (Revit closing) or written half-way. Either way it is not a usable instance.
    return undefined;
  }
}

/**
 * Every Revit currently advertising a bridge, newest first.
 *
 * Revit sweeps dead instance files when it starts, but nothing sweeps them while it is running, so a
 * Revit that crashed a minute ago can still have a file here. A stale entry cannot hurt: connecting to
 * its pipe fails and the caller is told plainly, which is far better than silently hiding a session
 * that IS alive because a liveness probe was too clever.
 */
export function listBridgeInstances() {
  let files = [];
  try {
    files = fs
      .readdirSync(INSTANCES_DIR)
      .filter((f) => f.toLowerCase().endsWith(".json"))
      .map((f) => path.join(INSTANCES_DIR, f));
  } catch {
    // No per-instance folder: an older AJ Tools, or the bridge has never been started on this machine.
    const legacy = readInstanceFile(DISCOVERY_FILE);
    return legacy ? [legacy] : [];
  }

  const instances = files.map(readInstanceFile).filter(Boolean);
  if (instances.length === 0) {
    const legacy = readInstanceFile(DISCOVERY_FILE);
    return legacy ? [legacy] : [];
  }

  instances.sort((a, b) => String(b.startedUtc || "").localeCompare(String(a.startedUtc || "")));
  return instances;
}

/** Short human label for one instance - what Ajmal actually recognises a session by. */
export function describeInstance(info) {
  const version = info.revitVersion ? `Revit ${info.revitVersion}` : "Revit";
  const title = (info.windowTitle || "").trim();
  // Revit's window title is "<document> - <Revit year> - <view>"; the document is the useful half.
  const doc = title ? title.split(" - ")[0].trim() : "";
  return `${version}${doc ? ` - ${doc}` : ""} (pid ${info.pid})`;
}

/** Pin this MCP session to one Revit. Returns the chosen instance, or throws with the valid choices. */
export function selectBridgeInstance(pid) {
  const wanted = Number(pid);
  const instances = listBridgeInstances();
  const match = instances.find((i) => Number(i.pid) === wanted);
  if (!match) {
    const list = instances.map((i) => `  - ${describeInstance(i)}`).join("\n");
    throw new Error(
      instances.length
        ? `No connected Revit with pid ${pid}. Currently connected:\n${list}`
        : NOT_CONNECTED
    );
  }
  // Switching Revit drops any project pin: a title that was open in one Revit means nothing in
  // another, and carrying it over would silently target the wrong thing or fail confusingly.
  if (selectedPid !== Number(match.pid)) selectedDocument = null;
  selectedPid = Number(match.pid);
  selectedWasExplicit = true;
  return match;
}

export function getSelectedPid() {
  return selectedPid;
}

/**
 * Pin every later call to one open project by title, inside the Revit already chosen. Pass null to go
 * back to "whichever is in front". The title is NOT validated here on purpose - Revit is the only thing
 * that knows what is open right now, and it answers with the real list if the name is wrong.
 */
export function selectDocument(title) {
  selectedDocument = title && String(title).trim() ? String(title).trim() : null;
  return selectedDocument;
}

export function getSelectedDocument() {
  return selectedDocument;
}

function readDiscoveryInfo() {
  const instances = listBridgeInstances();

  if (instances.length === 0) {
    cachedDiscovery = undefined;
    selectedPid = null;
    selectedWasExplicit = false;
    throw new Error(NOT_CONNECTED);
  }

  const choices = () => instances.map((i) => `  - ${describeInstance(i)}`).join("\n");

  if (selectedPid !== null) {
    const chosen = instances.find((i) => Number(i.pid) === selectedPid);

    if (chosen) {
      // His own choice stands even after other Revits appear - he already answered this question.
      if (selectedWasExplicit || instances.length === 1) return chosen;

      // Only auto-picked, and now there is a real choice to make. Ask before anything is sent.
      selectedPid = null;
      throw new Error(
        `Another Revit has been opened since this chat started, so there are now ${instances.length} ` +
          `sessions and it is not safe to keep assuming:\n${choices()}\n` +
          "Ask Ajmal which session he means, then call use_revit_instance with that pid. " +
          "Nothing has been sent to Revit."
      );
    }

    // The session being used has gone.
    const wasExplicit = selectedWasExplicit;
    const lostPid = selectedPid;
    selectedPid = null;
    selectedWasExplicit = false;

    if (wasExplicit) {
      // He named this one. Sliding onto whatever else happens to be open is exactly the
      // "finished in the wrong project" failure this mechanism exists to prevent - so stop.
      throw new Error(
        `The Revit session you chose (pid ${lostPid}) has closed.\n` +
          (instances.length
            ? `Still open:\n${choices()}\nCall use_revit_instance with the pid you want.`
            : NOT_CONNECTED)
      );
    }
    // Never explicitly chosen, so there is nothing of his to contradict - fall through and re-pick.
  }

  if (instances.length === 1) {
    // The ordinary case, and it must feel exactly as it always did: one Revit, no questions.
    selectedPid = Number(instances[0].pid);
    selectedWasExplicit = false;
    return instances[0];
  }

  throw new Error(
    `${instances.length} Revit sessions are connected, so it is not safe to guess which one you mean:\n` +
      choices() +
      "\nAsk Ajmal which session to use, then call use_revit_instance with that pid. " +
      "Nothing is sent to Revit until one is chosen."
  );
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

// KNOWN LIMITATION, not fixed here (would need a design decision, not a bug fix): reuse below only
// checks LOCAL socket health (not closed/destroyed/writable). If Revit's listener dies without the OS
// noticing at the socket level — rare, but named pipes can do this — a reused connection can look
// healthy, accept the write, then simply never answer, so the caller pays the full 90s RESPONSE_TIMEOUT_MS
// instead of a fast failure. Fixing this would mean a cheap "ping" before reuse or a periodic heartbeat —
// worth a deliberate decision (adds latency/complexity to the common path) rather than a silent addition.
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

function sendRequest(connection, info, code, allowDestructive, documentTitle) {
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
      // `document` is omitted entirely when nothing is pinned, so an OLDER AJ Tools that has never
      // heard of the field sees exactly the payload it always saw.
      const payload = { token: info.token, code, allowDestructive: !!allowDestructive };
      if (documentTitle) payload.document = documentTitle;
      connection.socket.write(JSON.stringify(payload) + "\n");
    } catch (err) {
      closeConnection(connection, err.message);
    }
  });
}

async function callBridgeNow(code, allowDestructive) {
  const info = readDiscoveryInfo();
  const connection = await getConnection(info);
  return sendRequest(connection, info, code, allowDestructive, selectedDocument);
}

// Revit runs API work on one ExternalEvent at a time. Serializing calls preserves that contract while
// the underlying named-pipe connection stays open between requests.
let bridgeCallQueue = Promise.resolve();

export function callBridge(code, allowDestructive) {
  const nextCall = bridgeCallQueue.then(() => callBridgeNow(code, allowDestructive));
  bridgeCallQueue = nextCall.catch(() => undefined);
  return nextCall;
}
