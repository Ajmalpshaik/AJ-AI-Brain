# Score history

**This file is the single source of truth for how well the search works.** Quote it; do not quote a
remembered number. Every line from 2026-08-20 onward is stamped with the model, chunk settings, corpus
size and fingerprint that produced it.

Lines ABOVE that point carry no stamp and are **not comparable to each other or to anything since** — the
model, the chunk size and the corpus all changed underneath them without being recorded. That is how
three different accuracy figures (75%, 60%, 29%) ended up quoted around this Brain at the same time. They
are kept for history, not for comparison.

## The 2026-08-20 cluster — read this before comparing those lines

Eight runs that day alternate between 3/14 and 2/14. They are **not** eight different search
configurations. They are the same search, measured while one question sat on a knife edge.

The runs at `chunks=3996` are the corpus at `main`; `chunks=3998-4018` add one knowledge file, one
`INDEX.md` table row and one edited line of `START-HERE.md`. The whole 3/14 -> 2/14 difference is
`what does duck mean` moving `glossary.md` from #1 to #6 — **on a corpus 2 chunks larger.**

Also note these were the first runs ever made off Windows, on `all-MiniLM-L6-v2`, using
`semantic-index/setup.sh`. `bge-small-en-v1.5` still has **no score line at all**: `huggingface.co` is blocked from the container the runs happened in. That measurement is still
owed, and it is step 3 of the handover.

**Confirmed once more after merging `main`:** the same 4001-chunk corpus, with only main's two commits
mixed in, scores **3/14 / MRR 0.325** — the row flipped back with no search change at all. A 1-point
move on this test set is noise. **Do not adopt or reject anything on it**, including the BGE comparison
that is still owed.

Full working: `semantic-index/rag-architecture-decisions.md`.

## The 2026-08-21 run — read before reading the 11 -> 10

Automatic spelling correction was added that day (`brain_common.py` -> `correct_spelling`). The run
below it shows retrievable falling 11 -> 10, and **that was not the spelling change.** A controlled A/B
over every row in `test-questions.md`, spelling on versus off, gave **identical #1, identical top-5,
identical retrievable, and not one row changing rank.** The move came from the corpus edits made in the
same session - the third time a file written *about* the search has moved this score, which is the
warning already recorded in `rag-architecture-decisions.md`.

The useful lesson is the other way round. Spelling correction fires on **44% of real questions** and
demonstrably fixes them - on one, it put the correct fragment at #1 where the typed version had not
returned it at all - while scoring **exactly zero** here. The test rows' own typos happen to be the
ambiguous kind the correction deliberately refuses to guess at. A change can be real and invisible to a
14-row score at the same time; that is a fact about the score, not the change.

## THE TEST SET GREW TO 28 ROWS ON 2026-08-21 — lines above and below are NOT comparable

Fourteen rows were added from `job-log/questions.jsonl` — Ajmal's own real questions, expected answers
drafted by the assistant from the library and marked as such in `test-questions.md`. **A 14-row score and
a 28-row score measure different things**, so do not read the step in these numbers as a change in the
search. Nothing about retrieval changed in that run.

First 28-row baseline: **5/28 at #1, 7/28 in top 3, 10/28 in top 5, 20/28 retrievable, MRR 0.267.**

The step is worth understanding rather than smoothing over. On the old 14 rows the score was 3 at #1; the
28-row set has 5, so **the new rows are answered better than the seeded ones** — expected, because three
of the four surviving 2026-08-06 rows were deliberately chosen as documented *failures*. MRR fell
(0.313 -> 0.267) because the denominator doubled while the hard seeded rows stayed hard.

What this set now buys, which 14 rows could not: **8 rows fail outright and 10 more rank below #5.** That
is the material the parked decisions have been waiting for — the cross-encoder, BGE against MiniLM, the
meaning-versus-words balance, and `RRF_K`. Re-sweep those against this set, not against the old one.

One line per `score-brain` run, oldest first. Written automatically. **`score_brain.py` appends to
the END of this file, so any explanation you add goes ABOVE this line, never below it** — prose put
after the list gets buried one run at a time, and this is the file `CLAUDE.md` tells people to quote.

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
- 3/14 at #1, 5/14 in top 3, 6/14 in top 5, 11/14 retrievable, MRR 0.326  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=4001 fp=a5fe51a07241db26
- 3/14 at #1, 5/14 in top 3, 6/14 in top 5, 11/14 retrievable, MRR 0.325  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=4009 fp=5e2411794df50cea
- 3/14 at #1, 4/14 in top 3, 7/14 in top 5, 11/14 retrievable, MRR 0.323  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=3779 fp=5e2411794df50cea
- 3/14 at #1, 4/14 in top 3, 7/14 in top 5, 11/14 retrievable, MRR 0.317  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=3892 fp=5e2411794df50cea
- 3/14 at #1, 4/14 in top 3, 6/14 in top 5, 11/14 retrievable, MRR 0.314  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=3888 fp=9a5e1f22e59b67ed
- 3/14 at #1, 4/14 in top 3, 6/14 in top 5, 10/14 retrievable, MRR 0.314  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=3892 fp=a2722261e5266a63
- 3/14 at #1, 4/14 in top 3, 6/14 in top 5, 10/14 retrievable, MRR 0.314  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=3895 fp=a2722261e5266a63
- 2/14 at #1, 3/14 in top 3, 5/14 in top 5, 10/14 retrievable, MRR 0.252  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=3895 fp=a2722261e5266a63
- 3/14 at #1, 4/14 in top 3, 6/14 in top 5, 10/14 retrievable, MRR 0.314  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=3895 fp=a2722261e5266a63
- 3/14 at #1, 4/14 in top 3, 6/14 in top 5, 10/14 retrievable, MRR 0.314  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=3895 fp=a2722261e5266a63
- 3/14 at #1, 4/14 in top 3, 6/14 in top 5, 10/14 retrievable, MRR 0.313  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=3897 fp=dc35e3a2759240e4
- 3/14 at #1, 4/14 in top 3, 6/14 in top 5, 10/14 retrievable, MRR 0.313  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=3897 fp=dc35e3a2759240e4
- 3/14 at #1, 4/14 in top 3, 6/14 in top 5, 10/14 retrievable, MRR 0.313  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=3899 fp=dc35e3a2759240e4
- 3/14 at #1, 4/14 in top 3, 6/14 in top 5, 10/14 retrievable, MRR 0.313  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=3888 fp=dc35e3a2759240e4
- 3/14 at #1, 4/14 in top 3, 6/14 in top 5, 10/14 retrievable, MRR 0.313  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=3888 fp=dc35e3a2759240e4
- 5/28 at #1, 7/28 in top 3, 10/28 in top 5, 20/28 retrievable, MRR 0.267  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=3888 fp=dc35e3a2759240e4
- 5/28 at #1, 7/28 in top 3, 10/28 in top 5, 20/28 retrievable, MRR 0.267  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=3888 fp=dc35e3a2759240e4
- 5/28 at #1, 7/28 in top 3, 10/28 in top 5, 20/28 retrievable, MRR 0.267  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=3888 fp=dc35e3a2759240e4
- 5/28 at #1, 7/28 in top 3, 10/28 in top 5, 20/28 retrievable, MRR 0.267  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=3888 fp=dc35e3a2759240e4
- 5/28 at #1, 7/28 in top 3, 10/28 in top 5, 20/28 retrievable, MRR 0.267  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=3890 fp=dc35e3a2759240e4
- 5/28 at #1, 7/28 in top 3, 10/28 in top 5, 20/28 retrievable, MRR 0.267  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=3890 fp=dc35e3a2759240e4
- 5/28 at #1, 8/28 in top 3, 10/28 in top 5, 20/28 retrievable, MRR 0.262  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=4769 fp=b0ebc92d8d470e34
- 3/28 at #1, 8/28 in top 3, 10/28 in top 5, 20/28 retrievable, MRR 0.227  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=7225 fp=b0ebc92d8d470e34
- 3/28 at #1, 8/28 in top 3, 11/28 in top 5, 20/28 retrievable, MRR 0.233  |  model=all-MiniLM-L6-v2 chunk=900/1100/150 chunks=7234 fp=b0ebc92d8d470e34
