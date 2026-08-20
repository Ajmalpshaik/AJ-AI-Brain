# Score history

**This file is the single source of truth for how well the search works.** Quote it; do not quote a
remembered number. Every line from 2026-08-20 onward is stamped with the model, chunk settings, corpus
size and fingerprint that produced it.

Lines ABOVE that point carry no stamp and are **not comparable to each other or to anything since** — the
model, the chunk size and the corpus all changed underneath them without being recorded. That is how
three different accuracy figures (75%, 60%, 29%) ended up quoted around this Brain at the same time. They
are kept for history, not for comparison.

One line per `score-brain` run, oldest first. Written automatically.

- 1/4 at #1, 2/4 in top 3, 2/4 in top 5
- 1/4 at #1, 2/4 in top 3, 2/4 in top 5
- 1/4 at #1, 2/4 in top 3, 2/4 in top 5
- 1/4 at #1, 2/4 in top 3, 2/4 in top 5
- 1/4 at #1, 2/4 in top 3, 2/4 in top 5
- 3/5 at #1, 3/5 in top 3, 3/5 in top 5
- 3/5 at #1, 3/5 in top 3, 3/5 in top 5
- 3/5 at #1, 3/5 in top 3, 3/5 in top 5
- 3/5 at #1, 3/5 in top 3, 3/5 in top 5
- 3/5 at #1, 3/5 in top 3, 3/5 in top 5
- 3/5 at #1, 3/5 in top 3, 3/5 in top 5
- 2/5 at #1, 2/5 in top 3, 2/5 in top 5
- 3/5 at #1, 3/5 in top 3, 3/5 in top 5
- 2/5 at #1, 2/5 in top 3, 2/5 in top 5
- 3/5 at #1, 3/5 in top 3, 3/5 in top 5
- 3/5 at #1, 3/5 in top 3, 3/5 in top 5
- 3/5 at #1, 3/5 in top 3, 3/5 in top 5
- 3/6 at #1, 3/6 in top 3, 3/6 in top 5
- 3/6 at #1, 3/6 in top 3, 3/6 in top 5
- 3/6 at #1, 3/6 in top 3, 3/6 in top 5
- 3/6 at #1, 3/6 in top 3, 3/6 in top 5
- 4/7 at #1, 4/7 in top 3, 4/7 in top 5
- 4/7 at #1, 4/7 in top 3, 4/7 in top 5
- 4/14 at #1, 4/14 in top 3, 7/14 in top 5
- 4/14 at #1, 4/14 in top 3, 7/14 in top 5
- 4/14 at #1, 4/14 in top 3, 5/14 in top 5
- 4/14 at #1, 4/14 in top 3, 5/14 in top 5
- 4/14 at #1, 4/14 in top 3, 7/14 in top 5
- 3/14 at #1, 3/14 in top 3, 7/14 in top 5, 10/14 retrievable, MRR 0.299  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=3786 fp=ab19d3a62e3514a5
- 3/14 at #1, 5/14 in top 3, 6/14 in top 5, 11/14 retrievable, MRR 0.323  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=3786 fp=ab19d3a62e3514a5
- 3/14 at #1, 5/14 in top 3, 6/14 in top 5, 11/14 retrievable, MRR 0.321  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=3786 fp=ab19d3a62e3514a5
- 2/14 at #1, 4/14 in top 3, 5/14 in top 5, 11/14 retrievable, MRR 0.267  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=4018 fp=ab19d3a62e3514a5
- 3/14 at #1, 5/14 in top 3, 6/14 in top 5, 11/14 retrievable, MRR 0.325  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=3996 fp=ab19d3a62e3514a5
- 2/14 at #1, 4/14 in top 3, 5/14 in top 5, 11/14 retrievable, MRR 0.267  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=4018 fp=ab19d3a62e3514a5
- 3/14 at #1, 5/14 in top 3, 6/14 in top 5, 11/14 retrievable, MRR 0.325  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=3996 fp=ab19d3a62e3514a5
- 2/14 at #1, 4/14 in top 3, 5/14 in top 5, 11/14 retrievable, MRR 0.267  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=4018 fp=ab19d3a62e3514a5
- 2/14 at #1, 4/14 in top 3, 5/14 in top 5, 11/14 retrievable, MRR 0.267  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=4001 fp=ab19d3a62e3514a5
- 2/14 at #1, 4/14 in top 3, 5/14 in top 5, 11/14 retrievable, MRR 0.265  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=3998 fp=ab19d3a62e3514a5

## The 2026-08-20 cluster — read this before comparing those lines

Eight runs that day alternate between 3/14 and 2/14. They are **not** eight different search
configurations. They are the same search, measured while one question sat on a knife edge.

The runs at `chunks=3996` are the corpus at `main`; `chunks=3998-4018` add one knowledge file, one
`INDEX.md` table row and one edited line of `START-HERE.md`. The whole 3/14 -> 2/14 difference is
`what does duck mean` moving `glossary.md` from #1 to #6 — **on a corpus 2 chunks larger.**

Also note these were the first runs ever made off Windows, on `all-MiniLM-L6-v2`, using
`semantic-index/setup.sh`. `bge-small-en-v1.5` — the current default — still has **no score line at
all**: `huggingface.co` is blocked from the container the runs happened in. That measurement is still
owed, and it is step 3 of the handover.

**Confirmed once more after merging `main`:** the same 4001-chunk corpus, with only main's two commits
mixed in, scores **3/14 / MRR 0.325** — the row flipped back with no search change at all. A 1-point
move on this test set is noise. **Do not adopt or reject anything on it**, including the BGE comparison
that is still owed.

Full working: `semantic-index/rag-architecture-decisions.md`.
- 3/14 at #1, 5/14 in top 3, 6/14 in top 5, 11/14 retrievable, MRR 0.326  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=4001 fp=a5fe51a07241db26
- 3/14 at #1, 5/14 in top 3, 6/14 in top 5, 11/14 retrievable, MRR 0.325  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=4009 fp=5e2411794df50cea
