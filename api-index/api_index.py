"""Build the SEPARATE Revit API index from api-index/corpus/.

    index-api            build (or rebuild) the API index
    index-api --stats    just report what the corpus holds, index nothing

The corpus comes from running scripts/context/harvest-revit-api.cs through the
bridge — it reflects over the RevitAPI.dll your Revit actually loaded, so the
index describes YOUR version rather than a website's guess.

Reads the corpus. Writes only inside api-index/. Never touches the Brain's index.
"""

import re
import sys
import time
from pathlib import Path

import api_common as cfg

BATCH = 200


def chunks_for(path, split_to_size):
    """One chunk per API type (a `## Name (kind)` block), split further if huge."""
    text = path.read_text(encoding="utf-8")
    namespace = path.stem.replace("-", ".")
    out = []
    blocks = re.split(r"^## ", text, flags=re.M)[1:]
    for block in blocks:
        head = block.split("\n", 1)[0].strip()
        name = head.split("(")[0].strip()
        kind = head.split("(")[-1].rstrip(")").strip() if "(" in head else "type"
        body = f"{namespace}.{name} ({kind})\n## {block}".strip()
        pieces = split_to_size(body, cfg.CHUNK_TARGET, cfg.CHUNK_MAX, cfg.CHUNK_OVERLAP)
        for i, piece in enumerate(pieces):
            out.append((
                f"{namespace}.{name}#{i}",
                piece,
                {"namespace": namespace, "type": name, "kind": kind,
                 "path": f"api-index/corpus/{path.name}", "part": i},
            ))
    return out


def main(argv):
    cfg.prepare()
    if not cfg.CORPUS_DIR.is_dir() or not any(cfg.CORPUS_DIR.glob("*.md")):
        raise SystemExit(
            "No corpus yet at " + str(cfg.CORPUS_DIR) + "\n"
            "Run scripts/context/harvest-revit-api.cs through the bridge first — it reflects over the\n"
            "RevitAPI.dll your Revit has loaded and writes one file per namespace here."
        )

    brain = cfg.brain()
    files = sorted(cfg.CORPUS_DIR.glob("*.md"))
    all_chunks = []
    for f in files:
        all_chunks.extend(chunks_for(f, brain.split_to_size))

    print("Revit API index (SEPARATE from the Brain's own index)")
    print(f"  corpus     : {len(files)} namespace file(s)")
    print(f"  types      : {len({c[2]['type'] for c in all_chunks})}")
    print(f"  chunks     : {len(all_chunks)}")
    print(f"  database   : {cfg.CHROMA_DIR}")
    print(f"  collection : {cfg.COLLECTION_NAME}   (the Brain's is '{brain.COLLECTION_NAME}' — different DB, different name)")
    if "--stats" in argv:
        return 0

    start = time.time()
    embedder = brain.get_embedding_function()
    client = cfg.client()
    try:
        client.delete_collection(cfg.COLLECTION_NAME)
    except Exception:
        pass
    collection = client.create_collection(
        name=cfg.COLLECTION_NAME, embedding_function=embedder,
        metadata={"hnsw:space": "cosine"})

    for i in range(0, len(all_chunks), BATCH):
        batch = all_chunks[i:i + BATCH]
        collection.add(ids=[c[0] for c in batch],
                       documents=[c[1] for c in batch],
                       metadatas=[c[2] for c in batch])
        print(f"    {min(i + BATCH, len(all_chunks))}/{len(all_chunks)}")

    print()
    print("DONE")
    print(f"  stored     : {collection.count()} chunks")
    print(f"  time       : {time.time() - start:.1f} s")
    print()
    print("Ask it:  api-index\\ask-api.cmd \"what does FilteredElementCollector.UnionWith do\"")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
