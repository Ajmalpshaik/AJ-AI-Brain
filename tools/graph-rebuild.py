"""Rebuild the knowledge graph's CODE side, keeping the dangling-edge repair, and report
how stale the markdown side has become.

WHY NOT JUST `graphify update .`
--------------------------------
That command goes extract -> build -> write in one step, so there is nowhere to insert
tools/graphify-repair.py. Running it would silently undo the repair that took 203 dangling
edges to 0 and recovered 162 edges (see knowledge/brain-log.md, 2026-08-13). This runs the
same pipeline with the repair in the middle, which is the only way the fix survives a rebuild.

WHAT IT CANNOT DO
-----------------
The markdown side needs an LLM: graphify's semantic extraction dispatches subagents, which is
neither automatic nor free. So this rebuilds CODE from the AST, reuses whatever markdown
extraction is already cached, and then does the one thing that was missing before - it SAYS
how many documents have changed since their cached extraction. That staleness used to be
completely invisible: search_graph would answer confidently from a graph built days ago and
nothing anywhere said so.

    python tools/graph-rebuild.py            rebuild if anything changed
    python tools/graph-rebuild.py --check     report only, write nothing
"""

import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "graphify-out"
SPEC = Path.home() / ".claude" / "skills" / "graphify" / "references" / "extraction-spec.md"


def main(argv):
    check_only = "--check" in argv

    if not (OUT / "graph.json").exists():
        print("No graph yet. Build it once with the full pipeline before this can maintain it.")
        return 0

    sys.path.insert(0, str(ROOT / "semantic-index"))
    from graphify.detect import detect
    from graphify.cache import check_semantic_cache
    from graphify.extract import collect_files, extract
    from graphify.build import build_from_json
    from graphify.cluster import cluster, score_all
    from graphify.analyze import god_nodes, surprising_connections, suggest_questions
    from graphify.report import generate
    from graphify.export import to_json

    det = detect(ROOT)
    (OUT / ".graphify_detect.json").write_text(json.dumps(det, ensure_ascii=False), encoding="utf-8")

    docs = [f for cat in ("document", "paper", "image") for f in det["files"].get(cat, [])]
    cn, ce, ch, uncached = check_semantic_cache(
        docs, root=str(ROOT), prompt_file=str(SPEC) if SPEC.exists() else None
    )

    # Machine-written files change on their own and would report stale forever. A warning
    # that is always on is a warning nobody reads - the same reasoning that made the STALE
    # INDEX banner compare content instead of modified-dates.
    ALWAYS_CHANGING = {"score-history.md"}
    uncached = [u for u in uncached if Path(u).name not in ALWAYS_CHANGING]

    # The whole point of the --check half: make the invisible visible.
    if uncached:
        print(f"MARKDOWN STALE: {len(uncached)} of {len(docs)} document(s) changed since their")
        print("  cached extraction. The code side below is current; the document side is not.")
        print("  Fixing it needs graphify's semantic pass (subagents or a GEMINI_API_KEY).")
        for u in uncached[:8]:
            print(f"    {Path(u).name}")
        if len(uncached) > 8:
            print(f"    ... and {len(uncached) - 8} more")
        print()
    else:
        print(f"Markdown side current: all {len(docs)} document(s) cached.\n")

    if check_only:
        return 0

    code_files = []
    for f in det["files"].get("code", []):
        p = Path(f)
        code_files.extend(collect_files(p) if p.is_dir() else [p])
    ast = extract(code_files, cache_root=ROOT)

    seen = {n["id"] for n in ast["nodes"]}
    nodes = list(ast["nodes"])
    for n in cn:
        if n["id"] not in seen:
            nodes.append(n)
            seen.add(n["id"])
    merged = {"nodes": nodes, "edges": ast["edges"] + ce, "hyperedges": ch,
              "input_tokens": 0, "output_tokens": 0}
    (OUT / ".graphify_extract.json").write_text(
        json.dumps(merged, indent=2, ensure_ascii=False), encoding="utf-8")

    # The step `graphify update` has no room for.
    repair = subprocess.run([sys.executable, str(ROOT / "tools" / "graphify-repair.py")],
                            capture_output=True, text=True, cwd=str(ROOT))
    for line in (repair.stdout or "").splitlines():
        if line.strip().startswith(("A ", "B ", "C ", "D ", "edges", "nodes", "dangling")):
            print("  " + line.strip())

    ex = json.loads((OUT / ".graphify_extract.json").read_text(encoding="utf-8"))
    G = build_from_json(ex, root=str(ROOT), directed=False)
    if G.number_of_nodes() == 0:
        print("ERROR: graph came out empty - refusing to overwrite a good graph.json")
        return 1

    communities = cluster(G)
    cohesion = score_all(G, communities)

    # Keep the names. They were written by hand and must survive a rebuild; anything new
    # falls back to its own hub, which is what graphify does anyway.
    saved = {}
    labels_file = OUT / ".graphify_labels.json"
    if labels_file.exists():
        saved = {int(k): v for k, v in json.loads(labels_file.read_text(encoding="utf-8")).items()}
    deg = dict(G.degree())
    labels = {}
    for cid, members in communities.items():
        if cid in saved and not str(saved[cid]).startswith("Community "):
            labels[cid] = saved[cid]
        else:
            hub = max(members, key=lambda n: deg.get(n, 0))
            labels[cid] = str(G.nodes[hub].get("label") or hub)[:47]

    questions = suggest_questions(G, communities, labels)
    wrote = to_json(G, communities, str(OUT / "graph.json"), force=True, community_labels=labels)
    report = generate(G, communities, cohesion, labels, god_nodes(G),
                      surprising_connections(G, communities), det,
                      {"input": 0, "output": 0}, str(ROOT), suggested_questions=questions)
    (OUT / "GRAPH_REPORT.md").write_text(report, encoding="utf-8")
    labels_file.write_text(json.dumps({str(k): v for k, v in labels.items()}, ensure_ascii=False),
                           encoding="utf-8")

    for f in (".graphify_detect.json", ".graphify_extract.json"):
        (OUT / f).unlink(missing_ok=True)

    print(f"  graph rebuilt: {G.number_of_nodes()} nodes, {G.number_of_edges()} edges, "
          f"{len(communities)} communities  (written: {wrote})")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
