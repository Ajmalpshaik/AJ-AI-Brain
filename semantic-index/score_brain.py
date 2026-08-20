"""Score the Brain's search against a fixed set of questions.

Retrieval was measured once, on 2026-08-06 - 24 questions, 13 right at #1. The score
was written down; the questions were not. So the single most useful measurement this
Brain ever made could not be repeated, and every later change to the model, the
chunking or the files would have been made blind.

This file exists so that number can be produced again on demand, before and after any
change, instead of guessing whether the change helped.

Reads only. Never changes the Brain, never touches Revit.

    score-brain              score every question in test-questions.md
    score-brain --self-test  check the parser and scorer, no model load needed
"""

import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
QUESTIONS = HERE / "test-questions.md"
HISTORY = HERE / "score-history.md"

# Where a search result keeps its repo-relative path. Confirmed 2026-08-13:
# hybrid_search returns a list of dicts keyed "path", already forward-slashed and
# relative to the Brain root. The alternatives are kept as fallbacks so a future
# change to the result shape fails loudly here instead of silently scoring zero and
# looking like a search problem.
_REL_KEYS = ("path", "rel", "rel_path", "relpath", "file", "source")


def result_path(item):
    """Pull the repo-relative path out of one search result."""
    if isinstance(item, str):
        return item.replace("\\", "/")
    if isinstance(item, dict):
        for key in _REL_KEYS:
            value = item.get(key)
            if isinstance(value, str):
                return value.replace("\\", "/")
        meta = item.get("metadata")
        if isinstance(meta, dict):
            for key in _REL_KEYS:
                value = meta.get(key)
                if isinstance(value, str):
                    return value.replace("\\", "/")
    if isinstance(item, (tuple, list)) and item and isinstance(item[0], str):
        return item[0].replace("\\", "/")
    raise SystemExit(
        f"Cannot find the file path in a search result: {item!r}\n"
        f"Add its key to _REL_KEYS at the top of {Path(__file__).name}."
    )


def parse_questions(text):
    """Read the markdown table into [(question, expected_path)].

    The file has more than one table, so rows are taken only while they look like a
    question/path pair: two cells, second one containing a slash.
    """
    rows = []
    for line in text.splitlines():
        line = line.strip()
        if not line.startswith("|"):
            continue
        cells = [c.strip() for c in line.strip("|").split("|")]
        if len(cells) != 2:
            continue
        question, expected = cells
        if not question or not expected:
            continue
        if question.lower() == "question":              # header row
            continue
        if set(question) <= set("-: "):                 # separator row
            continue
        if "/" not in expected:                         # prose table, not a question row
            continue
        rows.append((question, expected.replace("\\", "/")))
    return rows


def rank_of(expected, paths):
    """1-based position of expected in paths, or None if absent."""
    for i, path in enumerate(paths, start=1):
        if path == expected:
            return i
    return None


def stamp():
    """What this score was measured ON — model, chunking, index size, date.

    Added 2026-08-20 after three different accuracy figures (75%, 60%, 29%) were
    found quoted around the Brain with no way to tell what any of them measured.
    A bare number in a history file is not a measurement, it is a rumour: the
    model changed, the chunk size changed and the corpus grew between those runs,
    and nothing recorded it. Every line written from here on carries its settings.
    """
    sys.path.insert(0, str(HERE))
    import brain_common as cfg
    try:
        count = cfg.get_client().get_collection(cfg.COLLECTION_NAME).count()
    except Exception:
        count = "?"
    return {
        "model": cfg.EMBED_MODEL,
        "chunk": f"{cfg.CHUNK_TARGET}/{cfg.CHUNK_MAX}/{cfg.CHUNK_OVERLAP}",
        "chunks": count,
        "fingerprint": cfg.build_fingerprint(),
    }


def self_test():
    table = (
        "# heading\n"
        "some prose\n"
        "| Question | Should return |\n"
        "|---|---|\n"
        "| how many diffusers | skills/a/SKILL.md |\n"
        "| add 4 more floor levels | scripts/creators/create-levels.cs |\n"
        "\n"
        "| Question | Status on 2026-08-06 |\n"
        "|---|---|\n"
        "| how many diffusers | Correct at #1 |\n"
    )
    rows = parse_questions(table)
    assert rows == [
        ("how many diffusers", "skills/a/SKILL.md"),
        ("add 4 more floor levels", "scripts/creators/create-levels.cs"),
    ], rows

    assert rank_of("b.md", ["a.md", "b.md", "c.md"]) == 2
    assert rank_of("z.md", ["a.md", "b.md"]) is None
    assert rank_of("a.md", ["a.md"]) == 1

    assert result_path("knowledge\\a.md") == "knowledge/a.md"
    assert result_path({"path": "knowledge/a.md"}) == "knowledge/a.md"
    assert result_path({"rel": "knowledge/a.md"}) == "knowledge/a.md"
    assert result_path({"metadata": {"path": "scripts/b.cs"}}) == "scripts/b.cs"
    assert result_path(("skills/c/SKILL.md", 0.9)) == "skills/c/SKILL.md"

    print("self-test passed: parser, ranker and path extraction all OK")
    return 0


def main(argv):
    if "--self-test" in argv:
        return self_test()

    if not QUESTIONS.exists():
        raise SystemExit(f"No question file at {QUESTIONS}")

    rows = parse_questions(QUESTIONS.read_text(encoding="utf-8"))
    if not rows:
        raise SystemExit(f"No question rows found in {QUESTIONS}")

    sys.path.insert(0, str(HERE))
    from brain_search_hybrid import hybrid_search

    from brain_search_hybrid import CANDIDATES

    meta = stamp()
    print(f"Scoring {len(rows)} questions against the current index...")
    print(f"  model  {meta['model']}   chunks {meta['chunks']}   "
          f"chunk size {meta['chunk']}\n")

    # ONE search per question, at full pool depth. Every metric below is derived
    # from that single ranking, so #1 / top-3 / top-5 / found-at-all can never
    # disagree with each other the way separate runs could.
    at1 = at3 = at5 = 0
    reciprocal = 0.0
    in_pool = 0
    misses = []
    ranked_low = []
    first_notes = None

    for question, expected in rows:
        results, notes = hybrid_search(question, top_k=CANDIDATES)
        if first_notes is None:
            first_notes = notes
        paths = [result_path(r) for r in results]
        rank = rank_of(expected, paths)
        if rank == 1:
            at1 += 1
        if rank is not None and rank <= 3:
            at3 += 1
        if rank is not None and rank <= 5:
            at5 += 1
        if rank is not None:
            in_pool += 1
            reciprocal += 1.0 / rank
            if rank > 5:
                ranked_low.append((question, expected, rank))
        else:
            misses.append((question, expected, paths))

        # Per-question line, so a rank that slips from 1 to 3 is visible immediately
        # rather than hiding inside an unchanged "in top 3" total.
        where = f"#{rank}" if rank else "--"
        print(f'  {where:>3}  "{question}"')

    print()
    total = len(rows)
    mrr = reciprocal / total
    print(f"  #1 correct    {at1:3} / {total}   ({round(100 * at1 / total)}%)")
    print(f"  in top 3      {at3:3} / {total}   ({round(100 * at3 / total)}%)")
    print(f"  in top 5      {at5:3} / {total}   ({round(100 * at5 / total)}%)")
    print(f"  MRR           {mrr:.3f}          (1.000 = every answer at #1)")
    print()
    print(f"  retrievable   {in_pool:3} / {total}   "
          f"({round(100 * in_pool / total)}%)  <- found anywhere in the {CANDIDATES}-chunk pool")

    # THE diagnostic that decides what to fix next, added 2026-08-20.
    #
    #   answer not in the pool  -> a RETRIEVAL problem. Only a better embedding
    #                              model, better chunking or better query expansion
    #                              can help. Re-ranking provably cannot: it only
    #                              reorders what was already retrieved.
    #   in the pool but past #5 -> a RANKING problem, which is exactly what the
    #                              cross-encoder in rerank.py exists to fix.
    #
    # Without this split, both look identical on the score line and the obvious
    # next move (turn the re-ranker on) can be the wrong one.
    print()
    if misses:
        print(f"  RETRIEVAL problem - not in the pool at all ({len(misses)}):")
        for question, expected, got in misses:
            print(f'    "{question}"')
            print(f"        wanted  {expected}")
            print(f"        got #1  {got[0] if got else '(nothing)'}")
    if ranked_low:
        print(f"  RANKING problem - retrieved but below #5 ({len(ranked_low)}), "
              f"re-ranking could fix these:")
        for question, expected, rank in ranked_low:
            print(f'    #{rank:<3} "{question}"  wanted {expected}')
    if not misses and not ranked_low:
        print("  No retrieval or ranking problems: every answer is in the top 5.")

    # A score taken against a stale index is a score of an older Brain.
    if first_notes and "stale" in repr(first_notes).lower():
        print("\n  WARNING: the index looks STALE - this score is against an OLDER copy")
        print("           of the Brain. Rebuild, then score again.")

    if not HISTORY.exists():
        HISTORY.write_text(
            "# Score history\n\n"
            "One line per `score-brain` run, oldest first. Written automatically.\n\n",
            encoding="utf-8",
        )

    # Compare only against runs on the SAME model. Comparing a bge score with a
    # MiniLM score and calling the difference progress is how a number becomes a
    # rumour; that is the whole reason these lines are stamped.
    previous = [l for l in HISTORY.read_text(encoding="utf-8").splitlines()
                if l.startswith("- ")]
    same_model = [l for l in previous if "model=" + meta["model"] in l]
    if same_model:
        print(f"\n  previous run on {meta['model']}:\n    {same_model[-1][2:]}")
        try:
            was = int(same_model[-1].split(" at #1")[0].split("- ")[-1].split("/")[0])
            if at1 < was:
                print(f"\n  REGRESSION: #1 correct dropped {was} -> {at1}. "
                      f"Change something back, or explain it in brain-log.md.")
            elif at1 > was:
                print(f"\n  IMPROVED: #1 correct rose {was} -> {at1}.")
        except (ValueError, IndexError):
            pass
    elif previous:
        print(f"\n  No previous run on {meta['model']} - nothing to compare against.")
        print("  (Earlier lines without a model= stamp were written before "
              "2026-08-20 and are not comparable.)")

    with HISTORY.open("a", encoding="utf-8") as fh:
        fh.write(
            f"- {at1}/{total} at #1, {at3}/{total} in top 3, {at5}/{total} in top 5, "
            f"{in_pool}/{total} retrievable, MRR {mrr:.3f}"
            f"  |  model={meta['model']} chunk={meta['chunk']} "
            f"chunks={meta['chunks']} fp={meta['fingerprint']}\n"
        )

    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
