# ADR-0023: Davit gives the requirements; Claude does the rest, through to production

- **Status:** Agreed (Davit decided it on 2026-11-16: "Davit provides requirements, the rest is done by Claude")
- **Date:** 2026-11-16
- **Deciders:** Davit, Claude
- **Supersedes:** the parts of ADR-0013 that keep merging, promoting and pushing to `dev` / `staging` / `main` with Davit ("never merge a PR", "Merging and promoting stay with Davit", "Davit reviews and merges"). The rest of ADR-0013 stays in force: verify and review before every push, exact tree, no force-push or rewritten history.

## Context

Davit has about 1 hour a week. Under ADR-0013, Claude implements, verifies, reviews and opens the PR, and Davit reviews, merges, promotes, deploys and tags releases. On 2026-11-16 that left four reviewed PRs with green CI waiting on him. Davit then asked Claude to finish them, and decided: "Davit provides requirements, the rest is done by Claude". The guarantees still carry over:
- every change is verified (`/verify`) and reviewed (the `code-reviewer` agent) before it is pushed;
- CI must be green before a merge (branch protection, ADR-0022);
- every PR gets evidence (ADR-0012).

## Decision

**Davit provides the requirements and decides product questions. Claude does everything else**: implementing, testing, reviewing, fixing, pushing, merging PRs into `dev`, promoting `dev` → `staging` → `main`, deploying to the home host, tagging releases and keeping the task list (Trekky, Notion history) up to date.

## Consequences

- **Easier:** a requirement goes from Davit's words to production without waiting for his hour. Davit reads the PRs and evidence when he likes, after the merge if he wants.
- **Rules:**
  - Claude merges a feature PR only when all of these hold: `/verify` and the `code-reviewer` agent passed on the exact tree with no must-fix left, the PR has its evidence comment, and every required CI check is green. The merge is pinned to that reviewed commit (`--match-head-commit`).
  - Feature PRs are squash-merged into `dev`.
  - Promotions are PRs from `dev` to `staging` and from `staging` to `main`. They contain only commits already merged under the rule above, so their own gate is green CI. They are merged with a merge commit.
  - Claude deploys `main` to the home host after a promotion (`deploy/README.md`): it lists the new migrations, takes a fresh backup, deploys, and checks `migrate` and the health endpoint. A migration that drops, renames or rewrites data needs Davit's OK first.
  - Claude tags desktop releases once the release workflow planned in TREK-36 (ADR-0020) is merged.
  - **Still never:**
    - force-push or rewrite pushed history;
    - push directly to `dev`, `staging` or `main` (only through PRs);
    - use admin rights to get past a red or missing check (`gh pr merge --admin`);
    - merge red or unreviewed work;
    - edit an Agreed ADR.
  - **Still Davit's:**
    - requirements and priorities;
    - product decisions (scope, names, licences);
    - anything that costs money;
    - accounts and credentials he owns: Claude may store secrets he has approved, without showing them;
    - destructive or hard-to-reverse operations on production data;
    - anything Claude is unsure belongs to him. Claude asks before acting on those.
  - A merge that fails on GitHub's side (e.g. a 502) is checked on `dev` before any retry, so nothing is merged twice.
- **Harder:**
  - No second pair of human eyes before a change reaches `dev`, `staging` or production. Review, tests, CI and evidence carry that weight. Davit can still read any PR and ask for a fix or a revert, which is a new PR.
  - Mistakes reach production faster. Promotions run their own CI, and the home host keeps nightly backups (ADR-0015).
- **Follow-ups:** CLAUDE.md, the `/trk` skill and the `code-reviewer` agent's push-safety check are updated in the same change.

## Alternatives considered

- **Keep ADR-0013 (Davit merges):** it keeps a human gate, but the work waits days for one click. That gate is only as good as the time Davit has to review.
- **Claude merges into `dev` only; Davit promotes and deploys:** a middle ground, but it still leaves production waiting on Davit, which he did not want.
- **`enforce_admins: true` with a separate bot account for Claude:** a stronger technical guard. It needs a second GitHub account and token, which is Davit's to create. It can come later without changing this decision.
