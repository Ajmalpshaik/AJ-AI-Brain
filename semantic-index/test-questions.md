# Brain search test questions

Each row is a question in a modeller's own words, and the file that should come back.
`score-brain.cmd` runs every row through the real search and prints a score.

## Why this file exists

Retrieval was measured once, on 2026-08-06 — 24 questions, 13 right at #1. **The score was
written down; the questions were thrown away.** So the most useful measurement this Brain
ever made could not be repeated, and every later change to the model, the chunking or the
files would have been made blind.

Four of those questions survive, quoted inside `knowledge/brain-log.md` and
`semantic-index/README.md`. They are seeded below. The rest are gone.

## Ajmal writes the questions — not the assistant

Questions written by whoever is tuning the search prove nothing. They get unconsciously
shaped into questions it can already answer, and the score becomes a compliment instead of
a measurement. The 2026-08-06 run was worth trusting **precisely because independent
testers wrote it**.

So: add rows in your own words, the way you would say it out loud on site.

Rules for adding a row:

- **Say it the way you would say it** — site words and all. "duck" for duct, "light
  fitting" for lighting fixture. The site vocabulary is the measured weak spot, so those
  are the most valuable rows in the file.
- **The expected file is the one you would be happy to be handed.** One file per row.
- **A question that is answered WRONG today is the most valuable kind.** Add it anyway.
  That is the whole point — it is what proves a later fix actually fixed something.
- Paths are repo-relative, with forward slashes, exactly as the search prints them.

## What the score means right now

The seed set is **deliberately unrepresentative**: three of the four recovered rows are the
documented *failures* from 2026-08-06, because those were the ones worth writing down. So a
low score here is expected and is **not** comparable to the old 13/24.

Treat it as a **regression guard** — proof that a change did not break what worked, and a
target list for what to fix — not as a verdict on the Brain's quality. It becomes a real
quality measure once there are 20+ rows written by a person.

## The questions

| Question | Should return |
|---|---|
| how many diffusers do I need in this room | skills/ajtools-hvac-terminal-layout/SKILL.md |
| add 4 more floor levels | scripts/creators/create-levels.cs |
| how many light fitting | scripts/actions/reporting/action-count-by-group.cs |
| take my door schedule out to excel | scripts/actions/sheets-views/action-export-schedule-to-csv.cs |
| what does duck mean | knowledge/glossary.md |
| can you tell me the how meny vcd is there and tell me the size | scripts/actions/reporting/action-count-and-report.cs |
| can oyu tell me what is the biggest size of duct | knowledge/glossary.md |
| okkey can you isulate all the vcds | skills/ajtools-live-model/SKILL.md |
| can you isulate only the biddest widh vcd only | skills/ajtools-live-model/SKILL.md |
| i need now all the vcds that bigger counts | skills/ajtools-live-model/SKILL.md |
| now can you change the color to red | scripts/actions/color-graphics/action-set-color-uniform.cs |
| is that this model contail spaces | skills/ajtools-live-model/SKILL.md |
| what is the biggest space | skills/ajtools-live-model/SKILL.md |
| now tell me what is in this how meny airterminal is there | skills/ajtools-live-model/SKILL.md |

## Where each seeded row came from

| Question | Status on 2026-08-06 | Recorded in |
|---|---|---|
| how many diffusers do I need in this room | **Correct** at #1 — the case hybrid search was built to fix. Must not regress. | `semantic-index/README.md` |
| add 4 more floor levels | **Wrong** — returned `create-floor.cs`, the slab creator. Actively misleading. | `knowledge/brain-log.md` |
| how many light fitting | **Wrong** — matched "light hazard" in the sprinkler files. | `knowledge/brain-log.md` |
| take my door schedule out to excel | **Wrong** — the right file was absent from the top 5 entirely. | `knowledge/brain-log.md` |
| the seven rows from `isulate all the vcds` to `how meny airterminal is there` | **All Ajmal's own, asked during one real working session on 2026-08-13**, spellings kept exactly. They were captured late, in a batch, because the assistant promised to record each one as it was asked and then forgot seven times running — which is why `tools/test-row-nudge.mjs` now exists. **Expected answers come from `START-HERE.md`'s own routing table**, which sends "querying or changing the live, open Revit model right now — counts, sizes, view isolation" to `ajtools-live-model`; the colour one points at the specific fragment instead. **They are the assistant's reading of Ajmal's routing rules, not Ajmal's judgement — he should correct any that are wrong.** Note five share one expected file: that is what the routing says, and if the Brain cannot return the live-model skill for live-model questions, that is a real finding rather than a weak test. | asked live |
| can oyu tell me what is the biggest size of duct | **Ajmal's own, asked live 2026-08-13**, his spelling kept. **A guard row: the Brain currently gets this RIGHT and must keep doing so.** `glossary.md` ranked #1 on *both* meaning and words, and that is the correct answer — its 2026-08-04 note says "the maximum duct size" has no single answer, because round carries Diameter and rectangular carries Width × Height, and sorting by the FIRST dimension gives the widest duct rather than the biggest one. The live model proved it: widest is 1524 × 470 (0.72 m²), biggest by area is 1234 × 992 (1.22 m²). A fragment returning one number here would be *less* useful than the warning. | asked live |
| can you tell me the how meny vcd is there and tell me the size | **Ajmal's own, asked live 2026-08-13**, kept with his spelling. **Fails badly:** `action-count-and-report.cs` — PROVEN, and literally described as "bare count, or a size-breakdown table when asked" — is *absent from the entire candidate pool*, along with `action-count-by-group.cs` and `filter-by-category.cs`. Site vocabulary fired correctly (`vcd → volume control damper duct accessory`); ranking is what failed. **Expected answer chosen by the assistant — Ajmal to confirm or correct.** | asked live |
| what does duck mean | **BROKEN AT EVERY WEIGHT SINCE 2026-08-20 — do not re-chase it as a fresh bug.** Re-swept across 0.85-1.00 on the current corpus: the glossary now peaks at #5 and the sprinkler files beat it outright; no discount returns it to #1. It flipped from #1 to #6 on a **2-chunk** corpus change, which is what it now measures — knife-edge noise, not search quality. The full sweep is in `semantic-index/rag-architecture-decisions.md`. Fixing it needs more rows in this file, not another weight. Original note: **Guard row, not a quality measure.** Added by the assistant on 2026-08-13, and labelled as such because assistant-written questions must never be counted as evidence the search is good. Its only job is to stop one specific fix overshooting: discounting `glossary.md` to stop it displacing the terminal-layout skill also sinks the glossary on questions it genuinely answers. At 0.85 this returned `nfpa13-sprinkler-spacing.md` at #1. | this file |

The three failures are all **vocabulary**, not ranking: the site word simply is not in the
file that answers it. `knowledge/site-vocabulary.md` is the fix, and it is read live — a new
row there works immediately, with no rebuild.
