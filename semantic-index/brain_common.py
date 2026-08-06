"""
Shared settings for the AJ AI Brain semantic layer.

Both brain_index.py and brain_search.py import this. It is the ONE place that
decides where files are read from and where data is written to.

HARD RULE (Sophos / verify-fragments-compile.ps1 incident, 2026-08-04):
nothing may be written to %TEMP%, AppData\\Local\\Temp, or any system temp
folder. Every path below points inside SEMANTIC_ROOT, and the temp environment
variables are redirected there too, before chromadb is ever imported.
"""

import os
import re
import sys
from pathlib import Path

# --------------------------------------------------------------------------
# PATHS — the only settings you would ever need to change
# --------------------------------------------------------------------------

# The AJ AI Brain repo being indexed (read-only — nothing here is ever written).
BRAIN_ROOT = Path(r"D:\Ajmal\AJ AI Brain")

# Everything this layer creates lives under here, and nowhere else.
SEMANTIC_ROOT = BRAIN_ROOT / "semantic-index"

CHROMA_DIR = SEMANTIC_ROOT / "chroma-db"      # the vector database
MODEL_DIR = SEMANTIC_ROOT / "model-cache"     # the downloaded embedding model
RUN_TEMP = SEMANTIC_ROOT / "run-temp"         # scratch space, kept off %TEMP%

COLLECTION_NAME = "aj_brain"

# Folders inside the Brain that get indexed, and the label each one carries.
# Order matters only for the report.
INDEX_TARGETS = [
    ("scripts", "fragment"),
    ("knowledge", "knowledge"),
    ("skills", "skill"),
]

# Individual files at the repo root, indexed under the label "guide".
# These are the read-me-first documents, so a plain-English question can land
# on the operating manual rather than only on a fragment or a technique note.
#
# CLAUDE.md sits here too, under the same "guide" label rather than a category
# of its own. It is the auto-loaded session rules, and START-HERE.md — which it
# pulls in with @START-HERE.md — is already a guide doing the same job. A label
# used by exactly one file adds a choice to every search without adding an
# answer, so these five are kept together as "the documents you read first".
ROOT_DOCS = [
    "AGENT-SPEC.md",
    "START-HERE.md",
    "README.md",
    "SETUP.md",
    "CLAUDE.md",
]

FILE_EXTENSIONS = {".md", ".cs"}

# --------------------------------------------------------------------------
# CHUNK SIZE
# --------------------------------------------------------------------------
# The embedding model (all-MiniLM-L6-v2) only reads about 256 word-pieces,
# which is roughly 1,000 characters of English. Text past that point is
# silently ignored, so chunks are kept under it rather than sent oversized.
CHUNK_TARGET = 900   # aim for this
CHUNK_MAX = 1100     # never exceed this
CHUNK_OVERLAP = 150  # repeat this much between consecutive body chunks


# --------------------------------------------------------------------------
# SETUP — must run before chromadb is imported
# --------------------------------------------------------------------------

def prepare_environment() -> None:
    """Create our folders and force every temp path inside SEMANTIC_ROOT."""
    for folder in (SEMANTIC_ROOT, CHROMA_DIR, MODEL_DIR, RUN_TEMP):
        folder.mkdir(parents=True, exist_ok=True)

    # Redirect anything that might reach for a system temp folder.
    for var in ("TEMP", "TMP", "TMPDIR"):
        os.environ[var] = str(RUN_TEMP)

    # Console must be UTF-8: the Brain is full of em dashes, arrows and ✓.
    try:
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")
    except Exception:
        pass


def get_embedding_function():
    """
    Return the local embedding model, with its cache forced into MODEL_DIR.

    By default chromadb caches this model in C:\\Users\\<you>\\.cache\\chroma.
    DOWNLOAD_PATH is a class attribute, so overriding it here moves the whole
    download inside our one confirmed folder.
    """
    from chromadb.utils.embedding_functions.onnx_mini_lm_l6_v2 import (
        ONNXMiniLM_L6_V2,
    )

    ONNXMiniLM_L6_V2.DOWNLOAD_PATH = MODEL_DIR / ONNXMiniLM_L6_V2.MODEL_NAME
    return ONNXMiniLM_L6_V2()


def get_client():
    """Open the on-disk Chroma database in CHROMA_DIR."""
    import chromadb
    from chromadb.config import Settings

    return chromadb.PersistentClient(
        path=str(CHROMA_DIR),
        settings=Settings(anonymized_telemetry=False, allow_reset=True),
    )


# --------------------------------------------------------------------------
# SHARED HELPERS
# --------------------------------------------------------------------------

def read_text(path: Path):
    """
    Read a file as UTF-8, tolerating a byte-order mark.

    Returns (text, had_bad_bytes). had_bad_bytes is True if any character
    could not be decoded — the indexer reports those rather than hiding them.
    """
    raw = path.read_bytes()
    try:
        return raw.decode("utf-8-sig"), False
    except UnicodeDecodeError:
        return raw.decode("utf-8-sig", errors="replace"), True


_SENTENCE_BREAK = re.compile(r"(?<=[.!?])\s+")


def _atomise(text: str, hard_max: int):
    """
    Break text down until every piece fits, always on the best boundary
    available: paragraph, then line, then sentence, then word.

    The sentence step matters more than it looks. A SKILL.md frontmatter
    `description:` is one single unbroken line — often over 1,000 characters —
    that glues together the skill's purpose AND several "Do NOT use this for X"
    routing clauses. Without a sentence split it stayed whole, overflowed the
    model's ~1,000-character reading window, and averaged the useful trigger
    phrases together with the negations until neither matched well.
    """
    out = []
    for para in text.split("\n\n"):
        para = para.strip()
        if not para:
            continue
        if len(para) <= hard_max:
            out.append(para)
            continue

        for line in para.split("\n"):
            line = line.strip()
            if not line:
                continue
            if len(line) <= hard_max:
                out.append(line)
                continue

            for sentence in _SENTENCE_BREAK.split(line):
                sentence = sentence.strip()
                if not sentence:
                    continue
                if len(sentence) <= hard_max:
                    out.append(sentence)
                    continue

                # Last resort: pack words up to the limit.
                buf = ""
                for word in sentence.split(" "):
                    if buf and len(buf) + len(word) + 1 > hard_max:
                        out.append(buf)
                        buf = ""
                    buf = (buf + " " + word) if buf else word
                    while len(buf) > hard_max:  # one absurdly long token
                        out.append(buf[:hard_max])
                        buf = buf[hard_max:]
                if buf.strip():
                    out.append(buf.strip())

    return out


def split_to_size(text: str, target: int = CHUNK_TARGET,
                  hard_max: int = CHUNK_MAX,
                  overlap: int = CHUNK_OVERLAP):
    """
    Cut text into model-sized pieces. Every piece returned is guaranteed to be
    hard_max characters or fewer, so nothing is silently truncated by the
    model. Returns a list of strings (empty ones dropped).
    """
    text = text.strip()
    if not text:
        return []
    if len(text) <= hard_max:
        return [text]

    pieces = []
    buf = ""

    for unit in _atomise(text, hard_max):
        candidate = (buf + "\n" + unit) if buf else unit
        if len(candidate) <= target:
            buf = candidate
            continue

        if buf:
            pieces.append(buf)
            # Carry a little context forward so a thought split across two
            # chunks is still findable from either side.
            tail = buf[-overlap:] if overlap else ""
            if tail and len(tail) + len(unit) + 1 <= hard_max:
                buf = tail + "\n" + unit
            else:
                buf = unit
        else:
            buf = unit

    if buf.strip():
        pieces.append(buf.strip())

    return [p for p in pieces if p.strip()]
