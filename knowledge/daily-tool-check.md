# The daily four-tool check — what it can prove, and where it has to be run

A scheduled daily routine checks four things: **AJ Tools (the live bridge), the vector search, Graphify
(the knowledge graph), and the Obsidian vault.** Half of that job can only be done on the Windows PC.
This note says which half, so nobody re-derives it — and so a run that comes back "cannot see them" is
read as the environment reporting honestly, not as the tools being broken.

Written 2026-08-25, from a run that measured all of the below rather than assuming it.

## The split: committed code vs machine-local state

The Brain's three derived layers — **vector index, knowledge graph, Obsidian vault** — are all
gitignored on purpose. They are *derived* from the source files, they are large, and they are specific
to the machine that built them. So a git checkout carries **their code and none of their state.**

The bridge is the same shape for a different reason: it is an MCP relay that starts a Windows-side
listener and talks to an open Revit session. A Linux container has no Revit to talk to and no relay
registered.

That gives a clean rule, and it is the only thing worth remembering here:

> **Currency of the four tools is a question about a machine, not about a repository.**
> It is answerable where the Brain folder lives. Anywhere else, the honest answer is "no state present",
> and `tools/brain-status.mjs` already prints exactly that under *Derived layers*.

## What a container session CAN check, and should

These are real checks and they are worth running daily — they catch the drift that *does* travel in git:

| Check | Command | What it proves |
|---|---|---|
| Repo consistency | `node tools/verify-consistency.mjs` | Every count, link, cross-reference, header status and encoding claim in the repo still matches disk |
| True library state | `node tools/brain-status.mjs` | Skill/fragment/native-tool counts, how much is proven against a real model, open items |
| Peer sessions | `git fetch origin main` then `git rev-list --left-right --count origin/main...HEAD` | Whether another session has pushed work this checkout has not seen — run it at the **start**, not at push time |
| Derived-layer wiring | Read `.claude/settings.json` → `Stop` | That the three rebuild hooks are still registered, in their three phases |
| Release currency | `.claude-plugin/plugin.json` → `version` | Whether pushed work would actually reach an installed copy |

Measured 2026-08-25: all five came back clean, and the derived layers reported absent — which is the
expected result in a checkout, not a fault.

## What it CANNOT check, and must not claim

- **Is the bridge running** — needs Revit open and the relay registered. No `ping` tool exists in a
  container session at all; it is not that the call fails, it is that the tool is not there.
- **Is the vector index current** — `tools/brain-setup.mjs --check` in a fresh checkout reports the
  search environment and index simply missing. That is a statement about the checkout, not about the PC.
- **Graphify data integrity** — [`obsidian-export.mjs`](../tools/obsidian-export.mjs) looks for
  `graphify-out/graph.json` and there is none; the `graphify` binary is not installed either.
  [`graph-rebuild.mjs`](../tools/graph-rebuild.mjs) is gated on a code change and exits silently with
  nothing to do.
- **Obsidian vault status** — the vault is a pure function of `graph.json`. No graph, no vault, nothing
  to compare.

None of those four is a failure. Each tool reported "not set up on this machine" and exited 0, which is
the behaviour they were built to have.

## Running the real check — on the PC, in this order

1. `git pull` first, so the checks read what peer sessions have pushed.
2. `node tools/brain-status.mjs` — the *Derived layers* block names any layer that is missing or stale.
3. `node tools/brain-setup.mjs --check` — says whether the search environment and index are built.
4. `node tools/obsidian-export.mjs --check` — says what it *would* export, changing nothing.
5. Ask the search a question you know the answer to. A `STALE INDEX` banner is the index telling you the
   files have moved on since it was built; `semantic-index\index-brain.cmd` rebuilds only what changed.
6. Only if a Revit version changed: `tools\check-scripts.cmd`, and let it finish — see
   [`revit-version-compatibility.md`](revit-version-compatibility.md).

Steps 2–5 are seconds. The graph's markdown side is the one part no hook can do on its own, because it
needs a model call — that is what
[`skills/brain-update-layers/SKILL.md`](../skills/brain-update-layers/SKILL.md) is for.

## The rule this is an instance of

The handover for 2026-08-24 recorded four sessions burning time on a block that was real in a container
and absent on the PC, because it had been written down as a fact about *the operation* instead of a fact
about *the environment*. Same trap, same fix:

> **An environment-specific limit belongs in a sentence that names the environment — and says where to
> go instead.**

"The vector index cannot be verified" invites someone to go looking for a broken index. "The vector
index cannot be verified *from a container session, because it is gitignored machine state* — check it
on the PC with `brain-status`" ends the question at the first reading.
