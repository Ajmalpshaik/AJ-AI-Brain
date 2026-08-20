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
step "4/5  The embedding model (~127 MB, downloaded once, then fully offline)"
# --------------------------------------------------------------------------
# bge-small-en-v1.5 comes from huggingface.co. That host is reachable from a normal PC but is
# blocked by some corporate proxies and by the Claude Code for web container (measured
# 2026-08-20: the proxy answers 403 to CONNECT). A blocked download must NOT abandon the whole
# setup half-built - chromadb ships all-MiniLM-L6-v2 from a different host, the Brain scored on
# it for months, and brain_common.py keeps it selectable for exactly this reason.
MODEL="${AJ_BRAIN_EMBED_MODEL:-bge-small-en-v1.5}"
FALLBACK=""

if [ "$MODEL" = "bge-small-en-v1.5" ]; then
  if [ -s "$HERE/model-cache/model.onnx" ] && [ -s "$HERE/model-cache/tokenizer.json" ]; then
    ok "bge-small-en-v1.5 already downloaded"
  elif "$PY" "$HERE/embed_bge.py" --download >/dev/null 2>&1; then
    ok "bge-small-en-v1.5 downloaded"
  else
    FALLBACK=1
    MODEL="all-MiniLM-L6-v2"
    printf '  \033[33mSKIPPED\033[0m  could not reach huggingface.co\n'
    info "Falling back to all-MiniLM-L6-v2, which chromadb fetches from a different host."
    info "The Brain works fully on it - it is what every score line before 2026-08-20 used."
    info ""
    info "To use the better model later, from a machine that can reach huggingface.co:"
    info "    $PY $HERE/embed_bge.py --download"
    info "    $PY $HERE/brain_index.py --full"
  fi
fi
# Persist it - an exported variable dies with this shell and every later search would
# fall back to the BGE default against a MiniLM index. See brain_common.py.
export AJ_BRAIN_EMBED_MODEL="$MODEL"
if [ -n "$FALLBACK" ]; then
  printf '%s\n' "$MODEL" > "$HERE/embed-model.txt"
  info "remembered in semantic-index/embed-model.txt - delete it to go back to the default"
fi

if [ -z "$FALLBACK" ]; then
  info "smoke test - the model must tell a matching passage from a non-matching one:"
  "$PY" "$HERE/embed_bge.py" 2>&1 | sed 's/^/      /'
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

Nothing else needs doing. The Stop hook re-indexes after any turn that edits a file,
and search_brain works as an MCP tool call from here on.
EOF
