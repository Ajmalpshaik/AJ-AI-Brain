"""
Build the searchable index of the AJ AI Brain.

Run this once now, and again any time you add or change a skill, a knowledge
file, or a script fragment:

    "D:\\Ajmal\\AJ AI Brain\\semantic-index\\venv\\Scripts\\python.exe" ^
        "D:\\Ajmal\\AJ AI Brain\\semantic-index\\brain_index.py"

It READS skills/, knowledge/ and scripts/ and never writes to them. Everything
it creates goes into semantic-index/ only.

Every run rebuilds the index from scratch. That is deliberate: a partial
update would leave chunks behind for files you deleted or renamed, and a stale
index is worse than no index (the same reasoning the repo's .gitignore already
applies to graphify-out/).
"""

import re
import sys
import time

import brain_common as cfg

cfg.prepare_environment()  # must happen before chromadb is imported

BATCH_SIZE = 200

# --------------------------------------------------------------------------
# PARSERS
# --------------------------------------------------------------------------

HEADING_RE = re.compile(r"^(#{1,6})\s+(.*)$")
CS_KEY_RE = re.compile(r"^//\s*([A-Z][A-Z /()-]{2,30}):\s*(.*)$")
INPUTS_START_RE = re.compile(r"^//\s*-+\s*INPUTS")
INPUTS_END_RE = re.compile(r"^//\s*-+\s*END INPUTS")


def parse_cs(text: str):
    """
    Pull the meaningful parts out of a C# fragment.

    Fragments open with a comment header that carries PURPOSE (265 of 267 have
    one) and sometimes SOURCE, then usually a marked INPUTS block, then code.
    Returns a dict of the pieces found.
    """
    lines = text.split("\n")

    header_keys = {}
    current_key = None
    header_end = 0

    for i, line in enumerate(lines):
        stripped = line.strip()
        if stripped.startswith("//") or not stripped:
            match = CS_KEY_RE.match(stripped)
            if match:
                current_key = match.group(1).strip()
                header_keys[current_key] = match.group(2).strip()
            elif current_key and stripped.startswith("//"):
                # A wrapped continuation line of the previous key.
                cont = stripped.lstrip("/").strip()
                if cont and not set(cont) <= {"=", "-"}:
                    header_keys[current_key] += " " + cont
            header_end = i + 1
            if stripped and not stripped.startswith("//"):
                break
        else:
            header_end = i
            break

    # The INPUTS block: what you have to fill in to use this fragment.
    inputs_lines = []
    inside = False
    for line in lines:
        if INPUTS_START_RE.match(line.strip()):
            inside = True
            continue
        if INPUTS_END_RE.match(line.strip()):
            inside = False
            continue
        if inside:
            inputs_lines.append(line)

    body = "\n".join(lines[header_end:]).strip()

    return {
        "purpose": header_keys.get("PURPOSE", "").strip(),
        "source": header_keys.get("SOURCE", "").strip(),
        "other": {k: v for k, v in header_keys.items()
                  if k not in ("PURPOSE", "SOURCE")},
        "inputs": "\n".join(inputs_lines).strip(),
        "body": body,
    }


def parse_md(text: str):
    """
    Split a markdown file into frontmatter plus (heading, content) sections.
    Files with no headings come back as one section with an empty heading.
    """
    frontmatter = ""
    lines = text.split("\n")

    if lines and lines[0].strip() == "---":
        for i in range(1, len(lines)):
            if lines[i].strip() == "---":
                frontmatter = "\n".join(lines[1:i]).strip()
                lines = lines[i + 1:]
                break

    sections = []
    heading = ""
    buf = []
    for line in lines:
        match = HEADING_RE.match(line)
        if match:
            if buf and "\n".join(buf).strip():
                sections.append((heading, "\n".join(buf).strip()))
            heading = match.group(2).strip()
            buf = []
        else:
            buf.append(line)
    if buf and "\n".join(buf).strip():
        sections.append((heading, "\n".join(buf).strip()))

    return frontmatter, sections


def frontmatter_summary(frontmatter: str):
    """Pull name: and description: out of a SKILL.md frontmatter block."""
    name = desc = ""
    key = None
    for line in frontmatter.split("\n"):
        if line.startswith("name:"):
            key, name = "name", line[5:].strip()
        elif line.startswith("description:"):
            key, desc = "description", line[12:].strip()
        elif line.startswith(("  ", "\t")) and key:
            if key == "name":
                name += " " + line.strip()
            else:
                desc += " " + line.strip()
    return name.strip(), desc.strip()


# --------------------------------------------------------------------------
# CHUNK BUILDING
# --------------------------------------------------------------------------

def chunks_for_file(path, rel_path, area):
    """Turn one file into a list of (text, metadata) pairs ready to index."""
    text, bad_bytes = cfg.read_text(path)
    parts = rel_path.replace("\\", "/").split("/")
    if len(parts) > 2:
        category = "/".join(parts[1:-1])
    elif len(parts) == 2:
        category = parts[0]
    else:
        category = "root"  # a top-level document like AGENT-SPEC.md
    filename = path.name

    out = []

    def emit(pieces, kind, heading=""):
        for piece in pieces:
            out.append((piece, {"kind": kind, "heading": heading[:200]}))

    if path.suffix == ".cs":
        parsed = parse_cs(text)

        # The "card": what this fragment is, in words. Highest-value chunk.
        card = [f"{filename} — {area} in {category}"]
        if parsed["purpose"]:
            card.append("PURPOSE: " + parsed["purpose"])
        for key, value in parsed["other"].items():
            if value:
                card.append(f"{key}: {value}")
        emit(cfg.split_to_size("\n".join(card)), "card")

        if parsed["source"]:
            emit(cfg.split_to_size(f"{filename} SOURCE: {parsed['source']}"),
                 "source")

        if parsed["inputs"]:
            emit(cfg.split_to_size(
                f"{filename} — values you must fill in before running:\n"
                + parsed["inputs"]), "inputs")

        if parsed["body"]:
            emit(cfg.split_to_size(parsed["body"]), "code")

        # Safety net for the 2 files with no PURPOSE at all.
        if not out:
            emit(cfg.split_to_size(f"{filename}\n{text}"), "code")

    else:  # .md
        frontmatter, sections = parse_md(text)

        if frontmatter:
            name, desc = frontmatter_summary(frontmatter)
            if name or desc:
                emit(cfg.split_to_size(f"SKILL {name}: {desc}"), "card")
            else:
                emit(cfg.split_to_size(frontmatter), "card")

        for heading, content in sections:
            label = f"{filename} § {heading}" if heading else filename
            emit(cfg.split_to_size(f"{label}\n{content}"), "section", heading)

        if not out:
            emit(cfg.split_to_size(f"{filename}\n{text}"), "section")

    # Attach the metadata every chunk shares.
    results = []
    for i, (piece, extra) in enumerate(out):
        meta = {
            "path": rel_path.replace("\\", "/"),
            "abs_path": str(path),
            "area": area,
            "category": category,
            "filename": filename,
            "chunk_index": i,
        }
        meta.update(extra)
        results.append((f"{rel_path}#{i}".replace("\\", "/"), piece, meta))

    return results, bad_bytes


# --------------------------------------------------------------------------
# MAIN
# --------------------------------------------------------------------------

def main():
    start = time.time()
    print("AJ AI Brain — semantic index build")
    print(f"  reading from : {cfg.BRAIN_ROOT}")
    print(f"  writing to   : {cfg.SEMANTIC_ROOT}")
    print(f"  temp forced  : {cfg.RUN_TEMP}")
    print()

    if not cfg.BRAIN_ROOT.exists():
        print(f"ERROR: Brain folder not found: {cfg.BRAIN_ROOT}")
        return 1

    all_chunks = []
    file_counts = {}
    bad_files = []

    for folder_name, area in cfg.INDEX_TARGETS:
        folder = cfg.BRAIN_ROOT / folder_name
        if not folder.exists():
            print(f"  SKIPPED (missing): {folder_name}/")
            continue

        found = 0
        for path in sorted(folder.rglob("*")):
            if not path.is_file() or path.suffix.lower() not in cfg.FILE_EXTENSIONS:
                continue
            rel = str(path.relative_to(cfg.BRAIN_ROOT))
            chunks, bad = chunks_for_file(path, rel, area)
            if bad:
                bad_files.append(rel)
            all_chunks.extend(chunks)
            found += 1

        file_counts[folder_name] = found
        print(f"  {folder_name + '/':<12} {found:>4} files")

    # Root documents — named individually, because indexing the whole repo root
    # would sweep in throwaway files like fragment-compile-failures.txt.
    root_found = 0
    missing_root = []
    for name in cfg.ROOT_DOCS:
        path = cfg.BRAIN_ROOT / name
        if not path.is_file():
            missing_root.append(name)
            continue
        chunks, bad = chunks_for_file(path, name, "guide")
        if bad:
            bad_files.append(name)
        all_chunks.extend(chunks)
        root_found += 1

    file_counts["root docs"] = root_found
    print(f"  {'root docs':<12} {root_found:>4} files")

    if missing_root:
        print()
        for name in missing_root:
            print(f"  WARNING: root document not found, skipped: {name}")

    print()
    print(f"  {len(all_chunks)} chunks to index")

    if not all_chunks:
        print("ERROR: nothing found to index.")
        return 1

    cached = (cfg.MODEL_DIR / "all-MiniLM-L6-v2" / "onnx" / "model.onnx").exists()
    print("  loading embedding model"
          + (" from local cache (offline)..." if cached
             else " — downloading it once, ~80 MB..."))
    embedder = cfg.get_embedding_function()
    client = cfg.get_client()

    # Full rebuild: drop the old collection so deleted files leave no ghosts.
    try:
        client.delete_collection(cfg.COLLECTION_NAME)
        print("  removed previous index")
    except Exception:
        pass

    collection = client.create_collection(
        name=cfg.COLLECTION_NAME,
        embedding_function=embedder,
        metadata={"hnsw:space": "cosine"},
    )

    print("  embedding and storing...")
    for i in range(0, len(all_chunks), BATCH_SIZE):
        batch = all_chunks[i:i + BATCH_SIZE]
        collection.add(
            ids=[c[0] for c in batch],
            documents=[c[1] for c in batch],
            metadatas=[c[2] for c in batch],
        )
        done = min(i + BATCH_SIZE, len(all_chunks))
        print(f"    {done}/{len(all_chunks)}")

    stored = collection.count()
    elapsed = time.time() - start

    print()
    print("DONE")
    print(f"  files indexed : {sum(file_counts.values())}")
    for name, count in file_counts.items():
        print(f"      {name}/: {count}")
    print(f"  chunks stored : {stored}")
    print(f"  time taken    : {elapsed:.1f} seconds")
    print(f"  database at   : {cfg.CHROMA_DIR}")

    if bad_files:
        print()
        print("  WARNING — these files had characters that would not decode "
              "as UTF-8 and were indexed with replacements:")
        for name in bad_files:
            print(f"      {name}")

    if stored != len(all_chunks):
        print()
        print(f"  WARNING: expected {len(all_chunks)} chunks but the database "
              f"reports {stored}. Investigate before trusting results.")
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())
