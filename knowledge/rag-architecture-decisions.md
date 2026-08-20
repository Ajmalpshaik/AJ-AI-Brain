# The retrieval layer — why it is shaped this way, and what a rewrite would cost

**Asked 2026-08-20:** *"If I say to re-architect our project as per the best RAG and easy to do all kind
of RAG working — what changes would you do, is that needed, as of now is it okay or not, will it be
useful or not?"*

**Answer: do not rewrite it. The architecture is not what is limiting the search — the test set is.**
This file exists so that conclusion comes with its evidence attached, because "let's rebuild the RAG
properly" is an idea that will occur again, and it is a downgrade unless the two conditions at the bottom
of this file are met first.

## What is already here

Measured on the corpus as it stands: **348 files, 3,786 chunks, ~2.4 MB of text.**

| Piece | Where |
|---|---|
| Dense retrieval, `bge-small-en-v1.5`, ONNX, offline, no API key | `semantic-index/embed_bge.py` |
| BM25 exact-word retrieval with rarity (IDF) weighting | `semantic-index/brain_search_hybrid.py` |
| The two fused with Reciprocal Rank Fusion (K=60) | same file |
| Structure-aware chunking — a `.cs` fragment splits into PURPOSE card / INPUTS / code, a `.md` file by heading | `semantic-index/brain_index.py` |
| Chunk-kind weighting — a PURPOSE card counts 1.0, an INPUTS form 0.45, a code body 0.35 | `brain_search_hybrid.py` → `KIND_WEIGHT` |
| Per-file discounts for question-shaped reference files (`brain-log.md` 0.85, `glossary.md` 0.93) | `brain_search_hybrid.py` → `PATH_WEIGHT` |
| Query expansion from a site-word table, read live — a new row works with no rebuild | `knowledge/site-vocabulary.md` |
| Over-fetch ×3 then de-duplicate to distinct files, then re-rank over files not chunks | `brain_search_hybrid.py` → `_rank_files` |
| Cross-encoder re-ranker, built and switched off | `semantic-index/rerank.py` |
| Incremental rebuild from content fingerprints, 2–4 s, with ghost-chunk prevention | `brain_index.py` |
| Stale-index detection by file contents, not timestamps | `brain_common.py` → `check_staleness` |
| Retrieval injected into every prompt automatically | `tools/auto-search-hook.mjs` |
| A scoring harness and a stamped score history | `semantic-index/score_brain.py`, `score-history.md` |
| A second corpus kept deliberately separate | `api-index/` |

That is hybrid retrieval, contextual chunking, query rewriting, re-ranking, incremental indexing and an
eval harness. There is no missing stage.

## What has already been tried and reverted — do not re-propose these

Each one is a standard "best practice" recommendation. Each measured **worse here**, and the reasoning is
in the code at the line it was reverted from.

| Idea | Measured result | Recorded in |
|---|---|---|
| Contextual Retrieval — prefix every chunk with its filename + PURPOSE | recall@5 **fell 7/14 → 5/14**. Code chunks began competing with their own PURPOSE card on the words the card was built to own | `brain_index.py`, `chunks_for_file` |
| Same, on markdown continuation chunks | recall@5 **fell 7/14 → 5/14**; repeating the heading diluted each chunk and inflated those words' document frequency, so BM25 counted them for less | `brain_index.py` |
| A confidence floor — "say nothing matched instead of a wrong #1" | Top-1 closeness for a **correct** hit ran 35.5–65.1, for a **wrong** hit 27.8–56.6. The distributions overlap almost completely; no cut-off rejects wrong answers without rejecting right ones | `brain_search_hybrid.py`, above `STOPWORDS` |
| Bigger candidate pool (×1 → ×10) | **Neutral.** Identical #1, top-3, top-5 at every value; MRR within 0.003. The real gain came from re-ranking over files instead of chunks | `brain_search_hybrid.py`, `CHUNK_OVERFETCH` |
| Boost skills over fragments | MRR 0.323 → 0.424, but it **moved wins from one half of the test set to the other** and netted +1 on a set that splits exactly 7 skill / 7 non-skill. Fitting the sample, not the job | `brain_search_hybrid.py`, `AREA_WEIGHT` |
| Cross-encoder re-ranking | 3/5 → 3/5, ~1.5 s slower per query. One question **needs** query expansion to work at all, another is **misled** by it — opposite directions, same five questions | `rerank.py` header |

**The pattern is the point.** Six changes that any RAG guide would recommend; four measured neutral or
negative on this corpus. A rewrite throws all six results away and rediscovers them the expensive way.

## What is actually limiting the search

Not the pipeline. Three things, in order of how much they cost:

1. **The test set is 14 questions.** `semantic-index/score-history.md`, last reproducible run:
   **3/14 at #1, 5/14 in top 3, 6/14 in top 5, 11/14 retrievable, MRR 0.321.** Two finished features —
   the cross-encoder and the skill weighting — are switched off *solely* because 14 rows cannot tell a
   real gain from a coincidence. **This is the whole bottleneck**, and only Ajmal can clear it: the file
   itself explains why assistant-written questions do not count.
2. **Site vocabulary.** The measured failure class is words that appear in no file — "floor levels",
   "light fitting", "out to excel". No re-ranking reaches them; no architecture reaches them.
   `knowledge/site-vocabulary.md` is the fix, it is read live, and it has 48 rows.
3. **Nothing is learning from real questions yet.** `tools/auto-search-hook.mjs` already logs every
   question and its hits to `job-log/questions.jsonl` — that file does not exist yet, so the cheapest
   source of the next hundred test rows is currently producing nothing.

## The one re-architecture that IS worth doing

**`api-index/` is a second copy of the pipeline, not a second configuration of it.** It has its own
`api_common.py`, `api_index.py`, `api_search.py`, its own chunk sizes, and a search with no BM25, no
fusion and no re-ranking — because those were not copied across. A third corpus (standards, project
documents, manufacturer PDFs, a second Brain) means a third copy that drifts the same way.

The fix is a refactor, not a rewrite: **one corpus module that takes folders, collection name, chunk
rules and weights as configuration**, with `aj_brain` and `revit_api` as two configs of it. Retrieval
behaviour does not change — which is exactly what makes it safe, because `score-brain.cmd` producing the
identical line before and after is the proof it worked. That is what "easy to do all kinds of RAG
working" actually costs here: about a day, and no measurement risk.

**Second gap, and it is smaller than it looks:** the `.cmd` wrappers are Windows-only, but a
cross-platform path already exists — `mcp-server/brain-tools/search-brain.js` registers `search_brain`
as an MCP tool, and both it and `tools/auto-search-hook.mjs` already look in `venv/Scripts` *and*
`venv/bin`. **What is missing is only the venv itself off Windows:** `requirements.txt` gives Windows
setup commands only, so on Claude Code for web there is no `venv/` and no `chromadb`, and every entry
point reports "no Python found". A `setup.sh` beside `requirements.txt` closes it — an hour, not a
re-architecture.

## Revisit a rewrite only when both of these are true

1. `semantic-index/test-questions.md` holds **30+ rows in Ajmal's own words**, and the two switched-off
   features have been re-swept against them.
2. A specific question class is failing that **no configuration of the current pipeline can reach** —
   demonstrated on those rows, not argued from a diagram.

Until then, every hour spent on architecture buys less than an hour spent writing test questions.


## Folder structure — same verdict, for a related reason

Asked in the same conversation. The layout is **fine, and it is load-bearing**: the folder path becomes
the `category` field on every chunk (`brain_index.py`, `chunks_for_file`), and it is what the routing
tables in `START-HERE.md`, `knowledge/INDEX.md` and `scripts/README.md` point at. Moving folders is not
a cosmetic change here — **610 path references across the markdown files and 73 `// SOURCE:` lines
inside the fragments** would move with them.

Top level separates by **role**, and that is the right axis: `skills/` how to do a job, `knowledge/` what
is true and what bites, `scripts/` the C#, `tools/` maintenance, `semantic-index/` search,
`mcp-server/` the Revit bridge. Inside `scripts/`, the split is by **verb** — `actions/` change
something, `filters/` narrow a set, `creators/` make new elements, `recipes/` multi-step jobs,
`commands/` one-shot Revit commands, `context/` read the session, `lib/` the shared prelude. A request
maps onto that without being taught it.

Four small things are genuinely untidy, none urgent:

| What | Why it is worth a look |
|---|---|
| `scripts/actions/selection/` holds **1 file** | A folder with one file adds a routing decision and offers no choice. Fold into `actions/visibility/` or leave — but do not add more one-file folders. |
| `scripts/recipes/` is **36 files, flat, 588 KB** — the largest folder | `actions/` is subdivided at a third that size. The 8 `sprinkler-*.cs` are already a group in everything but the folder. |
| `scripts/actions/sheets-views/` is **35 files** | Sheets and views are two jobs sharing one folder — it is larger than most top-level folders. |
| `docs/superpowers/` — 2 plans + 1 spec from 2026-08-13 | The work they describe is **built**; the spec still says "Nothing in here is built yet". `docs/` is deliberately outside the index, so nothing will ever correct them. Delete or mark done. |

**Do not reshuffle for tidiness.** The consistency checker would catch every broken link, but that is 683
references churned for no measured gain, and the same rule applies as to the pipeline: change it when a
job it blocks turns up, not because a diagram would look better.