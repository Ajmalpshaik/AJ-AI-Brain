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

    def emit(pieces, kind, heading="", prefix=""):
        """Add chunks, giving EVERY piece its context line - not just the first.

        This is the mechanical half of Anthropic's Contextual Retrieval (2024): a chunk that
        does not say what it belongs to cannot be retrieved by a question about the thing it
        belongs to. Two real losses were found here on 2026-08-13:

          * `.cs` CODE chunks were emitted as raw body text with no filename and no PURPOSE,
            so a code chunk could only ever match on incidental variable names.
          * `.md` SECTION chunks had the label folded into the text BEFORE splitting, so a
            section long enough to split kept its heading on piece 1 and lost it on 2, 3, 4...

        Anthropic's full version writes an LLM sentence per chunk and reports a 35% drop in
        retrieval failures. This is the free part - deterministic, no model, no per-chunk cost.
        """
        for piece in pieces:
            text = f"{prefix}\n{piece}" if prefix and not piece.startswith(prefix) else piece
            out.append((text, {"kind": kind, "heading": heading[:200]}))

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
            # MEASURED 2026-08-13: prefixing every code chunk with `filename — PURPOSE: ...`
            # was tried and REVERTED. A code chunk carries no context at all, so adding it
            # looked like a free win - it is the mechanical half of Anthropic's Contextual
            # Retrieval, which reports a 35% drop in retrieval failures. Here it scored WORSE:
            # recall@5 fell 7/14 -> 5/14, with and without the matching markdown change, so
            # the code prefix alone was enough to cause it.
            #
            # Best reading of why: the KIND_WEIGHT in brain_search_hybrid.py already scores a
            # code chunk at 0.35 precisely because variable names are incidental. Injecting the
            # PURPOSE line into every code piece hands those chunks the same high-value words
            # the `card` chunk carries, so a fragment's code starts competing with its own card
            # - and with other fragments' cards - on text that was deliberately weighted down.
            # The context was already in the index; it was in the chunk built for it.
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
            # MEASURED 2026-08-13: labelling EVERY piece of a section was tried and REVERTED.
            # It reads like an obvious improvement - a continuation chunk currently has no idea
            # which file it came from - but scored worse: recall@5 fell 7/14 -> 5/14, and
            # "isulate all the vcds" and "change the color to red" both dropped out of the top
            # five. Repeating `filename § heading` across every piece of a long section dilutes
            # each chunk's distinctive content, and inflates those tokens' document frequency
            # so BM25's rarity weighting counts them for less. Markdown sections keep the label
            # on piece one only. The `.cs` CODE chunks are a different case and DO get a prefix
            # on every piece - they previously carried no context at all, so there was nothing
            # to dilute.
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

def collect_targets():
    """
    Everything the index covers, as [(path, rel, area)].

    Root documents are named individually rather than globbed, because
    indexing the whole repo root would sweep in throwaway files like
    fragment-compile-failures.txt.
    """
    targets, missing = [], []

    for folder_name, area in cfg.INDEX_TARGETS:
        folder = cfg.BRAIN_ROOT / folder_name
        if not folder.exists():
            missing.append(folder_name + "/")
            continue
        for path in sorted(folder.rglob("*")):
            if path.is_file() and path.suffix.lower() in cfg.FILE_EXTENSIONS:
                rel = str(path.relative_to(cfg.BRAIN_ROOT)).replace("\\", "/")
                targets.append((path, rel, area))

    for name in cfg.ROOT_DOCS:
        path = cfg.BRAIN_ROOT / name
        if path.is_file():
            targets.append((path, name, "guide"))
        else:
            missing.append(name)

    return targets, missing


def chunks_for(targets):
    """Chunk a list of targets. Returns (chunks, files_with_bad_bytes)."""
    chunks, bad = [], []
    for path, rel, area in targets:
        made, had_bad = chunks_for_file(path, rel, area)
        if had_bad:
            bad.append(rel)
        chunks.extend(made)
    return chunks, bad


def store(collection, chunks, label):
    """Embed and write chunks in batches, reporting progress."""
    if not chunks:
        return
    print(f"  {label} {len(chunks)} chunk(s)...")
    for i in range(0, len(chunks), BATCH_SIZE):
        batch = chunks[i:i + BATCH_SIZE]
        collection.add(
            ids=[c[0] for c in batch],
            documents=[c[1] for c in batch],
            metadatas=[c[2] for c in batch],
        )
        done = min(i + BATCH_SIZE, len(chunks))
        if len(chunks) > BATCH_SIZE:
            print(f"    {done}/{len(chunks)}")


def drop_files(collection, rels):
    """
    Remove every chunk belonging to these files.

    This is the step that makes an incremental rebuild trustworthy. A file that
    used to produce 12 chunks and now produces 8 would otherwise leave 4 ghosts
    behind - text that no longer exists anywhere in the Brain, still answering
    questions. Deleting by the `path` metadata clears them whatever the count.
    """
    if not rels:
        return
    try:
        collection.delete(where={"path": {"$in": list(rels)}})
    except Exception:
        for rel in rels:  # older Chroma without $in
            collection.delete(where={"path": rel})


def main():
    start = time.time()
    force_full = "--full" in sys.argv

    print("AJ AI Brain — semantic index build")
    print(f"  reading from : {cfg.BRAIN_ROOT}")
    print(f"  writing to   : {cfg.SEMANTIC_ROOT}")
    print(f"  temp forced  : {cfg.RUN_TEMP}")
    print()

    if not cfg.BRAIN_ROOT.exists():
        print(f"ERROR: Brain folder not found: {cfg.BRAIN_ROOT}")
        return 1

    targets, missing = collect_targets()
    if not targets:
        print("ERROR: nothing found to index.")
        return 1
    for name in missing:
        print(f"  WARNING: not found, skipped: {name}")

    current = {rel: cfg.fingerprint(path) for path, rel, _ in targets}
    manifest = cfg.read_manifest()
    build_now = cfg.build_fingerprint()

    # ---- decide: incremental, or start over? ----------------------------
    reason = None
    if force_full:
        reason = "--full requested"
    elif manifest is None:
        reason = "no manifest from a previous build"
    elif manifest.get("build") != build_now:
        reason = "chunking rules or settings changed"

    client = cfg.get_client()
    # The model is never downloaded on demand any more: get_embedding_function()
    # refuses to run without it, rather than risk an index built half in one
    # vector space and half in another. See embed_bge.py.
    print("  model        : " + cfg.EMBED_MODEL)
    embedder = cfg.get_embedding_function()

    collection = None
    if reason is None:
        try:
            collection = client.get_collection(
                name=cfg.COLLECTION_NAME, embedding_function=embedder)
        except Exception:
            reason = "no existing index to update"

    # ---- full rebuild ----------------------------------------------------
    if reason is not None:
        print(f"  FULL REBUILD ({reason})")
        try:
            client.delete_collection(cfg.COLLECTION_NAME)
        except Exception:
            pass
        collection = client.create_collection(
            name=cfg.COLLECTION_NAME, embedding_function=embedder,
            metadata={"hnsw:space": "cosine"})

        chunks, bad = chunks_for(targets)
        store(collection, chunks, "embedding")
        cfg.write_manifest([rel for _, rel, _ in targets])

        print()
        print("DONE (full rebuild)")
        print(f"  files indexed : {len(targets)}")
        print(f"  chunks stored : {collection.count()}")
        print(f"  time taken    : {time.time() - start:.1f} seconds")
        report_bad(bad)
        return 0

    # ---- incremental ------------------------------------------------------
    recorded = manifest["files"]
    changed = sorted(r for r in current if r in recorded
                     and recorded[r] != current[r])
    added = sorted(r for r in current if r not in recorded)
    removed = sorted(r for r in recorded if r not in current)

    if not (changed or added or removed):
        print("  UP TO DATE — nothing has changed since the last build.")
        print(f"  files : {len(targets)}   chunks : {collection.count()}")
        print(f"  time taken    : {time.time() - start:.1f} seconds")
        return 0

    print(f"  INCREMENTAL — {len(changed)} changed, {len(added)} new, "
          f"{len(removed)} deleted "
          f"({len(current) - len(changed) - len(added)} untouched, skipped)")
    for label, group in (("changed", changed), ("new", added),
                         ("deleted", removed)):
        for rel in group[:5]:
            print(f"      {label:<8} {rel}")
        if len(group) > 5:
            print(f"      {'':<8} ... and {len(group) - 5} more")

    # Old chunks go first, so a shrinking file cannot leave ghosts behind.
    drop_files(collection, set(changed) | set(removed))

    rebuild = set(changed) | set(added)
    chunks, bad = chunks_for([t for t in targets if t[1] in rebuild])
    store(collection, chunks, "embedding")

    cfg.write_manifest([rel for _, rel, _ in targets])

    print()
    print("DONE (incremental)")
    print(f"  files indexed : {len(targets)}")
    print(f"  re-embedded   : {len(rebuild)} file(s), {len(chunks)} chunk(s)")
    print(f"  chunks stored : {collection.count()}")
    print(f"  time taken    : {time.time() - start:.1f} seconds")
    print(f"  database at   : {cfg.CHROMA_DIR}")
    report_bad(bad)
    return 0


def report_bad(bad_files):
    if bad_files:
        print()
        print("  WARNING — these files had characters that would not decode "
              "as UTF-8 and were indexed with replacements:")
        for name in bad_files:
            print(f"      {name}")


if __name__ == "__main__":
    sys.exit(main())
