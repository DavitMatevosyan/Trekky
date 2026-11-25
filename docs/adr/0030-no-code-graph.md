# ADR-0030: No code graph: graphify is removed

- **Status:** Agreed. Davit decided on 2026-11-25 to drop graphify ("okay if that is the case drop it") after the TREK-82 measurement showed no saving.
- **Date:** 2026-11-25
- **Deciders:** Davit, Claude
- **Supersedes:** ADR-0029

## Context

TREK-82 committed graphify and built its code graph in CI on a `graphify` branch (ADR-0029). The TREK-82 criterion was to keep graphify only if it helps. The measurement in PR #66 asked three codebase questions in headless sessions, with and without the graph:
- With only the CLAUDE.md rule, Claude never called graphify and searched as usual.
- With the graph required, one question got cheaper and two cost the same or more.
- All answers were correct either way.

At about 590 files with one folder per module, grep finds the code in a handful of searches (one to seven calls in the measurement). The setup also added weight to every session: a workflow, four session hooks (a fetch at start plus three graph nudges), a CLAUDE.md section with rules to ignore those nudges, and a crash in `graphify section` (PR #69, closed unmerged when graphify was removed).

## Decision

Trekky has no code graph. The `Code graph` workflow, `tools/graphify/`, `.graphifyignore`, the stock `/graphify` skill, the graphify hooks (`.claude/settings.json`) and the CLAUDE.md graphify section are removed. The `graphify` branch is deleted once this change is merged to `dev`, so no workflow run can recreate it. Claude searches the code with grep, glob and file reads.

## Consequences

- **Easier:** sessions start without a fetch and tool calls carry no graph nudges. CLAUDE.md is shorter, and there is no extra CI workflow or pinned Python tool to maintain.
- **Harder:** questions that span several modules take a few more searches. The docs layer that linked ADRs and READMEs to code (`tools/graphify/docs-layer.json`) is gone. It stays in git history at the TREK-82 merge commit `89b4050`.
- **Follow-ups:** after the merge, delete the `graphify` branch and close PR #69. Revisit a code graph if the repo grows a lot, or when the context layer (the product after Trekky Docs) needs one. ADR-0029 and PR #66 describe how to set it up again.

## Alternatives considered

- **Keep it, since it costs almost nothing to run:** the CI minutes are free, but the hooks, nudges and rules weigh on every session for no measured gain.
- **Keep only the CI build, without hooks or rules:** nobody would use a graph that sessions never fetch.
- **Require graphify before any search:** the measurement showed this wasn't cheaper, and its answers skipped checking the source lines.
