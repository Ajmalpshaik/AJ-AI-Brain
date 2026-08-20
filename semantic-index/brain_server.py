"""A search process that stays alive, so the model is loaded once instead of every time.

WHY THIS EXISTS
---------------
Every message typed at an AI session runs `tools/auto-search-hook.mjs`, which spawns a
fresh Python process to search the Brain. Measured 2026-08-21, that cost **3,536 ms per
message**, and almost none of it was the search:

    load the embedding model   1,856 ms      <- paid again on every single message
    pull + tokenise the corpus   548 ms      <- now cached inside hybrid_search
    the actual query            ~215 ms
    process + import overhead   ~900 ms

The model does not change between messages. Neither does the corpus, until the index is
rebuilt. So this keeps one process alive holding both, and answers over a local socket.
Same model, same index, same code path - `hybrid_search` is called exactly as the
command line calls it, so there is no second implementation to drift.

SAFETY, because this opens a socket on a machine used for real work
-------------------------------------------------------------------
  * Binds to 127.0.0.1 ONLY, never 0.0.0.0. Nothing outside this machine can reach it.
  * An ephemeral port, written to brain-server.json along with a random token that every
    request must carry. Localhost-only is the real control; the token is what stops
    another local program using it by accident.
  * Read-only by construction. It can search and nothing else - there is no request that
    writes to the Brain, touches Revit, or runs code.
  * Idles out after IDLE_TIMEOUT and exits, so a forgotten server does not live forever.
  * If it dies, is killed, or was never started, callers fall back to doing the search
    in-process exactly as before. It is an optimisation, never a dependency.

    python brain_server.py                 run in the foreground (Ctrl-C to stop)
    python brain_server.py --stop          stop a running one
    python brain_server.py --status        say whether one is running, and how it is doing
"""

import json
import os
import secrets
import socket
import sys
import threading
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import brain_common as cfg  # noqa: E402

INFO_PATH = HERE / "brain-server.json"
IDLE_TIMEOUT = 30 * 60          # exit after this long with no request
REQUEST_TIMEOUT = 120           # a single request may not hold the server longer
MAX_REQUEST_BYTES = 64 * 1024   # a question is small; anything larger is a mistake


def read_info():
    """{"port","token","pid"} for a server that is CLAIMED to be running, or None."""
    try:
        info = json.loads(INFO_PATH.read_text(encoding="utf-8"))
        if all(k in info for k in ("port", "token")):
            return info
    except (OSError, ValueError):
        pass
    return None


def probe(info, timeout=1.5):
    """Ask a claimed server whether it is really there. Returns its reply or None."""
    if not info:
        return None
    try:
        with socket.create_connection(("127.0.0.1", info["port"]), timeout=timeout) as sock:
            sock.sendall((json.dumps({"token": info["token"], "cmd": "ping"}) + "\n").encode("utf-8"))
            sock.settimeout(timeout)
            data = b""
            while not data.endswith(b"\n"):
                chunk = sock.recv(4096)
                if not chunk:
                    break
                data += chunk
        return json.loads(data.decode("utf-8"))
    except (OSError, ValueError):
        return None


class Server:
    def __init__(self):
        self.token = secrets.token_hex(16)
        self.last_used = time.time()
        self.served = 0
        self.started = time.time()
        self.lock = threading.Lock()
        self.stopping = False

    # -- the one thing it does ---------------------------------------------
    def handle(self, request):
        cmd = request.get("cmd", "search")
        if cmd == "ping":
            return {"ok": True, "pid": os.getpid(), "served": self.served,
                    "up_seconds": round(time.time() - self.started, 1),
                    "model": cfg.EMBED_MODEL}
        if cmd == "stop":
            self.stopping = True
            return {"ok": True, "stopping": True}
        if cmd not in ("search", "context"):
            return {"ok": False, "error": f"unknown cmd {cmd!r}"}

        query = (request.get("query") or "").strip()
        if not query:
            return {"ok": False, "error": "empty query"}

        # Imported here, not at module scope: importing it pulls in chromadb, and a
        # --stop or --status call has no business paying for that.
        from brain_search_hybrid import hybrid_search

        # One search at a time. Queries take ~200 ms and the corpus cache is shared
        # module state; serialising is simpler than making that state thread-safe, and
        # at this latency nobody can tell.
        with self.lock:
            results, notes = hybrid_search(
                query,
                top_k=int(request.get("top_k") or 5),
                area=request.get("area") or None,
                use_fragment_tool=bool(request.get("use_fragment_tool", True)),
                use_rerank=bool(request.get("use_rerank", False)),
            )
            self.served += 1
            self.last_used = time.time()

        if cmd == "context":
            # The whole formatted block, built by the same code the command line uses, so
            # the caller only has to print it. This is what lets tools/auto-search-hook.mjs
            # skip Python entirely - ~570 ms of process start and imports on every message -
            # without a second copy of the block format (and its guardrail lines) in
            # JavaScript, quietly drifting.
            import brain_context
            if not results:
                return {"ok": True, "block": ""}
            if request.get("log"):
                brain_context.log_question(query, results)
            return {"ok": True, "block": brain_context.build_block(query, results, notes)}

        return {"ok": True, "results": results, "notes": notes}

    # -- plumbing ----------------------------------------------------------
    def serve_one(self, conn):
        try:
            conn.settimeout(REQUEST_TIMEOUT)
            data = b""
            while not data.endswith(b"\n"):
                chunk = conn.recv(4096)
                if not chunk:
                    return
                data += chunk
                if len(data) > MAX_REQUEST_BYTES:
                    conn.sendall(b'{"ok":false,"error":"request too large"}\n')
                    return
            request = json.loads(data.decode("utf-8"))
            if request.get("token") != self.token:
                conn.sendall(b'{"ok":false,"error":"bad token"}\n')
                return
            reply = self.handle(request)
        except Exception as exc:                      # never let one bad request kill it
            reply = {"ok": False, "error": f"{type(exc).__name__}: {exc}"}
        try:
            conn.sendall((json.dumps(reply, default=str) + "\n").encode("utf-8"))
        except OSError:
            pass

    def run(self):
        listener = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        listener.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        listener.bind(("127.0.0.1", 0))               # 127.0.0.1 - never 0.0.0.0
        listener.listen(16)
        port = listener.getsockname()[1]

        cfg.atomic_write_text(INFO_PATH, json.dumps(
            {"port": port, "token": self.token, "pid": os.getpid(),
             "started": time.strftime("%Y-%m-%dT%H:%M:%S")}, indent=1))

        # Warm up before announcing readiness, so the first real question does not pay
        # for the model load anyway - which would defeat the entire point.
        warm_start = time.time()
        from brain_search_hybrid import hybrid_search
        try:
            hybrid_search("warm up the model and the corpus cache", top_k=1)
        except Exception as exc:
            print(f"  warm-up failed: {type(exc).__name__}: {exc}", flush=True)
        print(f"AJ Brain search server ready on 127.0.0.1:{port} "
              f"(model {cfg.EMBED_MODEL}, warm-up {time.time() - warm_start:.1f}s)", flush=True)
        print(f"  idles out after {IDLE_TIMEOUT // 60} min. Stop with: "
              f"python brain_server.py --stop", flush=True)

        listener.settimeout(30.0)
        try:
            while not self.stopping:
                try:
                    conn, _addr = listener.accept()
                except socket.timeout:
                    if time.time() - self.last_used > IDLE_TIMEOUT:
                        print("  idle timeout - exiting", flush=True)
                        break
                    continue
                with conn:
                    self.serve_one(conn)
        except KeyboardInterrupt:
            print("  stopped", flush=True)
        finally:
            listener.close()
            # Only remove the file if it still describes THIS process. A newer server
            # may have replaced it, and deleting its details would strand it.
            info = read_info()
            if info and info.get("pid") == os.getpid():
                try:
                    INFO_PATH.unlink()
                except OSError:
                    pass


def main(argv):
    if "--stop" in argv:
        info = read_info()
        if not probe(info):
            print("no server running")
            return 0
        try:
            with socket.create_connection(("127.0.0.1", info["port"]), timeout=3) as sock:
                sock.sendall((json.dumps({"token": info["token"], "cmd": "stop"}) + "\n").encode("utf-8"))
                sock.recv(4096)
        except OSError:
            pass
        print("stop requested")
        return 0

    if "--status" in argv:
        reply = probe(read_info())
        if not reply:
            print("no server running (searches will load the model each time)")
            return 1
        print(f"running  pid {reply.get('pid')}  served {reply.get('served')} query(s)  "
              f"up {reply.get('up_seconds')}s  model {reply.get('model')}")
        return 0

    if probe(read_info()):
        print("a server is already running - nothing to do")
        return 0

    Server().run()
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
