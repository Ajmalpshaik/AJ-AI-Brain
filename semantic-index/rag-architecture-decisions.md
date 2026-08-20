# The retrieval layer — why it is shaped this way, and what a rewrite would cost

**Asked 2026-08-20:** *"If I say to re-architect our project as per the best RAG and easy to do all kind
of RAG working — what changes would you do, is that needed, as of now is it okay or not, will it be
useful or not?"*

**Answer: do not rewrite it. The architecture is not what is limiting the search — the test set is.**
This file exists so that conclusion comes with its evidence attached, because "let's rebuild the RAG
properly" is an idea that will occur again, and it is a downgrade unless the two conditions at the bottom
of this file are met first.

> **Why this file lives in `semantic-index/` and not in `knowledge/`, and what measuring it revealed —
> 2026-08-20.** It was written into `knowledge/` first, and the score card immediately reported a
> regression: **3/14 at #1 fell to 2/14, MRR 0.325 -> 0.267.** The single question that flipped was
> `what does duck mean` — `glossary.md` went from #1 to #6.
>
> Chasing it produced a more important result than the fix. Nothing was displaced by this file directly
> — it never appeared in a top 5. Stripping the verbatim test questions out of it changed nothing.
> Moving it out of the indexed folders changed nothing. **Reverting this session's `brain-log.md`
> entries as well changed nothing: the score still sat at 2/14 with a corpus just 2 chunks larger than
> the baseline** — one added table row in `knowledge/INDEX.md` and one edited line in `START-HERE.md`.
>
> **So the 3/14 -> 2/14 difference is not a measurement of anything. It is a single knife-edge test row
> moving under a 0.05% corpus change.** `README.md` predicted exactly this when `glossary.md`'s 0.93
> discount was chosen: *"these scores sit within thousandths of each other, so a value at the edge is
> one new file away from flipping."* It was one new file away, and the file arrived.
>
> Re-sweeping the discount on the current corpus (the documented method, `README.md`) shows the window
> has not just shifted — it has closed:
>
> | weight | #1 | top-3 | top-5 | MRR | `duck` -> glossary | `diffusers` -> skill |
> |---|---|---|---|---|---|---|
> | 0.85 | 2 | 3 | 5 | 0.238 | #15 | #1 |
> | 0.90 | 2 | 4 | 6 | 0.253 | #9 | #1 |
> | **0.93 (current)** | 2 | 4 | 5 | 0.267 | #6 | #1 |
> | 0.96 | 3 | 4 | 6 | 0.304 | #5 | #1 |
> | 0.98 | 2 | 4 | 6 | 0.269 | #5 | #2 |
> | 1.00 | 2 | 4 | 6 | 0.269 | #5 | #2 |
>
> **No weight returns the glossary to #1 on that question any more** — the sprinkler files now beat it
> outright. 0.96 scores best overall, but by a different route (the guard row still fails, something
> else passes), which is fitting the sample. **The weight was therefore left at 0.93** — changing it
> would be the blind tuning the score card exists to prevent.
>
> Three things follow, and they matter more than where this file sits:
>
> 1. **Write documents about the search into `semantic-index/`, not `knowledge/`.** That folder is
>    outside `INDEX_TARGETS`, exactly like `docs/`. This is the third time a file describing the Brain's
>    own machinery has cost the Brain retrieval accuracy (`brain-log.md`, `glossary.md`, this one) — and
>    the first caught before it shipped, entirely because the score card ran.
> 2. **The `what does duck mean` guard row is currently broken at every weight.** It is flagged as such
>    in `test-questions.md` so nobody re-chases it as a fresh bug.
> 3. **The score card will cry wolf until the test set grows.** One knife-edge row is 7% of a 14-row
>    score, so an ordinary session's `brain-log.md` entry can trip the REGRESSION alarm. That is not a
>    reason to distrust the score card — it is the sharpest argument yet for the 30 rows below.

## How to judge a proposed upgrade — Ajmal's rule, 2026-08-20

He set the acceptance test himself, and it governs this whole file. When he names an idea, check it
against what exists and answer with **one of exactly four verdicts, saying which one it is**:

| Verdict | When | What to do |
|---|---|---|
| **Build it** | We don't have it, and it's useful | Make it |
| **Upgrade to his** | We have it, but his version is better | Replace ours with his |
| **Keep ours** | We have it, and ours is already better | Change nothing — and say *why* ours wins |
| **Skip** | Not needed | Leave it |

His words: *"what ever am saying chek that is we have olaready — if no this is useful keep it make it.
And if the item we have but my idea is best better that we have [take mine]. Or if the best one we
have, keep, do not do. If useful items and good items keep. If no need, live it."*

The two failure modes this rule exists to stop: **silently building a duplicate** — which wastes the
work *and* adds a competitor to every search — and **silently skipping his idea**, which loses a real
improvement. State the verdict per item, never one verdict for a whole list, so he can overrule it.

**"Keep ours" is the verdict to distrust — check for the merge before settling on it.** His correction,
2026-08-21: *"see anything I given that we have already and this given — if we add or combine with this,
if this will be good, need to do also."* Having the feature is not the same as having what his framing
adds to it. Both times that question was asked properly it paid: de-duplication was already prevented at
write time, but nothing was checking whether prevention still *worked*, so his framing produced a
permanent check; and hybrid fusion had existed for weeks without anyone measuring whether combining the
two retrievers actually changed the ranking — his framing produced the table further down this file.
A "keep ours" reached without asking that question is a guess wearing a verdict's clothes.

The two tables below are what makes this check fast: **What is already here** answers "do we have it",
and **What has already been tried and reverted** answers the harder case — we tried it, it measured
worse, and here is the line it was reverted from.

## What is already here

Measured on the corpus as it stands (2026-08-20, build fingerprint `5e2411794df50cea`):
**352 files, 3,892 chunks.** File count is live from `semantic-index/index-manifest.json`; the chunk
count is the last `score-brain` line carrying that same fingerprint, in `score-history.md`.

| Piece | Where |
|---|---|
| Dense retrieval, offline, no API key. **Live model is `all-MiniLM-L6-v2`** — `bge-small-en-v1.5` is written and one setting away, but unscored and not downloaded; see `README.md` | `brain_common.py` → `EMBED_MODEL`; `semantic-index/embed_bge.py` |
| BM25 exact-word retrieval — textbook, k1=1.5 / b=0.75, document-length normalised, with plural stemming and stopwords | `semantic-index/brain_search_hybrid.py` → `class BM25` |
| The two fused with Reciprocal Rank Fusion (K=60, equal weights), fusing **file** ranks not chunk ranks | same file, `_rank_files` |
| Structure-aware chunking — a `.cs` fragment splits into PURPOSE card / INPUTS / code, a `.md` file by heading | `semantic-index/brain_index.py` |
| Chunk-kind weighting — a PURPOSE card counts 1.0, an INPUTS form 0.45, a code body 0.35 | `brain_search_hybrid.py` → `KIND_WEIGHT` |
| Per-file discounts for question-shaped reference files (`brain-log.md` 0.85, `glossary.md` 0.93) | `brain_search_hybrid.py` → `PATH_WEIGHT` |
| Query expansion from a site-word table, read live — a new row works with no rebuild | `knowledge/site-vocabulary.md` |
| Over-fetch ×3 then de-duplicate to distinct files, then re-rank over files not chunks | `brain_search_hybrid.py` → `_rank_files` |
| Cross-encoder re-ranker, built and switched off | `semantic-index/rerank.py` |
| Incremental rebuild from content fingerprints, 2–4 s, with ghost-chunk prevention | `brain_index.py` |
| Stale-index detection by file contents, not timestamps | `brain_common.py` → `check_staleness` |
| Ingest normalisation — UTF-8 with or without BOM, CRLF flattened before hashing, undecodable bytes reported not hidden | `brain_common.py` → `read_text`, `_normalised_bytes` |
| Eight metadata fields per chunk (`path`, `abs_path`, `area`, `category`, `filename`, `chunk_index`, `kind`, `heading`), plus each fragment's live verification status joined at query time and a 0.25 boost for proven ones | `brain_index.py` → `chunks_for_file`; `brain_search_hybrid.py` → `fragment_status_map`, `WEIGHT_PROVEN_TOOL` |
| Build provenance — `built_at` and `git_commit` stamped into the manifest on every successful build (2026-08-20) | `brain_common.py` → `write_manifest`, `manifest_build_info` |
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
| Split the two oversized knowledge files to stop them crowding out smaller ones | **Theory tested and refuted.** Damping the oversized file that wrongly ranked #1 never surfaced the correct smaller file — a third file just took the top spot. Small correct files are not being *beaten*; they are not scoring on their own merits. Splitting stays a readability rule, not an accuracy fix | the 2026-08-13 spec, §7.4, now folded in here |
| Cross-encoder re-ranking | 3/5 → 3/5, slower. One question **needs** query expansion to work at all, another is **misled** by it — opposite directions, same five questions. **Re-measured 2026-08-21 and the reason changed**: it is not inert. Over 40 real questions it changes the #1 answer on **35 of 40 (88%)** and the top-3 set on **40 of 40**, for +902 ms. The two settings are different search engines that tied on five questions by coincidence — which makes leaving it off a much stronger call, not a weaker one | `rerank.py` header |

**The pattern is the point.** Six changes that any RAG guide would recommend; four measured neutral or
negative on this corpus. A rewrite throws all six results away and rediscovers them the expensive way.

## Measured and found unnecessary — an ingest de-duplication stage

Asked 2026-08-20. Counted over the whole corpus using the indexer's own chunking, 352 files / 3,882
chunks:

| What was looked for | Found |
|---|---|
| Exact duplicate chunks (whitespace- and case-normalised) | **5 groups, 10 chunks — 0.26%** |
| …of those, spanning two different files | 3, all of them `code` bodies (weight 0.35, the lowest) |
| Near-duplicate PURPOSE cards across files (5-word shingle, Jaccard ≥ 0.55) | **1 pair out of 507** — `action-set-category-halftone.cs` / `action-set-category-line-style.cs`, which are genuinely sibling tools |
| Near-duplicate knowledge sections across files | **0 out of 1,652** |

**There is no duplication problem to solve**, and the reason is worth keeping: duplication here is
prevented at *write* time, not cleaned up at *ingest* time. `skills/brain-self-maintain/SKILL.md` routes
every new note to exactly one home, `tools/verify-consistency.mjs` fails the build on drift, and
`fragment-index.mjs --find` exists so a fragment gets found instead of rewritten. An ingest de-duplicator
would be machinery guarding a door that is already locked — and it carries a real risk the numbers above
do not: the two sibling colour fragments *should* both exist, so anything that silently dropped one would
be a regression, not a cleanup.

Separately, the thing usually meant by "de-duplication" in a search — *the same file filling all five
result slots* — is already handled, and not at ingest: `_rank_files` over-fetches chunks ×3 and collapses
them to distinct files before ranking.

**What was added, 2026-08-20 — the check itself, not a filter.** The counts above were taken by hand,
once, and nothing would have taken them again; write-time prevention is exactly the kind of guarantee
that decays without announcing it. `brain_index.py` → `find_duplicates` now runs on **every build**,
full or incremental, costing **0.4 s** over 3,885 chunks. It compares `card` and `section` chunks only —
`code` bodies share helpers deliberately, `source` chunks are one line, and `inputs` forms are parameter
lists, so sibling tools match almost by definition (including them flagged copy-vs-move at 80% and
room-vs-space at 60%, both correct and both permanent, which is how a check turns into noise). Known-good
pairs live in `ACCEPTED_DUPLICATES`, so it prints nothing today and speaks only when something new
appears.

## Confidence, citation, evals, caching, observability — checked 2026-08-21

A seven-stage list was proposed. Most of it exists; the notes below are the two that did not, and the
four that were **declined with a measurement** so nobody re-proposes them on instinct.

### Built: a cache for the fragment index — the largest cost in a search

`load_fragment_index()` was spawning a **Node subprocess on every single query**, re-reading and
re-parsing all 290 `.cs` fragments to obtain data that changes only when one of those files changes.
It was larger than embedding the question and the vector lookup combined, by a wide margin:

| | |
|---|---|
| Uncached, every query | **426 ms** |
| Cached, fresh process | **38 ms** |
| Correctness | identical result lists on 25/25 real questions |
| Invalidation | verified — touching one `.cs` misses the cache, then re-caches |

Cached to disk, not just in memory, because almost every search is its own short-lived process. The key
is a **stat-only** signature of `scripts/` (name, size, mtime-ns), which is deliberately the *opposite*
choice from the index manifest's content hash, for a reason worth keeping: there a false miss cost a
92-second rebuild, so reading contents paid; here a false miss costs one subprocess, so a git checkout
harmlessly misses and re-runs. The dangerous direction — a false *hit* — would need a fragment to change
while preserving both its size and its modified time to the nanosecond.

### Two write-safety bugs the score card flagged, one of them serious

Adding the cache produced a score-card `REGRESSION` warning — **2/14, MRR 0.252, between two runs of
3/14, MRR 0.314 on the same corpus fingerprint.** The cache was cleared of blame first (its data is
byte-identical to a fresh run) and the score proved reproducible on re-run, so the warning was a false
alarm about the *cause* and a true alarm about something else entirely.

1. **Non-atomic writes (introduced here, fixed).** `write_corpus_vocabulary` wrote in place, so a search
   running while the Stop hook re-indexed could read a **truncated dictionary** — which makes ordinary
   words look unknown, changes which get spell-corrected, and changes the answer. That is the
   unreproducible run. All three derived files (`corpus-vocabulary.txt`, `index-manifest.json`,
   `fragment-index-cache.json`) now write to a temp file and rename, via `cfg.atomic_write_text`.

2. **The manifest was written before the build was verified (pre-existing, fixed, and the serious one).**
   `write_manifest` ran *before* `collection.count()`. A rebuild crashed on that count call having
   already written the manifest, and the next run printed **"UP TO DATE — nothing has changed"** over an
   index holding **1,200 chunks of 3,895.** It answers every question, from a third of the Brain, and
   nothing looks wrong. This is precisely the failure the build-fingerprint machinery exists to prevent,
   arriving through the back door — the manifest *is* the claim that the index is current, so it must be
   the last thing written, never an early one. Both build paths now count first, refuse to write the
   manifest if the count is wrong, and exit non-zero saying so. **Rule: write the "this is finished"
   record last, after the thing it vouches for has been checked.**

### Declined, each with the number that decided it

| Proposed | Why not |
|---|---|
| **High-frequency query cache** | Measured over 172 logged questions: **171 distinct, 1 repeat.** A cache would hit **0.6%** of the time. There is no repeat traffic to cache. |
| **Freshness scoring** | Nothing here decays. A gotcha found in July is exactly as true in August, so weighting by age would demote proven technique in favour of recent writing — and the most recently written files are changelogs, which are already *discounted* for crowding out answers. No evidence of a freshness problem exists; if one appears it will appear as a wrong answer, and that is when to weight it. |
| **Per-chunk timestamps for citations** | Git already holds when every line of every file was written, with who and why attached. A timestamp copied into chunk metadata would be a second, worse copy that goes stale between rebuilds. |
| **Adversarial eval rows** | The right idea at the wrong time. Rows whose expected answer is "nothing here covers this" test over-confidence, which is real — but the test set is **14 rows** and that is already limiter 1. Adding adversarial rows to a set this small makes it less able to measure ordinary retrieval, not more. Revisit at 30+ ordinary rows. |

### Already present, and where

| Proposed | Where it already lives |
|---|---|
| Trust scoring | Three layers: per-fragment verification status joined live at query time with a `WEIGHT_PROVEN_TOOL` boost; `KIND_WEIGHT` (a PURPOSE card 1.0, an INPUTS form 0.45, a code body 0.35); `PATH_WEIGHT` discounts for changelog-shaped files |
| Retrieval consistency | The `CAUTION` line — measured 0 of 300 one-sided in a top-5 answer, 27.6% at top-50 |
| Constrained generation, no outside knowledge | Enforced at the agent layer by `tools/auto-search-hook.mjs`, which appends *"if none of these actually answer it, SAY SO — do not answer from general Revit knowledge while appearing to quote this Brain"* to every prompt, plus `START-HERE.md` rule 1 and the "quote the file, never a remembered figure" rule in `CLAUDE.md` |
| Citation-backed answers | Every result carries its repo-relative path, heading and chunk index; replies link files |
| Auditable outputs | `job-log/questions.jsonl` — every question with the files returned and both ranks |
| Recall benchmark, continuously | `score_brain.py` + `score-history.md`, run automatically by `tools/score-check.mjs`, a Stop hook that re-scores whenever a file that can move a ranking changes and shouts if the score fell |
| Retrieval tracing | `--explain` prints fused score, closeness, BM25 and the matched heading per result |
| Usage dashboard | `tools/job-report.mjs`, including `--unused` for fragments never once used on a real job |

### The hallucination fallback already has an answer, and it is not a threshold

*"Below a confidence score, say insufficient evidence"* is in the tried-and-reverted table above, with the
measurement: top-1 closeness for a **correct** hit ran 35.5–65.1, for a **wrong** hit 27.8–56.6. **The
distributions overlap almost completely** — no cut-off rejects wrong answers without also rejecting right
ones, so the feature cannot work in code here.

What his framing adds is *where* it belongs. The check is not impossible, it is impossible **for a
number**: deciding whether five returned files actually answer the question needs reading them, which is
exactly what the agent does and what the auto-search hook already instructs on every single prompt. So
the fallback exists and works — at the only layer that can see the evidence. Do not re-attempt it as a
score threshold.

## Query understanding — the weak spot was misdiagnosed, and it is now half fixed

**Limiter 2 in the list above said the measured failure class was "site vocabulary — a site word that
appears in no file at all". Measured properly on 2026-08-21, that is not what is happening.**

Over the 170 real questions in `job-log/questions.jsonl`, **111 of them (65%) contain a word that appears
in no file in this Brain.** But the top of that list is not jargon:

| The word typed | What it is | Times |
|---|---|---|
| `posible`, `everyting`, `anyting`, `someting`, `difrent` | ordinary English, typed fast | 9, 6, 5, 3, 7 |
| **the schedule word**, **the equipment word**, **the description word**, **the accessories word** | **ordinary Revit words, misspelled** | 5, 5, 4, 4 |

The costly half is the second row: these are words the Brain contains hundreds of times, and a
misspelling is worth **nothing at all** to BM25, because a word in no document contributes no score.
The exact-word half of the hybrid simply loses the most important term in the question. Checked on three
real questions, correcting the spelling changed the #1 answer every time — and in one case put
`fill-mm-document-register.cs` at #1, the right fragment for that job, where the typed version had not
returned it at all.

**So a hand-maintained phrase table was never going to reach this.** A row per misspelling does not
scale; there is no end to the ways a word can be typed wrong.

### What was built

`brain_common.py` → `correct_spelling`, applied after `expand_query` and, like it, to the **words side
only**. `brain_index.py` writes every word the Brain contains to `semantic-index/corpus-vocabulary.txt`
on each build (10,307 words; gitignored as derived state, like the database). Three rules make it safe
enough to leave on:

1. **Only words absent from the corpus are touched.** A word that exists is never "corrected".
2. **A unique best match, or nothing.** The typed form of *colour* sits one letter from three corpus
   words and *many* from five — both are left alone. This is the rule that separates fixing a real Revit
   word from guessing at chatter.
3. **The correction is added, never substituted** — the opposite of `expand_query`, for the opposite
   reason. A site word is replaced because it actively misleads; a typo is in no file, so it cannot
   mislead the word search, and keeping it protects the meaning side, which handles misspellings well
   through sub-word tokens. A wrong correction can therefore only add a weak extra term, never remove a
   good one.

Measured: fires on **75 of 170 real questions (44%)**, costs **4.4 ms**, and among the corrections it
makes are his own project words — the Ashghal spelling and *standard*, both typed several ways.

### What it scored, and why that is not a disappointment

**Zero change on the test set** — spelling on versus off, over every row in `test-questions.md`:
identical #1, identical top-5, identical retrievable, and **not one row changed rank.** The score line
moved 11 → 10 retrievable in the same session, and the A/B proves that was the corpus edits, not this.

That is worth stating plainly rather than hiding: the feature is **demonstrably neutral on the 14 rows
and demonstrably useful on real questions**, because the test rows' own typos happen to be exactly the
ambiguous kind rule 2 declines to fix. It is the clearest example yet of limiter 1 — the test set cannot
see a real improvement, and that is a fact about the test set.

### A trap found by falling into it

**Never write a misspelling into any file in `skills/`, `knowledge/` or `scripts/` — not even as an
example.** Those folders *are* the dictionary the corrector checks against. An example typo written into
`site-vocabulary.md` while documenting this feature promoted that typo to a real word, and switched off
its own correction within one rebuild. Same shape as the older failure where documenting the diffuser
problem made `brain-log.md` the top answer for the diffuser question. Describe the mistake; never spell
it out.

## The ANN index — checked properly 2026-08-21, and it needs nothing

Worth writing down because the hypothesis was specific, plausible, and **wrong**, so it will occur to
someone again. Chroma's HNSW runs on defaults with only `hnsw:space` set: `ef_search` 100,
`ef_construction` 100, `max_neighbors` 16. The search asks for `CANDIDATES 80 × CHUNK_OVERFETCH 3 = 240`
chunks — and asking an HNSW index for more results than its search list holds is a classic way to lose
recall silently. Measured against exhaustive numpy search over all 3,888 vectors, 40 real questions:

| Depth | ANN recall vs exact |
|---|---|
| 10 | 99.5% |
| 80 — the number of distinct files that survive | **98.7%** |
| 240 — what Chroma is actually asked for | 96.4% |

Then three follow-ups, because 96.4% still looks like something to fix:

1. **Raising `ef_search` does nothing.** Swept 100 → 240 → 400 → 800 (via `collection.modify`, verified
   to actually apply). Recall identical at every value, to the decimal. The limit is the graph, not the
   search breadth — and graph quality is a build-time parameter, not a knob.
2. **The misses are ties.** Mean similarity of what ANN returned versus what exact search returned:
   **0.2611 against 0.2612 at depth 80** — a gap of 0.00002, with the mean similarity itself around
   0.26. The chunks it "missed" are indistinguishable from the ones it kept. The 96.4% is an
   identity-matching artefact at the boundary, not lost quality.
3. **ANN is not even buying speed here.** Exhaustive numpy search over the whole corpus takes **0.2 ms**
   against HNSW's **2.1 ms** — brute force is *ten times faster*, because 3,888 × 384 floats is 6 MB and
   a single matrix-vector product beats graph traversal at this size. Both are irrelevant next to the
   **23 ms** spent embedding the question.

**So: change nothing.** Not because ANN is winning, but because at this corpus size the entire question
is beneath the noise floor. It becomes real if the corpus grows by orders of magnitude — and the number
to re-check first is the 0.2 ms brute-force figure, since the simplest correct answer at this scale is
"no index at all". Do not tune HNSW on the strength of a recall percentage alone; check the similarity
gap first, or you will be optimising ties.

## What "combining both approaches" is actually doing — measured 2026-08-20

The fusion had never been measured as a *combination*, only as a total score. Over 60 real questions from
`job-log/questions.jsonl`, counting how often a returned file was found by one retriever alone:

| Depth | Results | Found by one signal only |
|---|---|---|
| top 5 — what a normal answer shows | 300 | **0 (0%)** |
| top 20 | 1,200 | 16 (1.3%) |
| top 50 | 3,000 | 829 (27.6%) |

**One-sided hits exist in quantity; RRF is sinking them.** A file found by only one retriever collects
roughly half the fused score, so it settles below the files both agree on — which is precisely what the
fusion is for, now with a number attached rather than an assumption.

Two consequences. First, `brain_search_hybrid.py` prints a `CAUTION` line when a result was found by one
signal alone — silent in normal use by construction, useful the moment you ask for more results than
usual. Second, the long-standing guidance in `CLAUDE.md` — *"only one firing means check before trusting
it"* — was describing a case that **cannot occur in a top-5 answer**; it has been corrected to say so.

## What is actually limiting the search

Not the pipeline. Three things, in order of how much they cost:

1. **The test set is 14 questions.** `semantic-index/score-history.md`, last reproducible run:
   **3/14 at #1, 5/14 in top 3, 6/14 in top 5, 11/14 retrievable, MRR 0.321.** Two finished features —
   the cross-encoder and the skill weighting — are switched off *solely* because 14 rows cannot tell a
   real gain from a coincidence. **This is the whole bottleneck**, and only Ajmal can clear it: the file
   itself explains why assistant-written questions do not count.

   Three more decisions are parked behind the same wall, and it is worth naming them so they are not
   mistaken for settled: **the fusion balance has never been swept** (`WEIGHT_MEANING` and
   `WEIGHT_WORDS` are both 1.0 — a starting point, not a result), **`RRF_K` is 60 because that is the
   value the original method proposed**, not because 60 beat anything here, and **`bge-small-en-v1.5`
   versus `all-MiniLM-L6-v2` has never been run at all.** Each is a real unknown. None of them can be
   answered on 14 rows without repeating the `AREA_WEIGHT` mistake — a sweep that "wins" by moving
   points from one half of a small test set to the other is fitting the sample, and it was already
   caught doing exactly that once.
2. **Words the files do not contain.** Two different problems were filed under one heading here, and
   separating them on 2026-08-21 was worth more than any tuning. **Misspellings** are the larger half —
   65% of real questions carry a word in no file, mostly ordinary Revit words typed fast — and they are
   now corrected automatically; see the query-understanding section below. **Genuine site vocabulary** is
   the smaller, harder half: a word that is not a misspelling of anything, where the right answer is a
   *different* word. No re-ranking or architecture reaches those. `knowledge/site-vocabulary.md` is the
   fix, it is read live, and it has 48 rows. Worked examples are in `semantic-index/README.md`, quoted
   there rather than here for the reason in the warning above.
3. **Nothing is learning from real questions yet — but the raw material has now arrived.**
   `tools/auto-search-hook.mjs` logs every question and its hits to `job-log/questions.jsonl`. As of
   **2026-08-20 that file holds 165 unique questions in Ajmal's own words**, logged over 5 days, each
   stamped with the files the search actually returned. Nothing reads it yet. **This is now the
   cheapest path to limiter #1** — the 30-row test set no longer needs him to sit down and invent
   questions, because five days of real ones are already on disk. What it still needs from him is the
   *expected file* per row, which is the one half no log can supply.

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

## Still open, carried over from the retired 2026-08-13 spec

`docs/superpowers/` held two implementation plans and one design spec written on 2026-08-13. **The
plans are done** — the score card, the auto-reindex hook pair, `search_brain`, the compact context
block, the auto-search hook and the three agents all exist and are wired in. Nobody ever ticked their
60 checkboxes, so they read as 60 open jobs; they were deleted rather than left to mislead, and git
history has them. These are the only items that were still live:

| Item | State on 2026-08-20 |
|---|---|
| **Let the vocabulary file write itself** — when a search misses and a reworded one succeeds, log that pair as a candidate row | **Not built.** Still the highest-leverage idea in the spec: this is the one part of the system that improves every time it disappoints. Keep the two rules already learned — map the *phrase* not the word, and narrow rows only. |
| **A job log** — one line per real task, to learn which fragments actually do the work | **Collecting.** `tools/job-log-revit.mjs` is wired as a hook and `brain_context.py --log` writes `job-log/questions.jsonl`, which on 2026-08-20 holds **166 lines / 165 unique questions across 5 days**. `job-log/revit-runs.jsonl` is filling too. The payoff — *an unused fragment is not free, it competes in every search* — still needs a month of data, but the harvest for the test set does not: see limiter 3 above. |
| **A file per project** — stable facts only (standards, naming, units), never model state | **Not built.** Lowest priority, and `START-HERE.md` rule 2 rightly forbids the tempting version of it. |
| **Close out the unproven fragments** | Tracked live by `tools/brain-status.mjs` and `knowledge/brain-log.md`, which is the right home. Needs Revit, not a plan. |
| **`PreToolUse` narrates on matcher `*`** — every tool call speaks aloud | Untested with background agents running. If agents fire tool calls while Ajmal is modelling, he gets narration about work he did not ask for, mid-duct. Worth checking before turning agents loose. |

The spec also left three questions for Ajmal that were never answered, and they still shape the order
of everything above: **which goal comes first** — the assistant never missing what he has already
written, or the Brain answering from documents he never wrote (QCS, Ashghal, NFPA)? **Should the
fragment library grow or be pruned?** And **who writes the replacement test questions** — which
`test-questions.md` now answers: he does.
