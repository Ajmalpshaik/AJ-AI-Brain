#!/usr/bin/env python
"""
Ask every fragment's own job description back to the Brain's search, and report the ones the
search cannot find.

WHY THIS EXISTS. Ajmal, 2026-08-24: *"there is a chance that will make a issue first, it will not
come right thing, i have to sent message again and again"*. That is a RETRIEVAL failure, not a
missing capability - the fragment is on disk, it just never comes back. It had never been measured
per-fragment, so it could not be fixed per-fragment, and the only visible symptom was him repeating
himself.

WHAT IT MEASURES. For each fragment, the first sentence of its own PURPOSE is used as the query
(the fragment's own words for its own job, with its filename stripped out). If the fragment does
not come back in the top 5 of a search for its own description, nothing a person types is going to
find it either.

**THIS IS A LOWER BOUND ON THE PROBLEM, AND DELIBERATELY SO.** A fragment's own PURPOSE text shares
vocabulary with the chunk that was indexed, so the exact-words half of the hybrid search is being
handed the answer. Real questions use site words the file does not contain - that is this Brain's
measured weak spot (see semantic-index/score-history.md and knowledge/glossary.md). So a fragment
that fails THIS test is unreachable beyond argument; a fragment that passes it may still be
unreachable from a real sentence. Never quote a pass here as proof a fragment is findable.

THE SECOND CHECK - ASK -> WRITE. A question must never run a fragment that changes the model. Each
fragment is classified read-only or writing, and a read-only fragment whose search returns a WRITER
at #1 is reported separately. That is the dangerous class: measured 2026-08-24, "what are the room
sizes" returned action-dimension-rooms.cs, which draws dimensions into the model, at #1.

USAGE (from the repo root, takes a few minutes - it loads the embedding model once):
    semantic-index\\venv\\Scripts\\python.exe tools\\audit-fragment-routing.py
    semantic-index\\venv\\Scripts\\python.exe tools\\audit-fragment-routing.py --json out.json
Reads only. Never changes the Brain, never touches Revit.
"""
import argparse
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "semantic-index"))

from brain_search_hybrid import hybrid_search  # noqa: E402

# Header markers that mean "this fragment changes the model". A fragment is treated as READ-ONLY
# only when it says so and carries none of these - the safe direction, since mislabelling a writer
# as read-only is the error that would hide the very failure this tool exists to find.
WRITE_HINT = re.compile(
    r"\b(Transaction\s*\(|doc\.Delete|\.Create\(|NewOpening|dryRun|destructive|DESTRUCTIVE)\b")
READ_HINT = re.compile(r"(READ-ONLY|Read-only|read-only|no transaction|nothing is created|"
                       r"measures, changes nothing|The model never changes)")


def load_fragments():
    out = subprocess.run(["node", str(ROOT / "tools" / "fragment-index.mjs"), "--json"],
                         capture_output=True, text=True, encoding="utf-8", cwd=str(ROOT))
    if out.returncode != 0:
        sys.exit("fragment-index.mjs failed:\n" + (out.stderr or "")[:2000])
    return json.loads(out.stdout)


def first_sentence(purpose: str) -> str:
    """The fragment's own one-line statement of its job, as a person would say it."""
    text = re.sub(r"\s+", " ", purpose or "").strip()
    # Cut at the first sentence end that is not a filename ("...rooms.cs. Next..." must not split).
    parts = re.split(r"(?<=[a-z\)\"])\.\s+(?=[A-Z\"])", text)
    s = parts[0] if parts else text
    s = re.sub(r"\b[\w-]+\.cs\b", " ", s)          # strip filenames - they are a giveaway
    s = re.sub(r"[`\"*]", " ", s)
    s = re.sub(r"\s+", " ", s).strip()
    return s[:220]


def polarity(path: Path) -> str:
    try:
        head = path.read_text(encoding="utf-8", errors="replace")[:6000]
    except OSError:
        return "unknown"
    if READ_HINT.search(head) and not WRITE_HINT.search(head):
        return "read"
    return "write"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--json", help="also write the full result to this file")
    ap.add_argument("--top", type=int, default=5)
    ap.add_argument("--limit", type=int, default=0, help="only the first N fragments (for a smoke test)")
    args = ap.parse_args()

    frags = load_fragments()
    if args.limit:
        frags = frags[: args.limit]

    pol = {f["path"]: polarity(ROOT / "scripts" / f["path"]) for f in frags}

    rows, absent, askwrite, ranks = [], [], [], []
    for n, f in enumerate(frags, 1):
        q = first_sentence(f["purpose"])
        if not q:
            continue
        try:
            results, _ = hybrid_search(q, top_k=args.top, area="fragment", use_fragment_tool=False)
        except Exception as exc:                                  # noqa: BLE001
            print(f"  !! search failed for {f['path']}: {exc}", file=sys.stderr)
            continue
        paths = []
        for r in results:
            p = r.get("path") or r.get("source") or r.get("file") or ""
            p = str(p).replace("\\", "/")
            p = p.split("scripts/", 1)[1] if "scripts/" in p else p
            paths.append(p)
        rank = paths.index(f["path"]) + 1 if f["path"] in paths else 0
        ranks.append(rank)
        top1 = paths[0] if paths else ""
        row = {"path": f["path"], "status": f["status"], "query": q,
               "rank": rank, "top1": top1, "pol": pol.get(f["path"], "?"),
               "top1_pol": pol.get(top1, "?")}
        rows.append(row)
        if rank == 0:
            absent.append(row)
        if row["pol"] == "read" and row["top1_pol"] == "write" and top1 != f["path"]:
            askwrite.append(row)
        if n % 40 == 0:
            print(f"  ...{n}/{len(frags)}", file=sys.stderr)

    tot = len(ranks) or 1
    at1 = sum(1 for r in ranks if r == 1)
    at3 = sum(1 for r in ranks if 1 <= r <= 3)
    at5 = sum(1 for r in ranks if 1 <= r <= 5)
    print("\n=============== SELF-RETRIEVAL (each fragment searched by its own PURPOSE) ===============")
    print(f"  fragments tested        : {tot}")
    print(f"  found at #1             : {at1}  ({at1*100//tot}%)")
    print(f"  found in top 3          : {at3}  ({at3*100//tot}%)")
    print(f"  found in top 5          : {at5}  ({at5*100//tot}%)")
    print(f"  NOT FOUND AT ALL        : {len(absent)}  <- unreachable by their own description")
    print("  (a pass here is not proof of reachability - see this file's header)")

    if absent:
        print("\n=============== UNREACHABLE — not in the top 5 for its own PURPOSE ===============")
        for r in sorted(absent, key=lambda x: x["path"]):
            print(f"  [{r['status']:9}] {r['path']}")
            print(f"              its own words : {r['query'][:110]}")
            print(f"              what came #1  : {r['top1']}")

    if askwrite:
        print("\n=============== ASK -> WRITE — a read-only job whose top hit CHANGES THE MODEL ===========")
        for r in sorted(askwrite, key=lambda x: x["path"]):
            print(f"  [{r['status']:9}] {r['path']}")
            print(f"              asked         : {r['query'][:110]}")
            print(f"              got (writes!) : {r['top1']}")

    if args.json:
        Path(args.json).write_text(json.dumps(rows, indent=1), encoding="utf-8")
        print(f"\nfull result -> {args.json}")


if __name__ == "__main__":
    main()
