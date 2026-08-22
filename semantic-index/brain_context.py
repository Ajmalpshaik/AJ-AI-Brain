"""Print a COMPACT Brain search result block, for injecting into an AI session's context.

brain_search_hybrid.py's normal output carries a long snippet per result - right for a
person reading the answer, far too heavy to prepend to every message someone types. This
prints only what is needed to decide which file to open: the path, what kind of file it
is, how it was found, and whether a fragment is proven.

Never touches Revit, and never changes the Brain. With --log it appends one line to
job-log/questions.jsonl, which is data ABOUT the Brain's use, not part of the Brain -
it sits outside every indexed folder so it can never compete in search results.

    brain_context.py "how do I stop ducts overlapping the ceiling"
    brain_context.py --top 3 "sprinkler spacing"
    brain_context.py --log "..."          also record the question and what came back
"""

import argparse
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
JOB_LOG = HERE.parent / "job-log" / "questions.jsonl"


def log_question(query, results):
    """Append one line to job-log/questions.jsonl. Never raises.

    Two jobs, both of which need months of real use before they pay off, which is why this
    starts recording as early as possible:

      1. Which files actually answer real questions - and therefore which of the 290 fragments
         are doing the work and which have never once been the answer.
      2. A question -> correct-file pair set. That is the shape of data needed to fine-tune an
         embedding model on Ajmal's own site vocabulary, which is this Brain's measured weak
         spot and cannot be fixed by re-ranking.

    Deliberately no timestamp precision beyond the date: this is a usage record, not an audit
    trail, and a coarse date keeps the file readable and its git diffs small.
    """
    try:
        from datetime import date

        JOB_LOG.parent.mkdir(parents=True, exist_ok=True)
        entry = {
            "date": date.today().isoformat(),
            "question": query,
            "hits": [
                {
                    "path": str(r.get("path", "")).replace("\\", "/"),
                    "meaning": r.get("meaning_rank"),
                    "words": r.get("word_rank"),
                }
                for r in results
            ],
        }
        with JOB_LOG.open("a", encoding="utf-8") as fh:
            fh.write(json.dumps(entry, ensure_ascii=False) + "\n")
    except Exception:
        # A log that fails must never break the search it is only recording.
        pass


def build_block(query, results, notes):
    """
    The compact block, as text. Split out from main() so the warm server can build the
    identical thing without a second copy of the format living in JavaScript.

    That matters more than it sounds: the last two lines are the guardrail against
    answering from general Revit knowledge, and a duplicate formatter in another language
    is exactly how a guardrail quietly stops being emitted.
    """
    lines = [f'Brain hits for "{query}":']
    for i, r in enumerate(results, start=1):
        path = str(r.get("path", "?")).replace("\\", "/")
        area = str(r.get("area", "")) or "?"
        status = r.get("status")
        tag = status if status in ("PROVEN", "unproven") else area
        meaning = r.get("meaning_rank")
        word = r.get("word_rank")
        found = []
        if meaning:
            found.append(f"meaning#{meaning}")
        if word:
            found.append(f"words#{word}")
        lines.append(f"  {i}. {path}  [{tag}]  {' '.join(found)}")

    lines.append(
        "  (top 3-5, not just #1 - open the file before answering; "
        "high in BOTH meaning and words is the strong signal)"
    )
    # The guardrail. Measured 2026-08-13: retrieval is right at #1 on 3 of 5 test questions, so
    # roughly two questions in five get a wrong file at the top. Without this line the failure
    # mode is silent and worse than no search at all - an answer from general Revit knowledge,
    # delivered with the confidence of one that came from Ajmal's own proven files. Saying "the
    # Brain does not cover this" is a correct answer; quietly inventing one is not.
    lines.append(
        "  (if none of these actually answer it, SAY SO - do not answer from general "
        "Revit knowledge while appearing to quote this Brain)"
    )

    # A stale index means these hits describe an older copy of the Brain. Say so inline: the
    # whole point of injecting this is that nobody has to go looking for a warning.
    if notes and "stale" in repr(notes).lower():
        lines.append("  (WARNING: index is STALE - these paths may be out of date)")

    return "\n".join(lines)


def main(argv):
    parser = argparse.ArgumentParser(description="Compact Brain search block for context.")
    parser.add_argument("query", nargs="+")
    parser.add_argument("--top", type=int, default=5)
    parser.add_argument("--log", action="store_true",
                        help="append the question and its hits to job-log/questions.jsonl")
    args = parser.parse_args(argv)

    query = " ".join(args.query).strip()
    if not query:
        return 0

    sys.path.insert(0, str(HERE))
    # Through the client, not hybrid_search directly: this runs on EVERY message typed
    # at a session, and it was paying 1,856 ms to load the embedding model each time.
    # The client uses a warm server when one is up and does the search here when it is
    # not, so the answer is identical either way and the first search after a reboot
    # simply leaves a warm server behind for the second.
    from brain_client import search

    results, notes, _source = search(query, top_k=args.top)
    if not results:
        return 0

    if args.log:
        log_question(query, results)

    print(build_block(query, results, notes))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
