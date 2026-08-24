// Runs a child process and collects its output WITHOUT stopping the server.
//
// WHY. Both Brain search tools used `spawnSync`, which blocks Node's single thread until the child
// exits. Measured in the test suite on 2026-08-24: `search_brain` took 9.6 s and `search_graph` 2.4 s.
// For all of that time the MCP server could not answer anything at all — not another tool call, not a
// ping, not a cancellation. From the outside it is indistinguishable from a hung bridge, and a Brain
// search is the one thing a session is most likely to fire off while something else is in flight.
//
// The result shape is deliberately identical to spawnSync's ({ error, status, stdout, stderr }) so the
// call sites changed by one word plus an `await`, and their existing error handling still applies.
//
// The timeout and the output cap are enforced here rather than left to the caller, because spawnSync
// enforced both and dropping either while making this async would have traded one hang for another.

import { spawn } from "node:child_process";

export function spawnCapture(command, args, { cwd, timeout = 120000, maxBuffer = 10 * 1024 * 1024 } = {}) {
  return new Promise((resolve) => {
    let child;
    try {
      child = spawn(command, args, { cwd });
    } catch (err) {
      resolve({ error: err });
      return;
    }

    let stdout = "";
    let stderr = "";
    let overflowed = false;
    let settled = false;

    const finish = (result) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      resolve(result);
    };

    const timer = setTimeout(() => {
      child.kill();
      finish({ error: new Error(`timed out after ${Math.round(timeout / 1000)}s — the search did not finish`) });
    }, timeout);

    const collect = (stream, append) => {
      stream.setEncoding("utf8");
      stream.on("data", (chunk) => {
        if (append(chunk) > maxBuffer) {
          overflowed = true;
          child.kill();
        }
      });
    };

    collect(child.stdout, (chunk) => (stdout += chunk).length);
    collect(child.stderr, (chunk) => (stderr += chunk).length);

    child.on("error", (err) => finish({ error: err }));
    child.on("close", (status) =>
      finish(overflowed ? { error: new Error("output exceeded the 10 MB cap") } : { status, stdout, stderr })
    );
  });
}
