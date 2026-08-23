# Job log — what this Brain has actually been used for

Machine-written. Nobody edits these files by hand.

| File | One line per |
|---|---|
| `questions.jsonl` | question asked, and which files came back |
| `revit-runs.jsonl` | call that reached the live Revit model, and which fragments it used |

```bash
node tools/job-report.mjs            # what has actually been used
node tools/job-report.mjs --unused   # fragments never recorded running on a real job
```

## Why it exists

Nothing recorded what real work happened. Every session had real elements, real numbers, real
failures — and all of it evaporated when the session ended. Three questions about this Brain were
simply unanswerable:

- **Which of the 323 fragments actually do the work?** The standing guess is that about 40 do 90%
  of it. Nobody knows.
- **Which have never once run on a real job?**
- **Which fail repeatedly against a real model?** This is the most valuable signal the whole system
  produces, and until now it disappeared the moment a session ended.

## What it makes possible

**Pruning, safely.** An unused fragment is not free — it competes in every search, so it is one more
thing the right answer can lose to. But nothing should be deleted on a hunch. This is the evidence
that makes deleting a defensible decision instead of a guess.

**A fine-tune, eventually.** Every line in `questions.jsonl` is a *question → file* pair. That is
exactly the shape of data needed to train an embedding model on Ajmal's own site vocabulary — the
measured weak spot that re-ranking provably cannot fix (see `knowledge/brain-log.md`, 2026-08-13,
where the big-file theory was tested and refuted). A few hundred pairs is the entry price. Working
normally for a few months produces them as a by-product.

## Read it honestly

**The log started on 2026-08-13.** A fragment used heavily before that date shows as never-used, and
`--unused` says so every time it runs. It means *no evidence yet*, never *dead*. Give it months.

**Absence of a Revit run is not absence of use.** `revit-runs.jsonl` only fills when the AJ AI bridge
is connected and a script actually reaches the model.

## Why it lives here and not in `knowledge/`

`job-log/` sits outside every folder the semantic index reads. A steadily growing file inside
`knowledge/` would become another large matcher competing in search results — the exact fault
`brain-log.md` and `glossary.md` each had to be hand-discounted for. Keeping the log out of the index
means it can grow forever without ever degrading a search.
