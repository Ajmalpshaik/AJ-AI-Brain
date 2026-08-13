"""Cross-encoder re-ranking: re-score the shortlist by reading question and chunk TOGETHER.

WHY THIS IS DIFFERENT FROM THE SEARCH ITSELF
--------------------------------------------
The main search is a bi-encoder: the question and every chunk are turned into vectors
separately and compared. That is what makes it fast enough to scan 2,989 chunks - but it
means the model never actually reads the question against the chunk. A cross-encoder does:
one pass over "question [SEP] chunk", which is far more accurate and far too slow to run
over everything. So it runs over the shortlist only.

OFF BY DEFAULT — MEASURED, NOT ASSUMED (2026-08-13)
---------------------------------------------------
It is built, it works, and it is disabled because on the test set that exists it did not
help. Turn it on with `ask-brain-hybrid.cmd --rerank` or `use_rerank=True`.

Checked BEFORE building: of five test questions, three already returned the right file at
#1, one had its answer at #7 (exactly what re-ranking exists to fix) and one had its answer
ABSENT from the whole candidate pool - which no re-ranker can repair, since re-ranking only
reorders what was already retrieved. Honest ceiling: 3/5 -> 4/5.

What actually happened:

    original question fed to the cross-encoder   2/5  (broke "out to excel")
    expanded question fed to the cross-encoder   3/5  (no change, ~1.5 s/query slower)

The reason is worth keeping, because it is not a bug in either part:

  * "take my door schedule OUT TO EXCEL" only works because site-vocabulary.md expands it to
    `export csv schedule`. The word "excel" appears in no file. Feed the cross-encoder the
    raw question and it correctly judges that no chunk matches - so expansion is REQUIRED.

  * "add 4 more FLOOR LEVELS" expands to `level elevation storey`, and **"elevation" means
    two things in Revit** - a level's height, and an elevation view. The bi-encoder shrugs
    that off; the cross-encoder genuinely reads it and is led to create-room-elevations.cs
    (scored -2.35) over create-levels.cs (-7.08), even though create-levels.cs was scored on
    its own PURPOSE card, which is the correct chunk. So expansion is HARMFUL here.

One layer needs expansion, the other is misled by it, in opposite directions on the same
five questions. That cannot be tuned on five questions - it needs the 20+ in
test-questions.md. Until then, shipping this on would be exactly the blind change the score
card was built to prevent.

NO NEW DEPENDENCY either way: onnxruntime, tokenizers and numpy all arrive with chromadb.

NO NEW DEPENDENCY. onnxruntime, tokenizers and numpy all arrive with chromadb already, so
this adds a model file and no pip install. sentence-transformers was rejected: it pulls in
PyTorch, roughly 2 GB, into an environment that is deliberately minimal, fully offline and
on an antivirus-managed machine.

Reads only. Never touches Revit.
"""

import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
MODEL_DIR = HERE / "model-cache" / "ms-marco-MiniLM-L-6-v2"
MODEL_FILE = MODEL_DIR / "model.onnx"
TOKENIZER_FILE = MODEL_DIR / "tokenizer.json"

# How many of the merged candidates get re-scored. The cross-encoder is ~100x slower per
# pair than the vector search, so this is the latency dial: 25 pairs is tens of
# milliseconds, the whole shortlist would be seconds.
RERANK_DEPTH = 25

# Chunks are ~900 chars; 512 tokens covers that with room for the question.
MAX_LENGTH = 512

_session = None
_tokenizer = None
_input_names = None


def available():
    """True if the model was downloaded. Everything degrades to no re-ranking without it."""
    return MODEL_FILE.exists() and TOKENIZER_FILE.exists()


def _load():
    global _session, _tokenizer, _input_names
    if _session is not None:
        return

    import onnxruntime
    from tokenizers import Tokenizer

    _session = onnxruntime.InferenceSession(
        str(MODEL_FILE), providers=["CPUExecutionProvider"]
    )
    _input_names = {i.name for i in _session.get_inputs()}

    _tokenizer = Tokenizer.from_file(str(TOKENIZER_FILE))
    _tokenizer.enable_truncation(max_length=MAX_LENGTH)
    _tokenizer.enable_padding()


def score(query, passages):
    """Return one relevance score per passage. Higher is more relevant.

    Never raises: a re-ranker that throws would take down a search that works perfectly well
    without it, which is a bad trade. On any failure the caller gets None and keeps the
    original order.
    """
    if not passages or not available():
        return None

    try:
        import numpy as np

        _load()
        encodings = _tokenizer.encode_batch([(query, p) for p in passages])

        feeds = {}
        if "input_ids" in _input_names:
            feeds["input_ids"] = np.array([e.ids for e in encodings], dtype=np.int64)
        if "attention_mask" in _input_names:
            feeds["attention_mask"] = np.array(
                [e.attention_mask for e in encodings], dtype=np.int64
            )
        if "token_type_ids" in _input_names:
            feeds["token_type_ids"] = np.array(
                [e.type_ids for e in encodings], dtype=np.int64
            )

        logits = _session.run(None, feeds)[0]
        return [float(row[0]) for row in logits]
    except Exception:
        return None


def download():
    """Fetch the ONNX cross-encoder into model-cache/. ~89 MB, needs internet once.

    The model is git-ignored along with the rest of semantic-index/, so a fresh copy of the
    Brain on another machine has no re-ranker until this is run. That is deliberate - 89 MB
    in the repo would fight the rule that this Brain must stay small enough to copy - but it
    does mean the download step has to live here rather than in someone's memory.
    """
    import urllib.request

    base = "https://huggingface.co/Xenova/ms-marco-MiniLM-L-6-v2/resolve/main/"
    MODEL_DIR.mkdir(parents=True, exist_ok=True)
    for remote, local in (("onnx/model.onnx", "model.onnx"), ("tokenizer.json", "tokenizer.json")):
        out = MODEL_DIR / local
        if out.exists() and out.stat().st_size > 0:
            print(f"  have  {local} ({out.stat().st_size // 1024} KB)")
            continue
        print(f"  fetching {local} ...")
        urllib.request.urlretrieve(base + remote, out)
        print(f"  got   {local} ({out.stat().st_size // 1024} KB)")
    # ASCII only in console output: Windows consoles here are cp1252, and an em dash prints
    # as a replacement character. The repo's consistency checker watches file encoding, but
    # nothing watches what a script prints.
    print("\nDone. Re-ranking stays OFF by default - see the header of this file for why.")
    return 0


if __name__ == "__main__":
    if "--download" in sys.argv:
        sys.exit(download())

    # Smoke test: a clearly relevant passage must outscore a clearly irrelevant one.
    if not available():
        raise SystemExit(
            f"Model not found at {MODEL_DIR}\n"
            f"Run:  venv\\Scripts\\python.exe rerank.py --download"
        )

    _load()
    print("ONNX inputs:", sorted(_input_names))

    q = "how do I create new levels in Revit"
    passages = [
        "create-levels.cs PURPOSE: Batch-create Levels, either at even spacing from a "
        "start elevation, or at an explicit list of elevations.",
        "create-floor.cs PURPOSE: Create a floor slab from a boundary curve loop.",
        "nfpa13-sprinkler-spacing.md: maximum spacing for standard-spray upright sprinklers.",
    ]
    scores = score(q, passages)
    if scores is None:
        raise SystemExit("scoring returned None - see the exception guard in score()")

    print(f'\nquery: "{q}"\n')
    for s, p in sorted(zip(scores, passages), reverse=True):
        print(f"  {s:>8.3f}  {p[:70]}")

    best = max(range(len(scores)), key=lambda i: scores[i])
    print("\nPASS: the levels fragment scored highest" if best == 0
          else "\nFAIL: expected the levels fragment to win")
