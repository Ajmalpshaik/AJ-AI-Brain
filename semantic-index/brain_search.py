"""
Ask the AJ AI Brain a question in plain English.

    "D:\\Ajmal\\AJ AI Brain\\semantic-index\\venv\\Scripts\\python.exe" ^
        "D:\\Ajmal\\AJ AI Brain\\semantic-index\\brain_search.py" ^
        "how do I colour ducts by system type"

Options:
    --top 5                 how many results to show (default 5)
    --area fragment         only C# fragments    (scripts/)
    --area knowledge        only knowledge notes (knowledge/)
    --area skill            only skill workflows (skills/)
    --area guide            only the top-level manuals (AGENT-SPEC.md etc.)

This only reads. It never changes the Brain and never touches the Revit bridge.
"""

import argparse
import sys

import brain_common as cfg

cfg.prepare_environment()  # must happen before chromadb is imported

AREA_LABEL = {
    "fragment": "FRAGMENT",
    "knowledge": "KNOWLEDGE",
    "skill": "SKILL",
    "guide": "GUIDE",
}

AREA_CHOICES = ["fragment", "knowledge", "skill", "guide"]


def search(query: str, top_k: int = 5, area: str = None):
    """
    Find the parts of the Brain that best match a plain-English question.

    Returns a list of dicts, best match first, one entry per FILE:
        path       - real path, relative to the Brain folder
        abs_path   - full path on disk
        area       - "fragment" | "knowledge" | "skill" | "guide"
        category   - the subfolder it lives in
        closeness  - 0-100, higher means a closer match
        snippet    - the piece of text that actually matched
        kind       - which part matched (card / inputs / code / section)
        heading    - the markdown heading, when the match came from one
    """
    collection = cfg.get_client().get_collection(
        name=cfg.COLLECTION_NAME,
        embedding_function=cfg.get_embedding_function(),
    )

    where = {"area": area} if area else None

    # Ask for extra hits, because several chunks of the same file often match
    # and we only want to show each file once.
    raw = collection.query(
        query_texts=[query],
        n_results=min(top_k * 6, max(collection.count(), 1)),
        where=where,
    )

    if not raw["ids"] or not raw["ids"][0]:
        return []

    best_per_file = {}
    for doc, meta, dist in zip(raw["documents"][0],
                               raw["metadatas"][0],
                               raw["distances"][0]):
        path = meta["path"]
        if path in best_per_file and best_per_file[path]["_dist"] <= dist:
            continue
        best_per_file[path] = {
            "path": path,
            "abs_path": meta.get("abs_path", ""),
            "area": meta.get("area", ""),
            "category": meta.get("category", ""),
            "kind": meta.get("kind", ""),
            "heading": meta.get("heading", ""),
            "closeness": round(max(0.0, 1.0 - dist) * 100, 1),
            "snippet": doc.strip(),
            "_dist": dist,
        }

    results = sorted(best_per_file.values(), key=lambda r: r["_dist"])[:top_k]
    for r in results:
        r.pop("_dist", None)
    return results


def main():
    parser = argparse.ArgumentParser(
        description="Search the AJ AI Brain in plain English.")
    parser.add_argument("query", nargs="+", help="your question")
    parser.add_argument("--top", type=int, default=5,
                        help="how many results to show (default 5)")
    parser.add_argument("--area", choices=AREA_CHOICES,
                        help="limit to one part of the Brain")
    args = parser.parse_args()

    query = " ".join(args.query)

    try:
        results = search(query, top_k=args.top, area=args.area)
    except Exception as exc:
        print(f"ERROR: {exc}")
        print("If this says the collection does not exist, run "
              "brain_index.py first.")
        return 1

    print()
    print(f'QUESTION: "{query}"')
    if args.area:
        print(f"LIMITED TO: {args.area}")
    print("=" * 78)

    if not results:
        print("No matches found.")
        return 0

    for i, r in enumerate(results, 1):
        label = AREA_LABEL.get(r["area"], r["area"].upper())
        print()
        print(f"{i}. [{label}]  {r['path']}")
        print(f"   closeness: {r['closeness']}%   "
              f"matched in: {r['kind']}"
              + (f" § {r['heading']}" if r["heading"] else ""))
        snippet = " ".join(r["snippet"].split())
        if len(snippet) > 300:
            snippet = snippet[:300] + " ..."
        print(f"   {snippet}")

    print()
    print("=" * 78)
    print("Paths are relative to the Brain folder. Open the file to read it "
          "in full.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
