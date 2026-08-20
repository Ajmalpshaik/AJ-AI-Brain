"""
Hybrid search: meaning AND exact words, merged into one ranked list.

    "D:\\Ajmal\\AJ AI Brain\\semantic-index\\ask-brain-hybrid.cmd" ^
        "how many diffusers do I need in this room"

WHY THIS EXISTS
---------------
brain_search.py (semantic only) matches on meaning. That is powerful, but it
has one blind spot found by real testing on 2026-08-06: "how many diffusers do
I need in this room" ranks sprinkler files above the air-terminal skill,
because the SHAPE of the question ("count devices in a room") outweighs the one
word that says WHICH device. Both jobs are device-counting; only one is about
diffusers.

Exact-word search does not have that blind spot — but on its own it is worse,
because it cannot match "air is not too fast" to a note about 5 m/s velocity.

So this runs both and merges them. brain_search.py is left untouched as the
semantic-only baseline, so the two can be compared.

THREE SIGNALS
-------------
1. MEANING   - the existing Chroma search.
2. WORDS     - BM25 over the exact same chunks Chroma holds, so the two layers
               can never drift apart. BM25 is standard keyword ranking with
               three properties that matter here:
                 * rare words count more ("diffuser" is in 12 files, "room" in
                   57, so diffuser carries far more weight)
                 * repeated words count more, with diminishing returns
                 * a match in a SHORT chunk counts more than in a long one
3. PROVEN    - tools/fragment-index.mjs --json, read never written. It supplies
               the same match test its own --find uses (path + purpose + input
               fields) and, more usefully, each fragment's verification status,
               which nothing else here knows.

HOW THEY MERGE
--------------
Reciprocal Rank Fusion. The two scores are on incompatible scales - closeness
is 0-100, BM25 is unbounded - so adding them directly would be meaningless.
Instead each list votes by POSITION: being near the top of either list earns
points, and being near the top of both wins. Standard, robust, and it needs no
tuning to stay sane when one side returns nothing useful.
"""

import argparse
import hashlib
import json
import math
import re
import subprocess
import sys
from collections import Counter

import brain_common as cfg

cfg.prepare_environment()  # must happen before chromadb is imported

AREA_LABEL = {
    "fragment": "FRAGMENT",
    "knowledge": "KNOWLEDGE",
    "skill": "SKILL",
    "guide": "GUIDE",
}
AREA_CHOICES = ["fragment", "knowledge", "skill", "guide"]

# How many chunks to pull from each side before merging. Bigger than the final
# result count, so the merge has material to work with.
CANDIDATES = 80

# How many CHUNKS to pull in order to end up with CANDIDATES distinct FILES.
#
# MEASURED, 2026-08-20. Asking Chroma for 80 chunks returned only 32-37 distinct
# files on the four questions that were failing outright - 43 to 48 of the 80
# slots went to repeat chunks of the SAME file (one sprinkler note supplied ten
# of them). So the "80-candidate pool" was really a 33-candidate pool, and the
# right answer sat just outside it. That is a retrieval loss no re-ranker can
# repair, and it is invisible on the score line.
#
# Over-fetch then de-duplicate. HNSW cost is near-flat in n_results at this size,
# so this is paid for in microseconds.
#
# HONEST RESULT: the multiplier itself measured NEUTRAL. Swept x1/x2/x3/x4/x6/x10
# on the 14 questions - identical #1, top-3, top-5 and retrievable at every value,
# MRR within 0.003. The gain that showed up (MRR 0.299 -> 0.323, retrievable
# 10 -> 11, top-3 3 -> 5) came entirely from _rank_files() re-ranking over FILES,
# not from fetching more chunks. Kept at 3 because it makes the pool genuinely
# hold CANDIDATES distinct files as documented, and gives the cross-encoder more
# to re-rank if it is ever switched on - not because it is worth anything today.
CHUNK_OVERFETCH = 3

# Reciprocal Rank Fusion. K flattens the curve so rank 1 does not utterly
# dominate rank 2; 60 is the value the original RRF paper settled on.
RRF_K = 60
WEIGHT_MEANING = 1.0
WEIGHT_WORDS = 1.0
# A fragment that tools/fragment-index.mjs would itself have returned for one
# of the rare words in the question. Deliberately small: it is a tie-breaker
# among fragments, not a third opinion strong enough to overturn both lists.
WEIGHT_PROVEN_TOOL = 0.25

# Two gates decide which words may trigger the fragment-index signal, which is
# fragments-only and so must never fire on a word that is merely ordinary.
#
# An absolute ceiling: a word in more than this share of chunks is common, full
# stop. And a relative one: within a single question, only the rarest words
# count. A fixed threshold alone was not enough - measured per FILE "room" is in
# 19% of the Brain and looks common, but measured per CHUNK it is only ~8% and
# slipped through, boosting create-rooms-in-enclosed-regions.cs over the skill
# that actually answers the question. The relative gate is self-tuning and does
# not depend on picking the right denominator.
DISTINCTIVE_MAX_SHARE = 0.15
DISTINCTIVE_IDF_RATIO = 0.8

# BM25 constants. k1 controls how fast repeated words stop helping; b controls
# how much a long chunk is penalised. These are the standard defaults.
BM25_K1 = 1.5
BM25_B = 0.75

# Not every kind of chunk is equally good evidence that a file answers a
# question, so a word found in one counts for less than the same word found in
# another. Independent testing on 24 realistic questions (2026-08-06) showed
# this is where a third of the wrong answers came from:
#
#   "duplicate option not working" matched an input field named duplicateOption
#   "near 40 rooms"                matched a default value maxSegmentsPerRoom = 40
#
# An INPUTS block is a form to fill in, and a code body is full of incidental
# variable names. Neither describes what a fragment is FOR. A PURPOSE header or
# a paragraph of prose does, so those keep full weight.
KIND_WEIGHT = {
    "card": 1.0,      # filename + PURPOSE - what this thing is
    "source": 1.0,    # the SOURCE: cross-reference
    "section": 1.0,   # prose under a markdown heading
    "inputs": 0.45,   # a form to fill in, not a description
    "code": 0.35,     # variable names are incidental
}

# A few files are records of WHAT CHANGED, not answers to HOW DO I. They earn
# their place when you ask about this Brain's own history, and get in the way
# otherwise - so their final score is discounted rather than excluded.
#
# brain-log.md is the clearest case and the reason this exists. It is by far the
# largest file here, so it has the most surface area to match by accident, and
# it describes every problem ever solved - including, verbatim, the test
# questions those problems were found with. Documenting the diffuser failure
# honestly promptly made the log itself the #1 answer to "how many diffusers do
# I need in this room", displacing the skill that actually answers it. A log
# that outranks the thing it describes is a log that has eaten the search.
# MEASURED TRADE-OFF, not a tuned sweet spot. RRF scores sit within a few
# thousandths of each other, so this file falls off a cliff rather than sliding:
#
#   weight 1.00  "dated log of changes"  -> brain-log #1 (right)
#                "how many diffusers"    -> brain-log #1 (wrong)
#   weight 0.85  "dated log of changes"  -> not in top 4 (wrong)
#                "how many diffusers"    -> the skill #1 (right)
#
# There is no value that gets both. 0.85 is chosen because a real work question
# is asked constantly and a "show me the changelog" question almost never - and
# when you do want the log, opening the file beats searching for it. Documented
# in README.md so the behaviour is not mistaken for a bug later.
#
# glossary.md joined the list on 2026-08-13, for the same structural reason and caught by
# the same question. It maps Ajmal's words to Revit meanings, so it gains a row every time
# a term causes confusion - and a file of question-shaped phrases matches MORE questions the
# bigger it gets. It had quietly taken #1 from ajtools-hvac-terminal-layout on "how many
# diffusers do I need in this room" - the exact displacement brain-log.md was discounted for
# - while README.md still claimed that question ranked the skill first. Found by
# semantic-index/score_brain.py on its first ever run; nobody had noticed in the week since.
#
# UNLIKE brain-log.md, this one could NOT take 0.85, and the difference matters: looking a
# word up IS a real question and the glossary is the right answer to it, so the discount has
# to fix the displacement without sinking the file's own job. Swept, both cases per weight:
#
#   weight   "how many diffusers" -> skill   "what does duck mean" -> glossary
#   1.00     #2  wrong                       #1  right
#   0.98     #2  wrong                       #1  right
#   0.96     #1  right                       #1  right
#   0.93     #1  right                       #1  right     <- chosen
#   0.90     #1  right                       #1  right
#   0.85     #1  right                       #2  WRONG - nfpa13-sprinkler-spacing.md took #1
#
# The window where both hold is 0.90-0.96. 0.93 is its centre, chosen to sit as far as
# possible from both cliffs rather than next to one - these scores sit within thousandths of
# each other, so a value at the edge is one new file away from flipping.
PATH_WEIGHT = {
    "knowledge/brain-log.md": 0.85,
    "knowledge/glossary.md": 0.93,
}

# A prior on WHAT KIND of file should win a "how do I" question, applied after
# fusion. Everything defaults to 1.0; only a measured value belongs here.
#
# BUILT AND LEFT OFF, 2026-08-20 - the same call as the cross-encoder in rerank.py.
#
# The structural argument for boosting skills is real: 11 skill files compete with
# 282 fragments for the same questions, so without a prior the fragments win on
# sheer volume. And the sweep looks tempting:
#
#   weight   skill-Qs #1   other-Qs #1   overall #1   MRR
#   1.00       1 / 7         2 / 7           3        0.323
#   1.10       3 / 7         1 / 7           4        0.412
#   1.20       3 / 7         1 / 7           4        0.424
#   1.35       3 / 7         0 / 7           3        0.375
#
# It is off because of the middle column. It does not FIND anything new - it moves
# wins from one half of the test set to the other and nets +1, on a set that splits
# exactly 7 skill / 7 non-skill. That 50/50 split is an artefact of 14 questions,
# not a measurement of what gets asked, so tuning a global prior on it is fitting
# the sample rather than the job. 1.35 collapsing back to 3 shows how narrow the
# window is.
#
# Turn it on ONLY after test-questions.md reaches ~30 rows AND the skill/non-skill
# mix reflects real questions. Then re-run this sweep; if 1.1-1.2 still wins on
# BOTH columns, it has earned its place.
AREA_WEIGHT = {}


# NO CONFIDENCE FLOOR - MEASURED, 2026-08-20.
#
# "Say nothing here answers this, instead of a confident wrong #1" is an obvious
# idea and it does not work on this Brain. Measured on all 14 questions, top-1
# closeness for a CORRECT top hit was 35.5 / 43.4 / 65.1, and for a WRONG one it
# ranged 27.8 to 56.6. The distributions overlap almost completely, the fused
# score sits in 0.030-0.037 either way, and every single query fires both signals -
# so there is no threshold that rejects wrong answers without rejecting right ones
# at about the same rate.
#
# Written down so the idea is not rebuilt from intuition later. Revisit only with
# a signal that actually separates the two, not a tuned cut-off on these.

# Words so common they carry no signal. Kept short on purpose - BM25's rarity
# weighting already drives "the" and "how" towards zero on its own.
STOPWORDS = {
    "a", "an", "and", "are", "as", "at", "be", "but", "by", "do", "does",
    "for", "from", "how", "i", "if", "in", "is", "it", "my", "of", "on", "or",
    "that", "the", "then", "there", "these", "this", "to", "was", "what",
    "when", "where", "which", "will", "with", "you", "your",
}


# --------------------------------------------------------------------------
# TEXT HANDLING
# --------------------------------------------------------------------------

def _stem(word: str) -> str:
    """
    Crude plural stripping so "diffusers" and "diffuser" are the same word.

    Deliberately not a real stemmer. It only has to be applied identically to
    the question and to the corpus; consistency matters, linguistic accuracy
    does not.
    """
    if len(word) > 3 and word.endswith("s") and not word.endswith("ss"):
        return word[:-1]
    return word


def tokenise(text: str):
    """Lowercase, split on anything that is not a letter or digit, stem."""
    return [_stem(w) for w in re.findall(r"[a-z0-9]+", text.lower())
            if len(w) > 1]


def _raw_words(text: str):
    """Lowercase words, NOT stemmed."""
    return [w for w in re.findall(r"[a-z0-9]+", text.lower()) if len(w) > 1]


def query_terms(query: str):
    """
    Question -> the words worth searching for, stemmed.

    Stopwords are removed BEFORE stemming, not after. Getting this backwards
    let "this" survive: it stems to "thi", which is not in the stopword list,
    so a meaningless three-letter fragment went into the search and matched
    inside "within", "something" and "this" all over the corpus.
    """
    kept = [w for w in _raw_words(query)
            if w not in STOPWORDS and not w.isdigit()]
    if not kept:
        kept = [w for w in _raw_words(query) if not w.isdigit()]
    return [_stem(w) for w in kept]


def query_words_raw(query: str):
    """
    The words to hand to fragment-index.mjs's substring test, unstemmed.

    Its --find takes a whole word a human typed, so this mirrors that: real
    words only, nothing shorter than four characters, and never a bare number.
    Substring matching on short stems is what produced the "thi" disaster; bare
    numbers are worse, because "near 40 rooms" matched a fragment whose input
    block happens to read `maxSegmentsPerRoom = 40`.
    """
    return [w for w in _raw_words(query)
            if w not in STOPWORDS and len(w) >= 4 and not w.isdigit()]


# --------------------------------------------------------------------------
# BM25
# --------------------------------------------------------------------------

class BM25:
    """Standard BM25 over a fixed list of documents."""

    def __init__(self, docs_tokens):
        self.docs = docs_tokens
        self.n = len(docs_tokens)
        self.lengths = [len(d) for d in docs_tokens]
        self.avg_len = (sum(self.lengths) / self.n) if self.n else 0.0
        self.freqs = [Counter(d) for d in docs_tokens]

        doc_freq = Counter()
        for tokens in docs_tokens:
            for term in set(tokens):
                doc_freq[term] += 1
        self.doc_freq = doc_freq

    def idf(self, term: str) -> float:
        """How much this word narrows things down. Rare word -> big number."""
        df = self.doc_freq.get(term, 0)
        if df == 0:
            return 0.0
        return math.log(1 + (self.n - df + 0.5) / (df + 0.5))

    def scores(self, terms):
        """Score every document against these terms. Returns a list of floats."""
        out = [0.0] * self.n
        for term in terms:
            idf = self.idf(term)
            if idf <= 0:
                continue
            for i, freq in enumerate(self.freqs):
                f = freq.get(term, 0)
                if not f:
                    continue
                norm = 1 - BM25_B + BM25_B * (self.lengths[i] / self.avg_len)
                out[i] += idf * (f * (BM25_K1 + 1)) / (f + BM25_K1 * norm)
        return out


# --------------------------------------------------------------------------
# fragment-index.mjs — READ ONLY, never modified
# --------------------------------------------------------------------------

_FRAGMENT_CACHE_PATH = cfg.SEMANTIC_ROOT / "fragment-index-cache.json"
_fragment_memo = None


def _scripts_fingerprint():
    """
    Cheap signature of scripts/ - stat only, no file contents read.

    Deliberately NOT a content hash, which is the opposite of the choice made
    for the index manifest, and for a reason worth stating. There, a false
    *miss* meant a needless 92-second rebuild, so contents were worth reading.
    Here a false miss costs one node subprocess, so the stat is the better
    trade: a git checkout moves every mtime, misses the cache, re-runs node
    once and is correct. The dangerous direction - a false *hit* - needs a
    fragment to change while keeping both its size and its modified time to the
    nanosecond, which does not happen in practice.
    """
    parts = []
    scripts = cfg.BRAIN_ROOT / "scripts"
    for path in sorted(scripts.rglob("*.cs")):
        try:
            st = path.stat()
            parts.append(f"{path.name}:{st.st_size}:{st.st_mtime_ns}")
        except OSError:
            parts.append(f"{path.name}:?")
    return hashlib.sha1("|".join(parts).encode("utf-8")).hexdigest()[:16]


def load_fragment_index():
    """
    The fragment list from `node tools/fragment-index.mjs --json`, cached.

    Returns (fragments, note). On any failure the note explains why and the
    caller carries on with the other two signals rather than dying - but the
    note is always printed, so a silent downgrade is impossible.

    WHY THE CACHE. This was spawning a Node process on EVERY query, to re-read
    and re-parse all 290 `.cs` fragments for data that only changes when one of
    those files changes. Measured 2026-08-21 it was the single largest cost in
    a search - larger than embedding the question and the vector lookup put
    together, by a wide margin. Almost every search is its own short-lived
    process, so an in-memory cache alone would have helped nothing; the result
    is written to disk and re-read on the next run.
    """
    global _fragment_memo
    if _fragment_memo is not None:
        return _fragment_memo

    fingerprint = _scripts_fingerprint()
    try:
        cached = json.loads(_FRAGMENT_CACHE_PATH.read_text(encoding="utf-8"))
        if cached.get("fingerprint") == fingerprint:
            _fragment_memo = (cached["fragments"], None)
            return _fragment_memo
    except (OSError, ValueError, KeyError):
        pass

    result = _run_fragment_index()
    if result[0]:  # only cache a good answer, never an error state
        try:
            # Atomic, for the same reason as the corpus vocabulary: two searches
            # can run at once, and half a cache file is worse than none.
            cfg.atomic_write_text(
                _FRAGMENT_CACHE_PATH,
                json.dumps({"fingerprint": fingerprint, "fragments": result[0]}))
        except OSError:
            pass
    _fragment_memo = result
    return result


def _run_fragment_index():
    script = cfg.BRAIN_ROOT / "tools" / "fragment-index.mjs"
    if not script.is_file():
        return [], f"not found: {script}"
    try:
        proc = subprocess.run(
            ["node", str(script), "--json"],
            capture_output=True, text=True, encoding="utf-8",
            cwd=str(cfg.BRAIN_ROOT), timeout=60,
        )
    except FileNotFoundError:
        return [], "node is not on PATH"
    except subprocess.TimeoutExpired:
        return [], "timed out after 60s"

    if proc.returncode != 0:
        return [], f"exited {proc.returncode}: {(proc.stderr or '').strip()[:200]}"
    try:
        return json.loads(proc.stdout), None
    except json.JSONDecodeError as exc:
        return [], f"output was not valid JSON: {exc}"


def fragment_tool_hits(fragments, words):
    """
    Which fragments would `fragment-index.mjs --find <word>` have returned?

    Mirrors its own filter (tools/fragment-index.mjs lines 165-171): a
    case-insensitive substring test against the path, the PURPOSE text, and the
    input field names and notes.

    `words` must already be filtered down to DISTINCTIVE words - see the caller.
    This signal can only ever promote fragments, never a skill or a knowledge
    note, so letting a common word like "room" trigger it would quietly bias
    every general question towards C# and away from the skill that answers it.

    Returns {repo-relative path: set of matched words}.
    """
    hits = {}
    for frag in fragments:
        # PURPOSE only - deliberately tighter than fragment-index.mjs's own
        # --find, which also searches the path and the input fields. Testing
        # showed those two channels produce keyword accidents rather than
        # answers: the ordinary English verb "copy" hit action-copy-elements.cs
        # for a question about what to fill in before running a fragment, and
        # "folder" hit action-export-parameters-to-csv.cs. A word in the PURPOSE
        # line means the fragment is ABOUT that thing; a word in its path or
        # input list often means nothing at all.
        haystack = frag.get("purpose", "").lower()
        matched = {w for w in words if w in haystack}
        if matched:
            hits["scripts/" + frag["path"]] = matched
    return hits


def fragment_status_map(fragments):
    """{repo-relative path: human-readable verification status}."""
    label = {
        "verified": "PROVEN", "untested": "not run", "blocked": "blocked",
        "impossible": "impossible", "no-status": "unproven",
        "not-in-readme": "?",
    }
    return {"scripts/" + f["path"]: label.get(f.get("status"), "?")
            for f in fragments}


# --------------------------------------------------------------------------
# THE SEARCH
# --------------------------------------------------------------------------

def _apply_rerank(query, order):
    """Re-score the top of the merged list with the cross-encoder, and reorder it.

    Only the head is re-scored - the cross-encoder reads question and chunk together, which
    is what makes it accurate and far too slow to run over everything. The tail keeps its
    RRF order and stays below the head, which is exactly what a re-ranker is for: it
    reorders the shortlist, it cannot promote something the first stage never retrieved.

    PATH_WEIGHT is re-applied here, and that detail matters. The discounts on brain-log.md
    and glossary.md were measured against RRF scores; if the cross-encoder simply overwrote
    the ordering, both would quietly come back and undo a fix made the same day. The logit
    is squashed to 0-1 first so the multiplication has the right sign - multiplying a
    NEGATIVE logit by 0.93 would make it larger, i.e. reward the file being discounted.

    Returns the original order untouched if the model is missing or scoring fails. A
    re-ranker that breaks must not take down a search that works without it.
    """
    try:
        import rerank as _rr
    except Exception:
        return order

    if not _rr.available():
        return order

    head, tail = order[:_rr.RERANK_DEPTH], order[_rr.RERANK_DEPTH:]
    if not head:
        return order

    # The ORIGINAL question, not the vocabulary-expanded one. Expansion appends bare Revit
    # keywords, which help a bi-encoder match but read as noise to a model that is genuinely
    # reading the sentence.
    scores = _rr.score(query, [entry["payload"]["snippet"] for _path, entry in head])
    if scores is None or len(scores) != len(head):
        return order

    for (path, entry), logit in zip(head, scores):
        prob = 1.0 / (1.0 + math.exp(-logit))
        entry["rerank"] = round(logit, 3)
        entry["_rr"] = prob * PATH_WEIGHT.get(path, 1.0)

    head.sort(key=lambda kv: -kv[1]["_rr"])
    return head + tail


def _best_per_file(pairs):
    """pairs = [(path, rank, payload)] -> {path: (best_rank, payload)}."""
    out = {}
    for path, rank, payload in pairs:
        if path not in out or rank < out[path][0]:
            out[path] = (rank, payload)
    return out


def _rank_files(pairs, limit=CANDIDATES):
    """Collapse chunk hits to files, keep the best `limit`, renumber them 1..N.

    The renumbering matters. RRF fuses ranked lists of the things being ranked,
    and the things here are FILES - but the ranks arriving from Chroma and BM25
    are CHUNK ranks. Leaving them alone made a file's fused score depend on how
    many chunks the files above it happened to be split into: a correct answer
    sitting behind one heavily-chunked note was scored as rank 40 rather than
    rank 5, purely because that note is long.
    """
    best = _best_per_file(pairs)
    ordered = sorted(best.items(), key=lambda kv: kv[1][0])[:limit]
    return {path: (position, payload)
            for position, (path, (_chunk_rank, payload))
            in enumerate(ordered, start=1)}


def hybrid_search(query, top_k=5, area=None, use_fragment_tool=True, use_rerank=False):
    """
    Search by meaning and by exact words, then merge.

    Returns (results, notes). Each result is a dict with the source path, the
    area, the combined score, the rank it held in each list, and a snippet.
    """
    notes = []

    # Site vocabulary: add the Revit words for any site phrase used. Applied to
    # the WORDS side only - the meaning side keeps the question exactly as
    # asked, because appending synonyms shifts the sentence's own meaning
    # vector and the semantic layer already handles paraphrase well. Widening
    # the exact-word search is where "floor level" -> "level" actually pays.
    expanded, fired = cfg.expand_query(query)
    if fired:
        shown = "; ".join(f"{p} -> {' '.join(w)}" for p, w in fired)
        notes.append(f"site vocabulary applied: {shown}")

    # Then spelling, on whatever the vocabulary did not already claim. Same
    # words-side-only reasoning as above, and it applies harder here: a typo is
    # worth nothing at all to BM25, while the meaning side copes with it through
    # sub-word tokens. Corrections are added, never substituted - see
    # cfg.correct_spelling for why that is the opposite choice from expansion.
    expanded, spelled = cfg.correct_spelling(expanded)
    if spelled:
        shown = "; ".join(f"{t} -> {c}" for t, c in spelled)
        notes.append(f"spelling: {shown}")

    terms = query_terms(expanded)

    collection = cfg.get_client().get_collection(
        name=cfg.COLLECTION_NAME,
        embedding_function=cfg.get_embedding_function(),
    )
    where = {"area": area} if area else None

    # --- signal 1: meaning ------------------------------------------------
    raw = collection.query(
        query_texts=[expanded],
        n_results=min(CANDIDATES * CHUNK_OVERFETCH, max(collection.count(), 1)),
        where=where,
    )
    meaning_pairs = []
    if raw["ids"] and raw["ids"][0]:
        for rank, (doc, meta, dist) in enumerate(zip(
                raw["documents"][0], raw["metadatas"][0],
                raw["distances"][0]), start=1):
            meaning_pairs.append((meta["path"], rank, {
                "meta": meta,
                "snippet": doc.strip(),
                "closeness": round(max(0.0, 1.0 - dist) * 100, 1),
            }))
    meaning = _rank_files(meaning_pairs)

    # --- signal 2: exact words -------------------------------------------
    # Pull every chunk so BM25 sees the same corpus the semantic side does.
    everything = collection.get(include=["documents", "metadatas"])
    docs = everything["documents"] or []
    metas = everything["metadatas"] or []
    if area:
        keep = [i for i, m in enumerate(metas) if m.get("area") == area]
        docs = [docs[i] for i in keep]
        metas = [metas[i] for i in keep]

    words = {}
    bm25 = None
    if docs:
        bm25 = BM25([tokenise(d) for d in docs])
        scored = bm25.scores(terms)
        # Discount matches found in weak evidence (input forms, code bodies).
        scored = [s * KIND_WEIGHT.get(metas[i].get("kind"), 1.0)
                  for i, s in enumerate(scored)]
        # Every positively-scored chunk, not the first CANDIDATES of them: the
        # cut to CANDIDATES now happens after de-duplication, in _rank_files, so
        # it cuts to distinct FILES. Sorting a few thousand floats is free.
        ranked = sorted((i for i, s in enumerate(scored) if s > 0),
                        key=lambda i: -scored[i])
        word_pairs = [
            (metas[i]["path"], rank, {
                "meta": metas[i],
                "snippet": docs[i].strip(),
                "bm25": round(scored[i], 2),
            })
            for rank, i in enumerate(ranked, start=1)
        ]
        words = _rank_files(word_pairs)

    # --- signal 3: fragment-index.mjs ------------------------------------
    # Which of the question's words are distinctive enough to be worth a
    # fragment-only boost? Measured against the corpus, not guessed.
    distinctive = []
    if bm25 is not None:
        limit = bm25.n * DISTINCTIVE_MAX_SHARE
        candidates = []
        for word in query_words_raw(expanded):
            df = bm25.doc_freq.get(_stem(word), 0)
            if 0 < df <= limit:
                candidates.append((bm25.idf(_stem(word)), word))
        if candidates:
            rarest = max(score for score, _ in candidates)
            distinctive = [w for score, w in candidates
                           if score >= rarest * DISTINCTIVE_IDF_RATIO]

    tool_hits, status_map = {}, {}
    if use_fragment_tool:
        fragments, problem = load_fragment_index()
        if problem:
            notes.append(f"fragment-index.mjs unavailable ({problem}) — "
                         f"ranking used meaning + words only")
        else:
            status_map = fragment_status_map(fragments)
            if distinctive:
                tool_hits = fragment_tool_hits(fragments, distinctive)
                notes.append("fragment-index checked for the distinctive "
                             f"word(s): {', '.join(distinctive)}")
            else:
                notes.append("no word in this question was distinctive enough "
                             "to boost fragments — ranked on meaning + words")

    # --- merge by rank ----------------------------------------------------
    combined = {}
    for path, (rank, payload) in meaning.items():
        entry = combined.setdefault(path, {"payload": payload, "score": 0.0,
                                           "meaning_rank": None,
                                           "word_rank": None, "tool": None})
        entry["score"] += WEIGHT_MEANING / (RRF_K + rank)
        entry["meaning_rank"] = rank
        entry["closeness"] = payload.get("closeness")

    for path, (rank, payload) in words.items():
        entry = combined.setdefault(path, {"payload": payload, "score": 0.0,
                                           "meaning_rank": None,
                                           "word_rank": None, "tool": None})
        entry["score"] += WEIGHT_WORDS / (RRF_K + rank)
        entry["word_rank"] = rank
        entry["bm25"] = payload.get("bm25")
        # Show the passage from whichever signal found this file more
        # convincingly. Always preferring the keyword chunk swapped a precise
        # semantic hit ("Hazard class is an INPUT - never assume it") for a
        # weaker section of the same correct file. Ties go to meaning, which is
        # the better judge of what a question was actually about.
        meaning_rank = entry.get("meaning_rank")
        if meaning_rank is None or rank < meaning_rank:
            entry["payload"] = payload

    for path, matched in tool_hits.items():
        if area and not path.startswith("scripts/"):
            continue
        if path in combined:
            combined[path]["score"] += WEIGHT_PROVEN_TOOL / (RRF_K + 1)
            combined[path]["tool"] = sorted(matched)

    # Discount the change-log style files, and apply the per-area prior, before
    # the final ordering.
    for path, entry in combined.items():
        weight = PATH_WEIGHT.get(path)
        if weight is not None:
            entry["score"] *= weight
        area_weight = AREA_WEIGHT.get(entry["payload"]["meta"].get("area"))
        if area_weight is not None:
            entry["score"] *= area_weight

    order = sorted(combined.items(), key=lambda kv: -kv[1]["score"])
    if use_rerank:
        order = _apply_rerank(expanded, order)

    results = []
    for path, entry in order:
        meta = entry["payload"]["meta"]
        results.append({
            "path": path,
            "abs_path": meta.get("abs_path", ""),
            "area": meta.get("area", ""),
            "kind": meta.get("kind", ""),
            "heading": meta.get("heading", ""),
            "score": round(entry["score"], 5),
            "meaning_rank": entry["meaning_rank"],
            "word_rank": entry["word_rank"],
            "closeness": entry.get("closeness"),
            "bm25": entry.get("bm25"),
            "tool_terms": entry["tool"],
            "rerank": entry.get("rerank"),
            "status": status_map.get(path),
            "snippet": entry["payload"]["snippet"],
        })
        if len(results) >= top_k:
            break

    return results, notes


# --------------------------------------------------------------------------
# CLI
# --------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(
        description="Search the AJ AI Brain by meaning AND exact words.")
    parser.add_argument("query", nargs="+", help="your question")
    parser.add_argument("--top", type=int, default=5,
                        help="how many results to show (default 5)")
    parser.add_argument("--area", choices=AREA_CHOICES,
                        help="limit to one part of the Brain")
    parser.add_argument("--no-fragment-tool", action="store_true",
                        help="skip tools/fragment-index.mjs (no Node needed)")
    parser.add_argument("--explain", action="store_true",
                        help="show why each result ranked where it did")
    parser.add_argument("--rerank", action="store_true",
                        help="re-score the shortlist with the cross-encoder (off by "
                             "default: measured neutral on the current 5-question test "
                             "set and ~1.5s slower — see rerank.py)")
    args = parser.parse_args()

    query = " ".join(args.query)

    try:
        results, notes = hybrid_search(
            query, top_k=args.top, area=args.area,
            use_fragment_tool=not args.no_fragment_tool,
            use_rerank=args.rerank)
    except Exception as exc:
        print(f"ERROR: {exc}")
        print("If this says the collection does not exist, run "
              "index-brain.cmd first.")
        return 1

    # Loudly, before the results - a stale index answers confidently from an
    # older copy of the Brain, and nothing about the answer looks wrong.
    banner = cfg.staleness_banner()
    if banner:
        print()
        print(banner)

    print()
    print(f'QUESTION: "{query}"   [hybrid: meaning + words]')
    if args.area:
        print(f"LIMITED TO: {args.area}")
    if args.explain:
        print(f"SEARCHED FOR: {', '.join(query_terms(query))}")
    for note in notes:
        print(f"NOTE: {note}")
    print("=" * 78)

    if not results:
        print("No matches found.")
        return 0

    for i, r in enumerate(results, 1):
        label = AREA_LABEL.get(r["area"], r["area"].upper())
        status = f"  [{r['status']}]" if r["status"] else ""
        print()
        print(f"{i}. [{label}]  {r['path']}{status}")

        found_by = []
        if r["meaning_rank"]:
            found_by.append(f"meaning #{r['meaning_rank']}")
        if r["word_rank"]:
            found_by.append(f"words #{r['word_rank']}")
        if r["tool_terms"]:
            found_by.append(f"fragment-index: {', '.join(r['tool_terms'])}")
        print(f"   found by: {' + '.join(found_by) if found_by else 'n/a'}")

        # Warn ONLY when the two searches disagree - when a file surfaced on one
        # signal alone. CLAUDE.md, START-HERE.md and the prompt hook all carry
        # the instruction "high in BOTH meaning and words is the strong signal;
        # only one firing means check before trusting it", so the tool should say
        # it rather than make a person work it out from two rank numbers.
        #
        # MEASURED 2026-08-20, and the result is why this prints nothing in the
        # normal case: over 60 real questions from job-log/questions.jsonl,
        # **300 of 300 top-5 results appeared in BOTH lists.** One-sided results
        # do not currently happen - over-fetching CHUNK_OVERFETCH x and cutting to
        # CANDIDATES distinct files means both retrievers return much the same
        # file set and differ only in order. So the guidance describes a case the
        # current configuration never produces.
        #
        # It is kept, silent, rather than deleted: it costs one comparison, and
        # it becomes true the moment the candidate pool shrinks or a retriever is
        # swapped - both of which are live questions in
        # rag-architecture-decisions.md. A label that fires on every result is
        # noise; one that fires on the exception is a check.
        if bool(r["meaning_rank"]) != bool(r["word_rank"]):
            only = "meaning" if r["meaning_rank"] else "word match"
            print(f"   CAUTION: found by {only} alone — the other search "
                  f"missed it, so check before trusting")

        if args.explain:
            bits = [f"score {r['score']}"]
            if r["closeness"] is not None:
                bits.append(f"closeness {r['closeness']}%")
            if r["bm25"] is not None:
                bits.append(f"bm25 {r['bm25']}")
            if r["heading"]:
                bits.append(f"§ {r['heading']}")
            print(f"   {'   '.join(bits)}")

        snippet = " ".join(r["snippet"].split())
        if len(snippet) > 300:
            snippet = snippet[:300] + " ..."
        print(f"   {snippet}")

    print()
    print("=" * 78)
    print("'meaning' = ranked by what you meant. 'words' = ranked by the exact "
          "words you typed.")
    print("Appearing in both is the strongest signal. Semantic-only baseline: "
          "ask-brain.cmd")
    return 0


if __name__ == "__main__":
    sys.exit(main())
