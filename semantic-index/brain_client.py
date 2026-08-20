"""Talk to a warm brain_server if one is up; otherwise search here, and start one for next time.

The whole contract in one line: **this is never allowed to make a search fail.** Every
path out of `search()` returns a real answer - from the server when it is there, from an
in-process search when it is not. A server that is missing, dead, hung, wrong-versioned
or refusing connections costs one timeout and then behaves exactly as before this file
existed.

That is why the fallback is not an error branch bolted on the side. It is the normal
path, and the server is the optimisation.

WHY IT STARTS THE SERVER ITSELF, rather than asking anyone to run one
---------------------------------------------------------------------
A speed-up that needs a manual step first is a speed-up nobody gets. This Brain has the
scar to prove it: `score_brain.py` existed for a whole day and nothing ever ran it, which
is why `tools/score-check.mjs` was written to run it automatically.

So the first search after a reboot is slow, exactly as slow as it is today - and it
leaves a warm server behind, so the second one is not. Nothing to remember, nothing to
install, and no new failure mode if the spawn does not work.
"""

import json
import os
import socket
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import brain_server as srv  # noqa: E402  (cheap: it does not import chromadb at module scope)

CONNECT_TIMEOUT = 1.0    # a server that cannot answer the door this fast is not helping
QUERY_TIMEOUT = 60.0


def _ask(info, payload, timeout):
    with socket.create_connection(("127.0.0.1", info["port"]), timeout=CONNECT_TIMEOUT) as sock:
        sock.settimeout(timeout)
        sock.sendall((json.dumps(payload) + "\n").encode("utf-8"))
        data = b""
        while not data.endswith(b"\n"):
            chunk = sock.recv(65536)
            if not chunk:
                break
            data += chunk
    return json.loads(data.decode("utf-8"))


def windowless_python():
    """
    The interpreter that is never handed a console window on Windows.

    WHY THIS IS NOT sys.executable. This venv's python.exe is a LAUNCHER SHIM over the
    Microsoft Store Python (pyvenv.cfg points at WindowsApps): it re-executes the base
    interpreter as a SECOND process, and that second process never saw the creation flags
    given to the first. So DETACHED_PROCESS below is honoured by the shim and lost by the
    real interpreter, which then allocates a console of its own - and on Windows 11, where
    Windows Terminal is the default console host, that console is a black window sitting on
    top of whatever Ajmal was actually looking at.

    Measured 2026-08-21, with the server started this way: conhost.exe parented directly to
    the server's python3.11.exe, and a WindowsTerminal.exe created in the same second.
    Ajmal's words: "evry time i chat what is this coming ??" - and closing that window kills
    the server, so the next message starts another one, and it looks like it comes back
    forever.

    pythonw.exe is a GUI-subsystem binary, so Windows allocates no console for it at all,
    and the shim re-executes the base pythonw - nothing has to survive a flag handoff. Same
    fix, same shim, as the voice layer made on 2026-08-11; see tools/voice/say.mjs.
    """
    if os.name != "nt":
        return sys.executable
    windowless = Path(sys.executable).with_name("pythonw.exe")
    return str(windowless) if windowless.exists() else sys.executable


def start_server_detached():
    """
    Launch a server that outlives this process. Best effort, never raises.

    Detached on purpose: the point is that it is still there for the NEXT command, so it
    must not be killed when this one exits, and must not hold this one's console open.
    """
    try:
        kwargs = {"cwd": str(HERE), "stdin": subprocess.DEVNULL,
                  "stdout": subprocess.DEVNULL, "stderr": subprocess.DEVNULL}
        if os.name == "nt":
            # DETACHED_PROCESS | CREATE_NEW_PROCESS_GROUP - this process gets no console of
            # its own, and Ctrl-C in the parent terminal does not reach it. It is NOT what
            # keeps the window away on its own: see windowless_python() above for the reason.
            kwargs["creationflags"] = 0x00000008 | 0x00000200
        else:
            kwargs["start_new_session"] = True
        subprocess.Popen([windowless_python(), str(HERE / "brain_server.py")], **kwargs)
        return True
    except Exception:
        return False


def search(query, top_k=5, area=None, use_fragment_tool=True, use_rerank=False,
           allow_start=True):
    """
    (results, notes, source) - source is "server" or "local", for the caller to report.

    Falls back to an in-process search on any problem at all, and quietly asks for a
    server to exist by the next call.
    """
    payload = {"cmd": "search", "query": query, "top_k": top_k, "area": area,
               "use_fragment_tool": use_fragment_tool, "use_rerank": use_rerank}

    info = srv.read_info()
    if info:
        try:
            reply = _ask(info, {**payload, "token": info["token"]}, QUERY_TIMEOUT)
            if reply.get("ok"):
                return reply.get("results") or [], reply.get("notes") or [], "server"
        except (OSError, ValueError):
            pass  # stale details, dead server, or a hung one - fall through

    # Do the work here. Import is deferred so --status-style callers never pay for it.
    from brain_search_hybrid import hybrid_search
    results, notes = hybrid_search(query, top_k=top_k, area=area,
                                   use_fragment_tool=use_fragment_tool,
                                   use_rerank=use_rerank)

    # Ask for a warm one next time. Deliberately AFTER answering, so starting it can
    # never delay the answer the caller is waiting on.
    if allow_start and not srv.probe(srv.read_info(), timeout=0.4):
        start_server_detached()

    return results, notes, "local"
