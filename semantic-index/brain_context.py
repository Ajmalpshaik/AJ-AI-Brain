"""Print a COMPACT Brain search result block, for injecting into an AI session's context.

brain_search_hybrid.py's normal output carries a long snippet per result - right for a
person reading the answer, far too heavy to prepend to every message someone types. This
prints only what is needed to decide which file to open: the path, what kind of file it
is, how it was found, and whether a fragment is proven.

Reads only. Never changes the Brain, never touches Revit.

    brain_context.py "how do I stop ducts overlapping the ceiling"
    brain_context.py --top 3 "sprinkler spacing"
"""

import argparse
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent


def main(argv):
    parser = argparse.ArgumentParser(description="Compact Brain search block for context.")
    parser.add_argument("query", nargs="+")
    parser.add_argument("--top", type=int, default=5)
    args = parser.parse_args(argv)

    query = " ".join(args.query).strip()
    if not query:
        return 0

    sys.path.insert(0, str(HERE))
    from brain_search_hybrid import hybrid_search

    results, notes = hybrid_search(query, top_k=args.top)
    if not results:
        return 0

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

    # A stale index means these hits describe an older copy of the Brain. Say so inline: the
    # whole point of injecting this is that nobody has to go looking for a warning.
    if notes and "stale" in repr(notes).lower():
        lines.append("  (WARNING: index is STALE - these paths may be out of date)")

    print("\n".join(lines))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
