// Low-level plumbing for talking to the AJ AI Bridge named pipe: discovery-file lookup, connection
// lifecycle, request queueing. Every tool file calls the one exported function, `callBridge(code,
// allowDestructive)` — nothing else here is meant to be imported directly.

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
  let stat;
  try {
    stat = fs.statSync(DISCOVERY_FILE);
  } catch {
    // Covers "file doesn't exist" (the common case) and any other stat failure (e.g. permissions) —
    // both mean the same thing to a caller: there's nothing usable to connect through right now.
    cachedDiscovery = undefined;
    throw new Error(
      "AJ AI Bridge is not connected. In Revit, open the AJ AI pane and click \"Connect AJ AI Bridge\", then try again."
    );
  }

  if (
    cachedDiscovery &&
    cachedDiscovery.mtimeMs === stat.mtimeMs &&
    cachedDiscovery.size === stat.size
  ) {
    return cachedDiscovery.info;
  }

  let info;
  try {
    const raw = fs.readFileSync(DISCOVERY_FILE, "utf8");
    info = JSON.parse(raw);
  } catch (err) {
    // Covers a genuine race (the file was deleted/replaced between the statSync above and this read —
    // e.g. Revit was closed at exactly this moment) as well as a truncated/corrupt write caught mid-flight.
    // Without this, either case would throw a raw ENOENT/SyntaxError straight out of this function instead
    // of the same friendly, actionable message every other failure mode here already gives.
    throw new Error(
      "Could not read the AJ AI bridge connection file (" + err.message + "). Reconnect from the AJ AI pane in Revit."
    );
  }

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

export function callBridge(code, allowDestructive) {
  const nextCall = bridgeCallQueue.then(() => callBridgeNow(code, allowDestructive));
  bridgeCallQueue = nextCall.catch(() => undefined);
  return nextCall;
}
