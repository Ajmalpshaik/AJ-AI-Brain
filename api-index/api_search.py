"""Search ONLY the Revit API index. Never reads the Brain's own collection.

    ask-api "how do I get every element of a category"
    ask-api --top 10 "connector"
"""

import argparse
import sys

import api_common as cfg


def main(argv):
    ap = argparse.ArgumentParser(description="Search the separate Revit API index.")
    ap.add_argument("query", nargs="+")
    ap.add_argument("--top", type=int, default=5)
    args = ap.parse_args(argv)
    question = " ".join(args.query)

    cfg.prepare()
    brain = cfg.brain()
    client = cfg.client()
    try:
        collection = client.get_collection(cfg.COLLECTION_NAME,
                                           embedding_function=brain.get_embedding_function())
    except Exception:
        raise SystemExit(
            "No API index yet.\n"
            "  1. run scripts/context/harvest-revit-api.cs through the bridge\n"
            "  2. api-index\\index-api.cmd"
        )

    raw = collection.query(query_texts=[question],
                           n_results=min(args.top, max(collection.count(), 1)))
    print(f'Revit API index — "{question}"\n')
    if not raw["ids"] or not raw["ids"][0]:
        print("  nothing matched")
        return 0
    for rank, (doc, meta, dist) in enumerate(
            zip(raw["documents"][0], raw["metadatas"][0], raw["distances"][0]), start=1):
        close = round(max(0.0, 1.0 - dist) * 100, 1)
        print(f"  #{rank}  {meta['namespace']}.{meta['type']}  ({meta['kind']}, closeness {close})")
        first = "\n".join(doc.strip().splitlines()[:4])
        print("      " + first.replace("\n", "\n      "))
        print()
    print("This is the API reference index. For how this Brain actually DOES a job,")
    print("ask the Brain instead:  semantic-index\\ask-brain-hybrid.cmd \"...\"")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
