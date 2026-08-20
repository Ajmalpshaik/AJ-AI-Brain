"""BGE-small-en-v1.5 embeddings, run on onnxruntime — the model the search reads with.

WHY THIS REPLACED all-MiniLM-L6-v2 (2026-08-20)
------------------------------------------------
MiniLM-L6 is a 2021 model with a 256 word-piece window. Two costs, both measured
on the score card rather than assumed:

  * Retrieval quality. On the 14-question set the right file was ABSENT from the
    whole 80-chunk candidate pool 7 times out of 14. Re-ranking cannot repair that
    — rerank.py only reorders what was already retrieved — so the only lever left
    is the model that does the retrieving.

  * Chunk size. The 256-token window is what forced CHUNK_TARGET down to 900
    characters, which splits a fragment's PURPOSE card away from its input fields
    so the two halves compete with each other. BGE-small reads 512, so that dial
    is now unlocked (see the note on CHUNK_TARGET in brain_common.py).

Same size class, same speed class, still fully offline after one download, still
no API key and still NO NEW DEPENDENCY: onnxruntime, tokenizers and numpy all
arrive with chromadb already, exactly as rerank.py established.

ONE CHANGE AT A TIME
--------------------
Only the model changed here. Chunk size was deliberately left alone, and the BGE
query prefix ships OFF — both are separate experiments, and neither can be judged
on 14 questions. That is the same discipline that keeps the cross-encoder off.

CHANGING THE MODEL INVALIDATES THE WHOLE INDEX
----------------------------------------------
Vectors from two different models are not comparable. brain_common.build_fingerprint()
names the model, so switching it changes the fingerprint and brain_index.py rebuilds
everything from scratch by itself. There is deliberately NO silent fallback to the
old model: a half-MiniLM, half-BGE collection would still answer every question,
just quietly worse, which is the exact fault the fingerprint exists to prevent.

Reads only. Never touches Revit.
"""

import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
MODEL_DIR = HERE / "model-cache" / "bge-small-en-v1.5"
MODEL_FILE = MODEL_DIR / "model.onnx"
TOKENIZER_FILE = MODEL_DIR / "tokenizer.json"

MODEL_NAME = "bge-small-en-v1.5"

# The window the model actually reads. 512 word-pieces, against MiniLM-L6's 256.
MAX_LENGTH = 512

# How many texts go through onnxruntime at once. Bounded so a full rebuild of a
# few thousand chunks cannot balloon memory on a work laptop.
BATCH_SIZE = 32

# BGE was trained with an instruction on the QUESTION only, never on the stored
# text. It is OFF because it is unproven here, and it is worth knowing that
# turning it on costs NO rebuild — it only changes how a query is embedded, so it
# can be A/B'd with score-brain.cmd alone once test-questions.md has 30 rows.
QUERY_PREFIX = ""
QUERY_PREFIX_BGE = "Represent this sentence for searching relevant passages: "

_session = None
_tokenizer = None
_input_names = None


def available():
    """True if the model was downloaded. Nothing here can run without it."""
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


def embed(texts):
    """Embed a list of strings. Returns one unit-length vector per string.

    Unlike rerank.py's score(), this does NOT swallow exceptions. A re-ranker that
    fails can fall back to the original order and still be useful; an embedder that
    fails has nothing to fall back to, and returning a wrong-but-plausible vector
    would poison the index silently.
    """
    import numpy as np

    if not available():
        raise RuntimeError(
            "Embedding model not found at " + str(MODEL_DIR) + "\n"
            "Run:  venv\\Scripts\\python.exe embed_bge.py --download"
        )

    _load()
    out = []
    for start in range(0, len(texts), BATCH_SIZE):
        batch = [t if t else " " for t in texts[start:start + BATCH_SIZE]]
        encodings = _tokenizer.encode_batch(batch)

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

        result = _session.run(None, feeds)[0]
        result = np.asarray(result, dtype=np.float32)

        # BGE pools on the CLS token (position 0), NOT the mean of all tokens.
        # Mean-pooling a BGE model produces vectors that still look reasonable and
        # rank measurably worse, which is the worst kind of wrong.
        if result.ndim == 3:
            pooled = result[:, 0, :]
        else:
            pooled = result

        norms = np.linalg.norm(pooled, axis=1, keepdims=True)
        pooled = pooled / np.clip(norms, 1e-12, None)
        out.extend(pooled.astype(np.float32).tolist())

    return out


# --------------------------------------------------------------------------
# Chroma plumbing
# --------------------------------------------------------------------------

def _base_class():
    from chromadb.api.types import Documents, EmbeddingFunction
    return EmbeddingFunction[Documents]


class BGESmallEmbeddingFunction(_base_class()):
    """The embedding function handed to Chroma for both indexing and searching."""

    def __init__(self, query_prefix: str = QUERY_PREFIX):
        self._query_prefix = query_prefix

    def __call__(self, input):
        return embed(list(input))

    def embed_query(self, input):
        """Chroma calls this for query_texts, and __call__ for stored documents.

        That split is what makes the BGE instruction prefix possible at all: it has
        to reach the question without ever reaching the indexed text.
        """
        texts = list(input)
        if self._query_prefix:
            texts = [self._query_prefix + t for t in texts]
        return embed(texts)

    @staticmethod
    def name():
        return "aj_brain_bge_small"

    def default_space(self):
        # Vectors leave embed() unit-length, so cosine and l2 rank identically.
        # Cosine is named because it makes the reported "closeness" literally
        # 1 - distance, and because it is the space BGE was trained for.
        return "cosine"

    def supported_spaces(self):
        return ["cosine", "l2", "ip"]

    def get_config(self):
        return {"query_prefix": self._query_prefix}

    @staticmethod
    def build_from_config(config):
        return BGESmallEmbeddingFunction(
            query_prefix=config.get("query_prefix", QUERY_PREFIX)
        )


def download():
    """Fetch the ONNX model into model-cache/. ~127 MB, needs internet once.

    Git-ignored with the rest of semantic-index/, so a fresh copy of the Brain on
    another machine has no embedder until this is run — same deliberate trade as
    rerank.py: the Brain must stay small enough to copy as a folder.
    """
    import urllib.request

    base = "https://huggingface.co/Xenova/bge-small-en-v1.5/resolve/main/"
    MODEL_DIR.mkdir(parents=True, exist_ok=True)
    for remote, local in (("onnx/model.onnx", "model.onnx"),
                          ("tokenizer.json", "tokenizer.json")):
        out = MODEL_DIR / local
        if out.exists() and out.stat().st_size > 0:
            print("  have  " + local + " (" + str(out.stat().st_size // 1024) + " KB)")
            continue
        print("  fetching " + local + " ...")
        urllib.request.urlretrieve(base + remote, out)
        print("  got   " + local + " (" + str(out.stat().st_size // 1024) + " KB)")
    # ASCII only in console output: Windows consoles here are cp1252.
    print("\nDone. Now run:  index-brain.cmd --full")
    return 0


if __name__ == "__main__":
    if "--download" in sys.argv:
        sys.exit(download())

    # Smoke test: prove the model discriminates before trusting it on 2,989 chunks.
    if not available():
        raise SystemExit(
            "Model not found at " + str(MODEL_DIR) + "\n"
            "Run:  venv\\Scripts\\python.exe embed_bge.py --download"
        )

    _load()
    print("ONNX inputs:", sorted(_input_names))

    question = "how do I create new levels in Revit"
    passages = [
        "create-levels.cs PURPOSE: Batch-create Levels, either at even spacing from a "
        "start elevation, or at an explicit list of elevations.",
        "create-floor.cs PURPOSE: Create a floor slab from a boundary curve loop.",
        "nfpa13-sprinkler-spacing.md: maximum spacing for standard-spray upright sprinklers.",
    ]

    vectors = embed([question] + passages)
    print("dimensions:", len(vectors[0]))

    q = vectors[0]
    sims = [sum(a * b for a, b in zip(q, v)) for v in vectors[1:]]

    print('\nquestion: "' + question + '"\n')
    for s, p in sorted(zip(sims, passages), reverse=True):
        print("  " + format(s, ">8.4f") + "  " + p[:70])

    best = max(range(len(sims)), key=lambda i: sims[i])
    print("\nPASS: the levels fragment scored highest" if best == 0
          else "\nFAIL: expected the levels fragment to win")
