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

Read docs/pyrevit-harvest.md, docs/pyrevit-platform-harvest.md, docs/revit-libraries-harvest.md
and docs/revitplugins-harvest.md first — they are the worked examples of this job, and the method
below was learned from doing them. The third is the case where the target was LIBRARIES rather than
tools; read it if what I am pointing at has no buttons. The fourth is the BIGGEST target so far
(183k lines, 72 plugins) — read it if this one is large, for how to say honestly what was read and
what was not.

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
- When you CORRECT an existing fragment, correct its scripts/README.md row in the same pass. The
  consistency checker asks whether a row exists, never whether it is still true, and that row is what
  the next session routes from.
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
| Fix the README row with the fragment | A fragment corrected in the morning still had the old, wrong rule in its README row that evening — in the document a session routes from |

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


## When the session has no Windows, no Revit and no .NET (added after the seventh harvest)

A container session can still run a real compile gate, and a *better* API diff than the Windows one.
Both were built from scratch on 2026-08-24 and took about ten minutes; the recipe is worth keeping
because the instinct is to assume neither is possible and fall back to eyeballing the code.

**The API surface, three versions at once.** Pull the shipped `RevitAPI.dll` / `RevitAPIUI.dll` for each
Revit version from the public package feed, then **parse the CLI metadata tables directly** — no .NET
runtime needed. Walk TypeDef for public/nested-public types under `Autodesk.Revit`, then their MethodDef,
Field and Property rows; decode each method signature's parameter count so overloads separate. Emit each
name three ways: bare, `Type.Member`, and `Type.Member/arity`.

Three reasons this beats the PowerShell reflection it replaces:

- **No shared-process caching to be fooled by.** The documented trap — *"reflecting on two Revit
  versions in ONE PowerShell process silently gives you the first one twice"* — cannot happen, because
  nothing is loaded into a runtime at all.
- **2027 works.** Its `RevitAPI.dll` is .NET 10 and will not load in PowerShell 5.1 under any
  circumstances. A metadata parser does not care.
- **The arity data answers version questions outright.** It settled one the same day:
  `ParameterFilterRuleFactory.CreateBeginsWithRule` has only the 3-argument form on 2020, both on 2024,
  and only the 2-argument form on 2027 — so no single call spans the range, and the reflection dispatch
  our filter fragments already use is not caution, it is required.

**And the packages ship `RevitAPI.xml`** — Autodesk's own documentation — for the newer versions. That
turns judgement calls into quotations. Two findings that day rested on it, including the level-elevation
defect: quoting *"no matter what values of the Elevation Base parameter is set"* is a different quality
of evidence from reasoning about what the property probably means.

**The compile gate.** Roslyn's compiler package (`csc.exe`, the .NET Framework build) runs under Mono —
it needs `netstandard.dll` copied next to it from Mono's facades, and nothing else. Reference the real
`RevitAPI.dll` plus Mono's `4.7.1-api` / `4.8-api` reference assemblies for Revit 2020 / 2024, and the
.NET 10 reference pack from the same feed for 2027. Mirror the harness in `tools/check-scripts.ps1`
exactly, **including its `examples/prelude-smoke-test.cs` special case**.

**Validate the gate against the whole existing library before trusting it on new code.** That step
earned its keep twice over:

- It reported a **false** failure, because the "does this fragment declare its own `sb`?" test matched a
  line inside a HEADER COMMENT that shows `var sb = ...` as an instruction. Strip comment lines first. A
  gate that fails known-good files is worse than no gate — the next person learns to ignore it.
- It reported a **true** failure the Windows gate cannot see. Pinning the 4.7.x reference assemblies for
  Revit 2020 (which targets net47) caught `.ToHashSet(...)`, a .NET Framework **4.7.2** extension.
  `verify-fragments-compile.ps1` leaves the framework references empty for a .NET Framework Revit and
  lets `csc` use its defaults, which resolve to whatever is installed — 4.8 on any current box. **So the
  Windows gate has always been checking a newer framework than Revit 2020 targets.** Worth pinning
  deliberately rather than inheriting: the strictness is the point.

**Watch out for one operational trap.** Bash re-reads a script file as it executes, so **editing
`check.sh` while a background run of it is in flight corrupts that run** — it will resync at a random
point and interleave its output with the next run's. Kill the job before patching the gate, and treat
any log written across an edit as void.

**This does not replace `tools\check-scripts.cmd`.** That one compiles against the Revit versions
actually installed on the PC, which is the question that matters before Ajmal runs anything. The
container gate is a pre-flight — but it turns "compile-checked" from a promise into a fact while the
work is being written, rather than a step that waits for a different machine.

## Auditing OUR code for a defect the harvest surfaced (added after the seventh harvest)

The most valuable output of a harvest is not a new fragment — it is a defect found in the library that
was already here. Both of the biggest findings on 2026-08-24 were that shape. And **both times the audit
itself went wrong in the same way**, so the method is worth writing down.

**A grep that finds nothing is evidence about the PATTERN, never about the CODE.** The level-elevation
audit searched for `Level\.Elevation`. That matches `room.Level.Elevation` and is blind to
`level.Elevation` / `lvl.Elevation` / `l.Elevation` — where the variable is already a `Level`, which is
the commoner shape by far. It returned nothing beyond what was already known, and the nothing was
written up as *"checked and clean: none of the other session's 28 fragments carries the defect"*. That is
a claim about the code resting on an untested pattern. A peer session found two it had missed; running
the corrected sweep across everything found **three more, in our own oldest creators** — live-verified
fragments that had been placing walls, floors and ceilings at the wrong height for weeks.

So, before reporting any sweep clean:

1. **Prove the pattern can see a known instance.** Grep for a case you have already fixed and check it
   comes back. One line. It would have caught this immediately.
2. **Search for the property, not for the expression you happen to remember.** `\.Elevation\b`, not
   `Level\.Elevation`. Cast wide, then read.
3. **Expect most hits to be correct code, and write down the verdict rule.** That sweep returns roughly
   two dozen hits of which only five were defects; the rest were sorting, report rows, and different
   types that share the word. A sweep that "fixes" every hit is as wrong as one that fixes none — so the
   knowledge note has to carry the table that separates them, or the next session re-derives it or,
   worse, mass-edits.
4. **Never claim a clean result for files you did not write.** A peer session's summary of it: *"a
   finding from a peer session is evidence, but its 'and I checked X is clean' about files it did not
   write is not proof."*

**And a live-verified fragment is not immune.** All three creators passed a real run on 2026-08-07. They
passed because the test model's levels use Elevation Base = Project, where both properties return the
same number — the test exercised the case that works and proved nothing about the case that breaks. When
a defect is *conditional on model configuration*, "verified" in `scripts/README.md` carries no weight
against it. Say so in the row when you fix it, rather than leaving a verification date that implies the
question was asked.
