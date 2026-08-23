# The harvest prompt — for pointing a fresh session at another repo

Written 2026-08-23 after three harvests in one day (a 69-tool extension, then a 203-tool platform, then a
third round on the same platform), and revised the same evening after a **fourth** — 12 repositories of
Revit developer LIBRARIES rather than tools ([`revit-libraries-harvest.md`](revit-libraries-harvest.md)).
Everything below is a lesson those four paid for.

**Paste the block into a new session and add the repo link.** It lives here so it is not retyped from
memory, which is how the reasoning gets lost.

---

```
Harvest this repo into the AJ AI Brain:  <PASTE THE REPO URL OR LOCAL PATH>

Read docs/pyrevit-harvest.md, docs/pyrevit-platform-harvest.md and docs/revit-libraries-harvest.md
first — they are the worked examples of this job, and the method below was learned from doing them.
The third one is the case where the target was LIBRARIES rather than tools; read it if what I am
pointing at has no buttons.

METHOD

1. Clone it to your scratchpad, not into my project folder. If it is a big repo use
   `git -c core.longpaths=true clone --depth 1` — Windows path limits break the plain clone.

2. Survey EVERY tool mechanically first: name, size, every Revit API call it actually makes,
   every BuiltInCategory it touches. Never judge a tool by its button label.

3. THEN READ THEM ALL PROPERLY. Do not stop at the survey. A survey shows what a tool CALLS
   and never what it had to LEARN, and that gap is where the value is. Last time the full
   read found a bug in a fragment written four hours earlier and a silent data-loss trap in
   an existing one — neither visible in any API list. Strip comments/imports/XAML and read in
   size bands so the volume stays manageable.

4. Give EVERY tool one of four verdicts, none skipped:
   BUILD (we have nothing) / UPGRADE (ours is weaker) / KEEP OURS (say why) / SKIP (nothing
   transferable — UI, ribbon wiring, dialogs).

5. BUILD what is missing. Do not defer it to "when a job asks". A fragment is additive,
   compile-checked and invisible until searched for — the "wait for evidence" rule in
   START-HERE.md is about INDEXING DOCUMENTS, where the cost is measured, not about code.
   I made that mistake and left five real capabilities unbuilt for no benefit.

RULES THAT ARE NOT NEGOTIABLE

- Everything becomes C#. The bridge runs C# only — run_csharp takes a C# string, run_fragment
  compiles .cs, there is no Python path. And check-scripts.cmd cannot compile-check Python, so
  a Python fragment would be the one part of the library with no version safety net.
- Never name the source anywhere in the Brain — not the repo, the author, the tool or the
  product. Write every technique as our own knowledge, in our own words.
- Compile-check before I ever run it: tools\check-scripts.cmd, against every Revit on this PC.
  Watch for the two known traps — ElementId.IntegerValue is REMOVED in Revit 2027 (use
  .ToString() for a label), and Definition.ParameterType is gone after 2023 (reach both by
  reflection; a try/catch does NOT help, it is a compile error).
- A fragment body cannot declare a class, so any technique whose answer is "implement this
  interface" is out of reach — say so instead of shipping something that cannot work.
- Dry-run by default on anything that changes the model, and say plainly in every header that
  it has not been run against a real model.

FINISH PROPERLY

- A ledger in docs/ with every verdict and its reasoning.
- An entry in knowledge/brain-log.md — write the length the finding deserves.
- node tools/sync-counts.mjs, then node tools/verify-consistency.mjs until it is clean.
- node tools/plugin-release.mjs (a push without the version bump reaches nobody).
- Tell me what you found in OUR code, not just what you copied. Every harvest so far has been
  worth more for that than for what came across.
```

---

## Why each rule is in there

| Rule | What it cost to learn |
|---|---|
| Read them all, not just the survey | The survey gave confident verdicts on 203 tools and missed a bug in a fragment written the same day |
| Build, don't defer | Five capabilities sat unbuilt because a rule about *indexing documents* was applied to *writing code* |
| C#, not Python | The bridge has no Python path, and the version checker cannot see Python at all |
| No source names | Ajmal's standing instruction, 2026-08-20 |
| Compile-check first | Two version traps in one day; each would have surfaced mid-job otherwise |
| Report what it found in OURS | Four harvests, and every one was worth more for that than for what it copied |

## When the target is a LIBRARY, not a set of tools (added after the fourth harvest)

A tool has a button label to be misled by, which is what step 2 of the method guards against. A library
has none — so the mechanical pass has to become something else, and this is what worked:

**Diff the API surface, not the file list.** Pull every `*Utils` / `*Filter` / `*Manager` class the
harvest touches, count how many of our fragments name each one, and let the zeros set the build list.
That single command produced most of the nine builds in the fourth harvest, including four capabilities
nobody had thought to ask for. It also finds the reverse — where we already use the class and theirs adds
nothing.

Two more things that only showed up on a library:

- **A wrapper library's real value is the LIST of what it wraps**, not the wrapping. The wrapped call is
  usually one line we can already write. The list is a map of Revit API capability, and the gaps in it
  against our own surface are the harvest.
- **Their tests and benchmarks are measured fact, and the highest signal per line in the whole repo** —
  a benchmark table states costs nobody would otherwise write down. Quote it with its conditions, never
  as if it were measured on our models.
