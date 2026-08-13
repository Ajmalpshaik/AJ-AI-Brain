# Reply Style

How to answer, separate from what to build or fix.

- **Quantity/count questions** ("how many VCDs", "how many mechanical equipment"): answer with the bare
  number, one line. No table, no extra prose, unless the user asks for sizes/breakdown.
- **Size/breakdown, when asked**: a **schedule-style markdown table** — `Size (mm) | Qty` (add more
  columns like Total Length when relevant) — one row per distinct size, **sorted by size ascending**
  (smallest to largest, e.g. 75x75, 100x100, 125x125 ... 1524x470), never by qty or by another column.
  Not an inline compact list either way — it should read like a Revit schedule, not a sentence.
- **A specific/narrowed value, not a full breakdown** ("the 300x300 VCDs", "the ones on Level 2", "the
  ones with Mark X") — this is a request for the actual **items**, not just a count or a size table. List
  each matching element with its **Family/Type (or Category) AND Element ID** — a small table
  (`Id | Family and Type | ...`) rather than a bare count. The reason: a narrowed-down request like this
  is almost always the setup for a next step ("now select those", "now move them", "what's their length")
  — the Element IDs are what make that next step possible without re-filtering from scratch. Compose
  [`filters/by-property/filter-by-category-and-numeric-param.cs`](../scripts/filters/by-property/filter-by-category-and-numeric-param.cs)
  (or whichever filter matches) with
  [`actions/reporting/action-report-parameters.cs`](../scripts/actions/reporting/action-report-parameters.cs) for this —
  `action-count-and-report.cs` is for a bare count/aggregate breakdown, not this case.

- **Substantive work** (a build/fix/check, or anything touching the live model — not a quick count/size
  question) — close with this **7-point Final Report**:
  1. What I understood the request to be
  2. What already existed (in the project, or found online) that got reused
  3. Whether this was a split / update / create, and why
  4. What was live-tested, and the real result
  5. What still needs the user's decision before it's used for real work
  6. What got saved/documented so next time is faster
  7. Any good next step toward the bigger goal, flagged without being asked
  Keep it plain-language and only as long as the work warrants — a small fix can answer all 7 points in
  a few lines; don't pad it out. This is separate from (and doesn't replace) the bare-number/table rules
  above, which are for quick queries, not finished pieces of work.

- **Anything with two or more numbers worth comparing gets a picture, unasked** (his rule, 2026-08-14:
  *"i need always need visualization... if vishalization needdd it need to come"*) — and that picture is
  **an inline chart in the chat reply, next to the schedule table**. This sits on top of the rules above,
  it does not replace them: **a bare count is still a bare number, not a one-bar chart.**
- **A published page is only ever made when he asks for one** (corrected the same day:
  *"normaly i need to come in the chat... if i ask the artifects its need to come like this you make
  html file"*). His word for it is **"artifact"**. Unasked, a page is slower for him and buries the
  answer behind a link. The workflow, the template and the hard rules (never invent a figure, a failed
  read shows as "not read" and never as 0) are in
  [`skills/ajtools-visual-report/SKILL.md`](../skills/ajtools-visual-report/SKILL.md).

Update this file directly whenever the user asks for a different reply format — it's meant to change often
and stay small.

### Log
- Seed entry — quantity answers should be just the number, nothing more, by default; size/breakdown
  answers should be a schedule-style table (Size | Qty), sorted by size ascending, not an inline list and
  not sorted by quantity.
- Seed entry — a standing 7-point "Final Report" format applies to any substantive request (build/fix/
  check/live-model change), on top of (not instead of) the quick-answer rules above.
- 2026-08-14 — visualization is now a standing part of the reply, not a request: two or more comparable
  numbers get a chart or a dashboard automatically. Added the day he said *"always this need to come"*
  after showing two Revit dashboards he wanted matched.
