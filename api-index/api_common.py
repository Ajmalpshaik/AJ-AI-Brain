"""Shared settings for the SEPARATE Revit API index.

THIS IS NOT THE BRAIN'S INDEX. It is a second, independent Chroma database that
`ask-brain-hybrid` never opens. Three independent reasons it cannot leak in:

  1. different directory   — api-index/chroma-db-api, not semantic-index/chroma-db
  2. different collection  — "revit_api", not "aj_brain"
  3. outside the Brain's indexed folders — brain_common.INDEX_TARGETS is
     scripts/ knowledge/ skills/ plus a fixed root-doc list; api-index/ is in none

Why that separation is not optional (measured 2026-08-20): the Revit API is
~1,700 classes and 30,000+ members against the Brain's 3,786 chunks. Indexed
together, the Brain becomes ~11% of its own index and every modelling question
lands on a reference page. See knowledge/revit-api-surface.md.

The embedding model is REUSED from semantic-index — same model, so no second
download and the two indexes stay comparable if ever compared deliberately.
"""

import os
import sys
from pathlib import Path

API_ROOT = Path(os.environ.get("AJ_API_INDEX_ROOT") or Path(__file__).resolve().parent)
BRAIN_ROOT = API_ROOT.parent
SEMANTIC_ROOT = BRAIN_ROOT / "semantic-index"

CORPUS_DIR = API_ROOT / "corpus"              # written by scripts/context/harvest-revit-api.cs
CHROMA_DIR = API_ROOT / "chroma-db-api"       # the SEPARATE vector database
RUN_TEMP = API_ROOT / "run-temp"              # scratch, kept off the system temp folder
COLLECTION_NAME = "revit_api"

# One API type per chunk is the natural retrieval unit — you look up "what is
# FilteredElementCollector", not "the middle of the DB namespace". Types with
# hundreds of members are split by the shared splitter so nothing is truncated.
CHUNK_TARGET = 900
CHUNK_MAX = 1100
CHUNK_OVERLAP = 120


def brain():
    """The Brain's own settings module — reused for the embedding model only."""
    sys.path.insert(0, str(SEMANTIC_ROOT))
    import brain_common
    return brain_common


def prepare():
    for folder in (API_ROOT, CHROMA_DIR, RUN_TEMP):
        folder.mkdir(parents=True, exist_ok=True)
    for var in ("TEMP", "TMP", "TMPDIR"):
        os.environ[var] = str(RUN_TEMP)
    try:
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")
    except Exception:
        pass


def client():
    import chromadb
    from chromadb.config import Settings
    return chromadb.PersistentClient(
        path=str(CHROMA_DIR),
        settings=Settings(anonymized_telemetry=False, allow_reset=True),
    )
