#!/usr/bin/env bash
# Set up the AJ AI Brain's search on Linux or macOS.
#
#   bash semantic-index/setup.sh
#
# WHY THIS EXISTS
# ---------------
# Everything the search needs is already cross-platform: brain_search_hybrid.py is plain
# Python, and both mcp-server/brain-tools/search-brain.js and tools/auto-search-hook.mjs
# already look for venv/bin/python as well as venv/Scripts/python.exe.
#
# The ONE thing missing off Windows was the venv itself. requirements.txt gives Windows
# setup commands only, so on Claude Code for web — or any Linux/macOS container — there is
# no venv, no chromadb, and every entry point reports "no Python found in semantic-index/venv".
# The retrieval layer went dark in exactly the sessions that edit the Brain most.
#
# This is the Bash half of the commands already documented in requirements.txt. It changes
# no settings and no retrieval behaviour — it only builds what those commands build.
#
# HARD RULE it must respect (Sophos / verify-fragments-compile.ps1 incident, 2026-08-04):
# nothing may be unpacked into the system temp folder. pip's temp and cache are redirected
# inside semantic-index/ below, the same way brain_common.py redirects Python's own temp.
#
# Safe to re-run. Every step checks whether it is already done and says so.

set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VENV="$HERE/venv"
PY="$VENV/bin/python"

# Keep pip off the system temp folder — see the hard rule above.
export TMPDIR="$HERE/pip-temp"
export PIP_CACHE_DIR="$HERE/pip-cache"
mkdir -p "$TMPDIR" "$PIP_CACHE_DIR"

step() { printf '\n\033[1m%s\033[0m\n' "$*"; }
ok()   { printf '  \033[32mOK\033[0m  %s\n' "$*"; }
info() { printf '      %s\n' "$*"; }

# --------------------------------------------------------------------------
step "1/5  Python"
# --------------------------------------------------------------------------
PYBIN="${PYTHON:-}"
if [ -z "$PYBIN" ]; then
  for c in python3.12 python3.11 python3; do
    command -v "$c" >/dev/null 2>&1 && { PYBIN="$c"; break; }
  done
fi
if [ -z "$PYBIN" ]; then
  echo "  No python3 found. Install Python 3.10 or newer, then run this again." >&2
  exit 1
fi
"$PYBIN" - <<'PYCHECK'
import sys
if sys.version_info < (3, 10):
    sys.exit("  Python %d.%d is too old - chromadb needs 3.10 or newer." % sys.version_info[:2])
PYCHECK
ok "$("$PYBIN" --version 2>&1)  at  $(command -v "$PYBIN")"

# --------------------------------------------------------------------------
step "2/5  Private Python installation (venv)"
# --------------------------------------------------------------------------
if [ -x "$PY" ]; then
  ok "already there — $VENV"
else
  info "creating $VENV ..."
  "$PYBIN" -m venv "$VENV"
  ok "created"
fi

# --------------------------------------------------------------------------
step "3/5  Dependencies (chromadb, and about 70 packages it pulls in)"
# --------------------------------------------------------------------------
if "$PY" -c "import chromadb" >/dev/null 2>&1; then
  ok "chromadb $("$PY" -c 'import chromadb;print(chromadb.__version__)' 2>/dev/null) already installed"
else
  info "this takes a few minutes the first time ..."
  "$PY" -m pip install --quiet --upgrade pip
  "$PY" -m pip install --quiet -r "$HERE/requirements.txt"
  ok "installed"
fi

# --------------------------------------------------------------------------
step "4/5  The embedding model"
# --------------------------------------------------------------------------
# Deliberately does NOT download bge-small-en-v1.5.
#
# Two reasons, both learned on 2026-08-20. brain_common.py's default is MiniLM
# because it ships inside chromadb and therefore always works - BGE was briefly the
# default and broke search outright on the Windows PC, because its weights are a
# separate ~127 MB download. And BGE has never been scored: score-history.md has no
# line for it. Making a first-time setup pull an unmeasured model over a working one
# is the "documentation ahead of reality" failure in a different coat.
#
# So setup does the thing that always works, and says how to upgrade deliberately.
MODEL="$("$PY" -c "import brain_common; print(brain_common.EMBED_MODEL)" 2>/dev/null || echo all-MiniLM-L6-v2)"
ok "using $MODEL — the repo default, no download needed"
if [ -f "$HERE/embed-model.txt" ]; then
  info "pinned by semantic-index/embed-model.txt — delete that file to go back to the default"
fi

# --------------------------------------------------------------------------
step "5/5  Build the index"
# --------------------------------------------------------------------------
# Switching models changes build_fingerprint(), so this rebuilds itself when it must.
# Vectors from two models cannot be compared, and a part-migrated index answers every
# question, just quietly worse.
"$PY" "$HERE/brain_index.py" 2>&1 | grep -vF "iB/s" | tail -5 | sed 's/^/      /'

printf '\n\033[1mReady.\033[0m  Running on: %s\n\n' "$MODEL"
cat <<EOF
Ask the Brain a question:

    $PY $HERE/brain_search_hybrid.py "how do I undo a mistake"

Score the search against semantic-index/test-questions.md:

    $PY $HERE/score_brain.py

Optional, and still unmeasured — try the newer embedding model and pin it:

    $PY $HERE/embed_bge.py --download          # ~127 MB, needs huggingface.co
    echo bge-small-en-v1.5 > $HERE/embed-model.txt
    $PY $HERE/brain_index.py --full
    $PY $HERE/score_brain.py                   # compare against score-history.md

Nothing else needs doing. The Stop hook re-indexes after any turn that edits a file,
and search_brain works as an MCP tool call from here on.
EOF
