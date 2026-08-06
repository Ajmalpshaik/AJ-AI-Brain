"""
Shared settings for the AJ AI Brain semantic layer.

Both brain_index.py and brain_search.py import this. It is the ONE place that
decides where files are read from and where data is written to.

HARD RULE (Sophos / verify-fragments-compile.ps1 incident, 2026-08-04):
nothing may be written to %TEMP%, AppData\\Local\\Temp, or any system temp
folder. Every path below points inside SEMANTIC_ROOT, and the temp environment
variables are redirected there too, before chromadb is ever imported.
"""

import hashlib
import json
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

# Fingerprints of every file at the moment the index was last built. This is
# what makes "your index is out of date" detectable instead of silent.
MANIFEST_PATH = SEMANTIC_ROOT / "index-manifest.json"

# The site-word -> Revit-word table, read live at search time (NOT baked into
# the index), so adding a row works immediately without a rebuild.
VOCABULARY_PATH = BRAIN_ROOT / "knowledge" / "site-vocabulary.md"

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
# SITE VOCABULARY — say it your way, search it Revit's way
# --------------------------------------------------------------------------

def load_vocabulary():
    """
    Read knowledge/site-vocabulary.md into [(phrase, [extra words])].

    Sorted longest phrase first, so "floor level" is matched and consumed
    before a bare "floor" ever gets a chance. That ordering is the whole
    safety property: mapping "floor" to "level" would be wrong (a floor is a
    real Revit category - a slab), which is precisely the bug this fixes.

    Returns [] if the file is missing or unreadable. A missing vocabulary makes
    search slightly worse, never broken.
    """
    if not VOCABULARY_PATH.is_file():
        return []

    try:
        text, _ = read_text(VOCABULARY_PATH)
    except OSError:
        return []

    entries = []
    for line in text.split("\n"):
        line = line.strip()
        if not line.startswith("|") or line.startswith("|---"):
            continue
        cells = [c.strip() for c in line.strip("|").split("|")]
        if len(cells) < 2:
            continue
        phrase, expansion = cells[0].lower(), cells[1].lower()
        # Skip the header row and any row with an empty side.
        if not phrase or not expansion or phrase.startswith("you say"):
            continue
        words = re.findall(r"[a-z0-9]+", expansion)
        if words:
            entries.append((phrase, words))

    entries.sort(key=lambda e: -len(e[0]))
    return entries


def expand_query(query: str):
    """
    Swap any site phrase in the question for the Revit words that mean it.

    Returns (rewritten_text, [(phrase, [words]) that fired]).

    The matched phrase is REPLACED, not merely supplemented. Adding alone was
    measured and was not enough: "add 4 more floor levels" kept the word
    "floor", which went on dragging the result to create-floor.cs - the slab
    creator - no matter how much weight "level" got. When a row exists it is
    because that site word actively misleads, so leaving it in place defeats
    the row's whole purpose.

    Everything not matched is left exactly as typed, and a phrase is consumed
    once matched, so a longer phrase blocks its own shorter substring
    ("floor level" is taken before a bare "floor" is ever considered).
    """
    lowered = " " + re.sub(r"[^a-z0-9]+", " ", query.lower()) + " "
    added, fired = [], []

    for phrase, words in load_vocabulary():
        padded = " " + re.sub(r"[^a-z0-9]+", " ", phrase).strip() + " "
        if padded in lowered:
            lowered = lowered.replace(padded, " ")
            fired.append((phrase, words))
            added.extend(words)

    if not added:
        return query, []
    return (lowered.strip() + " " + " ".join(added)).strip(), fired


# --------------------------------------------------------------------------
# STALENESS — the index is a snapshot, so make going stale impossible to miss
# --------------------------------------------------------------------------

def iter_indexed_files():
    """
    Every file the index covers, as (path, repo-relative-path).

    Used by the staleness check to see what is on disk RIGHT NOW. The manifest
    it compares against is written from the list the indexer actually walked,
    so the two can only disagree if a file appeared or changed - which is
    exactly what we want to detect.
    """
    for folder_name, _area in INDEX_TARGETS:
        folder = BRAIN_ROOT / folder_name
        if not folder.exists():
            continue
        for path in sorted(folder.rglob("*")):
            if path.is_file() and path.suffix.lower() in FILE_EXTENSIONS:
                yield path, str(path.relative_to(BRAIN_ROOT)).replace("\\", "/")
    for name in ROOT_DOCS:
        path = BRAIN_ROOT / name
        if path.is_file():
            yield path, name


def fingerprint(path: Path) -> str:
    """
    Short content hash. Content, not modified-date, on purpose: a git checkout
    or a file copy changes the date without changing a word, and a staleness
    warning that cries wolf is one you learn to ignore.
    """
    try:
        return hashlib.sha1(path.read_bytes()).hexdigest()[:16]
    except OSError:
        return "unreadable"


def build_fingerprint() -> str:
    """
    A hash of everything that decides HOW a file becomes chunks.

    This is what makes an incremental rebuild safe. Re-embedding only the files
    that changed is correct ONLY while the chunking rules stay identical -
    change CHUNK_TARGET, add a ROOT_DOC, alter the splitter, and every chunk
    already stored is the wrong shape, but their files are untouched so a
    file-by-file comparison would happily skip all of them.

    Hashing the two source files is deliberately blunt: editing even a comment
    forces one full rebuild. That is the right trade - a needless 80-second
    rebuild costs a minute, while a silently half-migrated index is the kind of
    fault you only discover through months of quietly worse answers.
    """
    parts = [
        str(CHUNK_TARGET), str(CHUNK_MAX), str(CHUNK_OVERLAP),
        repr(sorted(FILE_EXTENSIONS)), repr(INDEX_TARGETS), repr(ROOT_DOCS),
        "all-MiniLM-L6-v2",
    ]
    for src in (SEMANTIC_ROOT / "brain_common.py",
                SEMANTIC_ROOT / "brain_index.py"):
        try:
            parts.append(hashlib.sha1(src.read_bytes()).hexdigest())
        except OSError:
            parts.append("unreadable")
    return hashlib.sha1("|".join(parts).encode("utf-8")).hexdigest()[:16]


def read_manifest():
    """The last build's record, or None if absent/unreadable."""
    if not MANIFEST_PATH.is_file():
        return None
    try:
        data = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return None
    return data if isinstance(data.get("files"), dict) else None


def write_manifest(rel_paths) -> None:
    """Record what the index was built from. Called after a successful build."""
    entries = {}
    for rel in rel_paths:
        path = BRAIN_ROOT / rel
        if path.is_file():
            entries[rel.replace("\\", "/")] = fingerprint(path)
    MANIFEST_PATH.write_text(
        json.dumps({"build": build_fingerprint(), "files": entries}, indent=1),
        encoding="utf-8")


def check_staleness():
    """
    Compare the Brain on disk against what the index was built from.

    Returns a dict: {"known": bool, "stale": bool, "changed": [], "added": [],
    "removed": []}. "known" is False when there is no manifest yet - an index
    built before this check existed, which is unknown rather than stale.
    """
    result = {"known": False, "stale": False,
              "changed": [], "added": [], "removed": []}

    manifest = read_manifest()
    if manifest is None:
        return result

    recorded = manifest["files"]
    result["known"] = True
    seen = set()

    for path, rel in iter_indexed_files():
        seen.add(rel)
        if rel not in recorded:
            result["added"].append(rel)
        elif recorded[rel] != fingerprint(path):
            result["changed"].append(rel)

    result["removed"] = sorted(set(recorded) - seen)
    result["stale"] = bool(result["changed"] or result["added"]
                           or result["removed"])
    return result


def staleness_banner():
    """
    A short warning to print above search results, or "" when all is well.

    This exists because the index going stale is silent by nature: it keeps
    answering, just from an older copy of the Brain. Nothing about a wrong
    answer looks wrong.
    """
    state = check_staleness()

    if not state["known"]:
        return ("NOTE: cannot tell whether this index is current (no manifest "
                "- built before staleness tracking). Run index-brain.cmd once "
                "to start tracking.")
    if not state["stale"]:
        return ""

    bits = []
    for label, key in (("changed", "changed"), ("new", "added"),
                       ("deleted", "removed")):
        if state[key]:
            bits.append(f"{len(state[key])} {label}")

    lines = [
        "!" * 74,
        "STALE INDEX - these results are from an OLDER copy of the Brain.",
        f"  {', '.join(bits)} since the last rebuild.",
    ]
    for key in ("changed", "added", "removed"):
        for rel in state[key][:4]:
            lines.append(f"    {key[:7]:<8} {rel}")
        if len(state[key]) > 4:
            lines.append(f"    {'':<8} ... and {len(state[key]) - 4} more")
    lines.append("  FIX: run  semantic-index\\index-brain.cmd  (~90 s)")
    lines.append("!" * 74)
    return "\n".join(lines)


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
