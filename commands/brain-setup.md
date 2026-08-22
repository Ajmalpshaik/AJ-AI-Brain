---
description: Build the AJ AI Brain's machinery on this machine — relay dependencies, the search environment and the search index. Run once after installing the plugin, or any time the SessionStart check says the Brain is not set up. Safe to re-run.
---

# Set up the Brain on this machine

Installing this plugin brought the skills, the knowledge notes and the C# fragments. It did **not**
bring the machinery those depend on: the relay's npm dependencies, the Python search environment and
the vector index are all gitignored on purpose, because an index copied from someone else's machine is
stale the moment it arrives.

This command builds them.

## Do this

Run it and let it stream:

```
node "${CLAUDE_PLUGIN_ROOT}/tools/brain-setup.mjs"
```

If the plugin variable is not available in your shell, run it from the Brain folder instead:

```
node tools/brain-setup.mjs
```

It takes a few minutes. It is safe to re-run — every step checks whether it is already done and skips
it, so an interrupted run resumes from where it stopped.

## Then report honestly

Do not say it worked because the command exited 0. Read the real output:

1. It must end with `=== Setup complete ===`. If it ends with `FAILED:`, the run stopped there and
   nothing after it happened — say which step failed and quote the error.
2. Run `node "${CLAUDE_PLUGIN_ROOT}/tools/brain-status.mjs"` and read the **Derived layers** block.
3. Prove the search actually answers, rather than assuming:

```
node "${CLAUDE_PLUGIN_ROOT}/tools/../semantic-index/brain_search_hybrid.py" "how do I stop ducts overlapping the ceiling"
```

   (or `semantic-index\ask-brain-hybrid.cmd "..."` from the Brain folder). It should return in under
   four seconds, print no `STALE INDEX` banner, and put genuinely relevant files at the top. A fast
   empty answer is still a failure.

## Two things this command deliberately does not do

**The knowledge graph and the Obsidian vault.** Those need an AI session, not a script — graphify's
document pass dispatches subagents, which a shell script cannot do. Follow
[`skills/brain-update-layers/SKILL.md`](../skills/brain-update-layers/SKILL.md) when you want them.
Everything else works without them: the search, every skill and every fragment.

**Connecting to Revit.** Nothing here can reach a model. That needs a Revit-side add-in hosting the
bridge, which is a separate compiled codebase and is not in this folder — see
[`SETUP.md`](../SETUP.md) section 3, and prove it with the ping in section 4.

Say both of these out loud when reporting, so nobody reads "setup complete" as "it can drive Revit
now."
