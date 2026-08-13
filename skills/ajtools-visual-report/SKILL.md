---
name: ajtools-visual-report
description: Turn any Revit model reading into a visual. The DEFAULT is a chart rendered inline in the chat reply — that is what he wants normally, without asking. A published dashboard page (an "artifact" in his words, built from dashboard-template.html) is ONLY produced when he asks for one. Use this whenever an answer contains numbers worth comparing (counts per category or per size or per width, lengths, areas, airflow, warnings, coverage, model-health figures) — and use it WITHOUT being asked, because he set it as a standing rule on 2026-08-14: "always need visualization... if visualization needed it need to come". Also use when he says "make it artifact", "make it a dashboard", "give me the link", "give me the graph". Do NOT use it for a bare count question (one number, one line — see knowledge/reply-style.md), and note that his word "visualization" here means a chart or dashboard of the model's NUMBERS, never a 3D render.
---

# AJ Tools — Visual report

His standing rule, in his own words (2026-08-14):
*"i need always need visualization... if vishalization needdd it need to come."*
So this is not a command he has to type. **When an answer has numbers worth comparing, the picture comes
with it, automatically.** He should never have to ask twice.

He set it after showing two dashboards he wanted matched — *Revit Wall Types Analysis Dashboard* and
*Revit Model Warnings Dashboard*.

## The default is the chat, not a page

Corrected the same day, after the first two reports came back as published pages he had not asked for
(2026-08-14): *"normaly i need to come in the chat... if i ask the artifects its need to come like this
you make html file."*

| He said | Give him |
|---|---|
| *(nothing — just asked for the numbers)* | **Inline chart in the reply.** This is the normal case. |
| "make it artifact" · "make it a dashboard" · "give me the link" · "I want to send it" | **Published page**, built from [`dashboard-template.html`](dashboard-template.html) |

A page is for sending to someone else. If nobody else is going to see it, it is a chart in the chat.
**Never publish one unasked** — it is slower for him and it buries the answer behind a link.

## When a visual applies at all

| The answer is… | Give him |
|---|---|
| One number ("how many VCDs") | **Just the number.** No chart. [`reply-style.md`](../../knowledge/reply-style.md) wins. |
| A breakdown — per size, width, category, level, system, room | Schedule table **+ inline chart** |
| A whole-model reading — health, warnings, takeoff, audit | Key numbers + table **+ inline chart** (page only on request) |
| A list of specific items he will act on next | Table with Element IDs, no chart — the IDs are the point |

Rule of thumb: **two or more numbers that invite comparison → draw it.** One number → don't.

## Sorting

A size/width/diameter axis sorts **smallest to largest**, in both the table and the chart, per
[`reply-style.md`](../../knowledge/reply-style.md) — never by quantity, and never with the table in one
order and the chart in another. A non-size axis (category, system, level) sorts by value, largest first.

## Pipeline

1. **Read** with a PROVEN fragment or a native tool — never write new C# for this.
2. **Shape** the numbers yourself: group, sum, sort, convert feet→mm→m, compute shares.
3. **Render inline** — the chart appears in the reply, with the schedule table beside it.
4. **Only if he asked for a page**: fill the template, publish, hand over the link.
5. **Say where every figure came from** — which tools, which model, which date.

## The third shape — the model itself

Sometimes the real answer is not a chart at all: colour by parameter value, isolate, or draw coverage
circles in the view. Prefer this when the question is *where*, not *how many*.

**When the model gets coloured AND a chart follows, the chart must wear the model's colours** — he asked
for exactly that on 2026-08-14 ("color by size and give me same like this"), and a chart in different
colours to the screen makes him translate between two legends for no reason. To make that possible:
compose [`action-color-by-group.cs`](../../scripts/actions/color-graphics/action-color-by-group.cs) in
**`palette` mode, not `random`/`pastel`/`neon`** — those pick a random starting hue each run, so the
colours cannot be known outside Revit — order the groups deterministically (smallest→largest for a size
axis), and **have the script echo each group's RGB in its output**. Then reuse those exact RGB values as
the bar colours. This is the one case where the chart palette is dictated by the model rather than chosen.
Verified working: 6 duct sizes, colours read back out of the view before charting.

**And when a colour is reported, show the colour — not its numbers.** He asked for it as a table,
*"size in one column and another column what color is there"* (2026-08-14). A markdown table cannot
render a colour, so `RGB(230,25,75)` is the wrong answer even though it is accurate: he has to imagine
it. Use a widget table with a real swatch per row — swatch, colour name, RGB — so the legend can be held
next to the screen and matched by eye. Same rule for any future group colouring: system type, level,
insulation status.

## What feeds which report

| Report | Read it with | Status |
|---|---|---|
| Duct/pipe takeoff by size or width | `list_elements` + `report_parameters` (native) | **run end-to-end 2026-08-14** |
| Model warnings | [`context-all-warnings.cs`](../../scripts/context/context-all-warnings.cs) | fragment proven, report not built yet |
| Wall types / any category breakdown | [`action-count-by-group.cs`](../../scripts/actions/reporting/action-count-by-group.cs) | fragment proven, report not built yet |
| Model health — file size, warnings, families | [`model-health-audit.cs`](../../scripts/recipes/model-health-audit.cs) | fragment proven, report not built yet |
| Space airflow — design vs actual | [`action-report-space-airflow.cs`](../../scripts/actions/reporting/action-report-space-airflow.cs) | **fragment unproven** — run one Space first |
| Device coverage per room | [`action-report-coverage.cs`](../../scripts/actions/reporting/action-report-coverage.cs) | fragment proven, report not built yet |
| Anything already in a Revit schedule | [`action-export-schedule-to-csv.cs`](../../scripts/actions/sheets-views/action-export-schedule-to-csv.cs) | fragment proven |

## Hard rules — these are what make it trustworthy

- **Never invent, estimate or fill a number.** Every figure came off the model this session.
- **A reading that failed shows as "not read", never as 0.** A wrong zero on a takeoff is worse than a
  visible gap. This happened on the first one: a pipe count was lost to a dropped bridge connection and
  the tile said so.
- **Name the model, the Revit version and the date.** A report with no provenance is indistinguishable
  from a mock-up.
- **Say when one grouping hides another.** Grouping ducts by width puts 400×400 and 400×500 in one row
  and changes which group is biggest — so the table carries a "sizes inside it" column. Same elements,
  different question, different answer.
- **Element IDs stay available** — as a range in the footer, or in the table when he will act on them.
- **State the units.** Revit's API is feet; he reads mm. Convert explicitly and label it.
- **Figures from an earlier read in the same session are fine** — say so plainly rather than re-reading,
  especially when the bridge is off-limits (see [`core.md`](../../knowledge/live-model/core.md)).

## The look, when a page IS asked for

[`dashboard-template.html`](dashboard-template.html) carries it: header with provenance, a row of
headline tiles, bar rows, a schedule table, chips, and a footer naming the tools. Cool steel-and-ink
palette, monospace for every figure, works in light and dark. Copy it, fill it, don't redesign it each
time — consistency is what makes them read as one set.
