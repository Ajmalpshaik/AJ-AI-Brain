# Site vocabulary — the words you say → the words the files use

**This file is data, not prose.** `semantic-index/ask-brain-hybrid.cmd` reads the table below before
every search: if your question contains a phrase in the left column, the words in the right column are
quietly added to the search. You keep talking your way; it translates before it looks.

**Add a row whenever a search misses because you used the site word.** That is the whole point — it is the
one file here that is meant to grow every time the search disappoints you. One row, then rebuild
(`semantic-index\index-brain.cmd`) is *not* needed for this file — the table is read live at search time,
so a new row works immediately.

Related but different: [`glossary.md`](glossary.md) explains what an ambiguous term *means* and how to
handle it. This file only says which words to also search for. A term can sensibly live in both.

## Rules for editing

- **Left column: what you actually type.** Lowercase. Multi-word phrases are fine and are matched before
  single words, so `floor level` wins over `floor`.
- **Right column: the words to search for instead**, space-separated. **Your phrase is REPLACED, not
  added to** — so a row can change an answer, not only widen it. Write rows carefully. (This line used to
  say the opposite. Adding alone was tried and was not enough: "add 4 more floor levels" kept the word
  "floor" and went on returning `create-floor.cs`, the slab creator, however much weight "level" got.
  A row exists *because* the site word actively misleads, so leaving it in place defeats the row.
  See `expand_query` in [`../semantic-index/brain_common.py`](../semantic-index/brain_common.py).)
- **Only the left-hand phrase is replaced.** Every other word you typed is untouched, and a longer phrase
  is consumed before its own shorter substring, so `floor level` is taken before a bare `floor`.
- **Do not add a row for a simple misspelling.** Since 2026-08-21 the search corrects those by itself,
  against the words the Brain actually contains: a word in no file that sits one letter from exactly one
  real word is corrected automatically, with no row needed. Rows are for when the *right* word is a
  **different word**, not a different spelling of the same one.
- **Never write a misspelling into a file in this repo — not even as an example.** These folders are the
  dictionary the corrector checks against, so spelling a word wrong here promotes the typo to a real word
  and switches the correction off for it. Proved the hard way on 2026-08-21: an example typo written into
  this very file stopped that typo being corrected, within one rebuild. Describe the mistake, never spell
  it out.
- **Do not add a row for a word that is already in the files.** "duct" needs no row. Rows earn their place
  by covering a gap.
- **Be careful with words that mean something else in Revit.** `floor` alone must NOT map to `level` —
  a floor is a real Revit category (a slab). That is exactly the mistake this table exists to fix:
  "add 4 more floor levels" returned `create-floor.cs`, the slab creator. Map the *phrase*, not the word.

## The table

| You say | Also search for | Why |
|---|---|---|
| diffuser | air terminal supply return | Revit calls it an Air Terminal; measured miss 2026-08-06 |
| grille | air terminal | same family, different site word |
| floor level | level elevation storey | "floor" alone is a slab — measured miss 2026-08-06 |
| floor levels | level elevation storey | plural of the above |
| light fitting | lighting fixture light | Revit category is Lighting Fixtures |
| light fittings | lighting fixture light | plural of the above |
| fire fighting | sprinkler nfpa hazard | dictation + site term, see glossary |
| fire figting | sprinkler nfpa hazard | recurring dictation spelling, see glossary |
| sprinkler point | sprinkler head coverage | "point" is site shorthand for a head |
| fire point | sprinkler head coverage | as above |
| vcd | volume control damper duct accessory | see glossary |
| ahu | air handling unit mechanical equipment | |
| fcu | fan coil unit mechanical equipment | |
| out to excel | export csv schedule | measured miss 2026-08-06 |
| to excel | export csv schedule | as above |
| excel | export csv | as above |
| missed to tag | tag status untagged missing | measured miss 2026-08-06 |
| not tagged | tag status untagged missing | as above |
| tag missing | tag status untagged missing | as above |
| level wise | group by level count | "count level wise" = group by Level |
| plant room | mechanical equipment room | |
| false ceiling | ceiling | site term for a suspended ceiling |
| isulate | isolate temporary hide | recurring dictation spelling, recorded live 2026-08-13 |
| biddest | biggest largest maximum | typing near-miss, recorded live 2026-08-13 |
| widh | width | typing near-miss, recorded live 2026-08-13 |
| isulate all | isolate temporary hide | as above |
| vcds | volume control damper duct accessory | plural of `vcd`; asked live 2026-08-13 |
| srinkler | sprinkler fire | recurring dictation spelling, recorded from Ajmal's own message 2026-08-20 |
| pendend | pendent sprinkler | his spelling, 2026-08-20 |
| upraght | upright sprinkler | his spelling, 2026-08-20 |
| beem | beam structural framing | his spelling, 2026-08-20 |
| colom | column structural column | his spelling, 2026-08-20 |
| celling | ceiling | his spelling, and it appears in nearly every sprinkler question |
| room boundy | room boundary | his phrasing, 2026-08-20 |
| wall sprinkler | sidewall sprinkler | site word for a sidewall head |
| how much from wall | distance to wall spacing | his phrasing of the wall-distance rule |
| how much from the slab | deflector deck distance below | his phrasing of the upright height rule |
| fire figting | fire fighting sprinkler | already-known dictation spelling, written down at last |
| sealing | ceiling | his dictation of "ceiling", recorded 2026-08-20 |
| sealing void | ceiling void concealed space | as above — and it is the concealed-space question |
| ceiling void | concealed space void sprinkler | the void between ceiling and slab; NFPA calls it a concealed space |
| print sprinkler | upright sprinkler | dictation near-miss for "upright", 2026-08-20 |
| lovers | louvre air terminal | his spelling, 2026-08-20 — "there is lovers but this we need to add in the airterminal" |
| lover | louvre | singular of the above; safe because nothing else in a Revit model is called this |
| highlate | highlight colour override | his spelling, 2026-08-20 — "can you highlate all of this remaning all keep gray" |
| paramters | parameters | recurring typing near-miss, recorded 2026-08-20 |

### Rows deliberately removed — a record, so they are not re-added

| Rejected row | Why it made things worse |
|---|---|
| `drawing` → `view sheet` | Too generic. It fires on almost any question and pulled "which pipes did I miss tagging **in this drawing**" onto a view-template fragment, burying `filter-by-tag-status.cs`. A row must name a *specific* thing, not a whole class. |
| `duct size` → `width height diameter` | Same problem: "size" appears everywhere, so the row added noise to every duct question rather than sharpening one. |

**The lesson both share:** a row earns its place by being *narrow*. If the left column could plausibly appear
in a question about anything, it will damage more searches than it repairs.
