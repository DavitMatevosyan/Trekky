# ADR-0013: Claude verifies, reviews and pushes TRK branches

- **Status:** Agreed
- **Date:** 2026-10-30
- **Deciders:** Davit, Claude
- **Amends:** ADR-0012: its "only exception to never push" wording and its "no other branch" limit. The rest of ADR-0012 stays in force.

## Context

Until now Claude implemented and committed each TRK, and Davit verified it and pushed. Davit has about 1 hour a week, and that hour went into running builds and pushing branches instead of reviewing. Claude already runs `/verify` and the `code-reviewer` agent before every commit, so Davit repeating those steps adds little. The Notion agreement "every PR is reviewed by both Davit and Claude" still holds, and so do the protected-branch flow (`dev` → `staging` → `main` by merge only) and green CI before merge.

## Decision

Claude takes a TRK from implementation to a pushed branch and an open PR to `dev`: run `/verify`, review with the `code-reviewer` agent, fix every must-fix finding, commit, push the `TRK-<n>-…` branch, then open or update the PR. Davit reviews the PR and merges it.

## Rules

- **Push only when green.** `/verify` passed and the `code-reviewer` agent reported no must-fix findings, both on the exact tree being committed. After any change, including fixes for review findings, run both again. This applies to every push, including later fix commits on an open PR. If something couldn't be run (Docker down, for example), don't push. Report what's missing instead.
- **Only TRK branches (`TRK-<n>-…`) and `ui-evidence`.** Never push to `dev`, `staging` or `main`, and never merge a PR. Merging and promoting stay with Davit.
- **No history rewrites.** No force push, and no amending, squashing or rebasing commits that are already on the remote. A fix is a new commit under the same TRK key.
- **The PR is the hand-off.** The description lists what changed, the verification results and anything that needs a manual check. It ends with the Claude Code attribution line.

## Consequences

- Easier: Davit's hour goes into reviewing PRs, not running builds; every pushed commit has a recorded verify and review.
- Harder: a mistake reaches the remote before Davit sees it. It still can't reach `dev` without his merge, and CI runs on every PR into `dev`.
- Updates in the same change: CLAUDE.md ("How we work", "Agents and skills"). The `/trk` skill branches from `Trekky/dev`, re-runs checks after fixes, stages only reviewed files, and gains a new step 6, "Push and open the PR". The `code-reviewer` agent gains a push-safety check. The README is updated.

## Alternatives considered

- Keep "Davit pushes": safe, but it spends his limited time on mechanical steps that Claude already does.
- Let Claude merge into `dev` after green CI: faster, but it drops Davit's review, which the Notion "Reviews" agreement requires.
