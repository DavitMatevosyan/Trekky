# ADR-0022: dev, staging and main are protected on GitHub

- **Status:** Proposed
- **Date:** 2026-11-16
- **Deciders:** Davit, Claude
- **Builds on:** ADR-0013 (Claude verifies, reviews and pushes).

## Context

The team rules (Notion "Agreements": reviews, branching) require every change to go through a PR with green CI, and ADR-0013 forbids pushing to `dev`, `staging` or `main` directly. Branch protection needs a public repository on GitHub's free plan, so until TRK-22 the rules were only agreed, not enforced. The repository is public now. `staging` was created on 2026-11-16 by the first promotion, which joined `dev`'s history onto `main`'s initial commit. During that session one merge also landed twice on `dev`, an empty duplicate after a 502, which shows how easily a direct action can go wrong.

## Decision

`dev`, `staging` and `main` are **protected**. Changes go **only through pull requests**, with the CI checks **Backend (build + test)**, **Frontend (lint + test + build)**, **E2E (Playwright)** and **Docker images (build)** green. **Force-pushes and deletion are blocked for everyone.** Admins are not held to the PR and check rules (`enforce_admins: false`), so Davit can fix an emergency directly.

## Consequences

- **Easier:**
  - No unreviewed or red change reaches the three branches from a non-admin account.
  - Nobody, admins included, can force-push away history or delete one of them.
- **Rules:**
  - No approving review is required, because GitHub does not let anyone approve their own PR and Davit is the only maintainer. The PR itself, its CI and the posted review are the gate.
  - The checks are required by name, so renaming a CI job means updating the protection.
  - The Windows MSI job planned in TREK-36 (ADR-0020) must not be made required, because it runs only when `desktop/` or `frontend/` changes. A required check that never runs would block every other PR.
  - Claude works through `gh` with Davit's admin account, so GitHub does not stop Claude pushing to these branches directly. The same account could also merge a PR whose checks are red (`gh pr merge --admin`). Both still rest on ADR-0013: Claude never pushes to these branches, never merges and never uses admin rights to get past a check; merging stays with Davit. Setting `enforce_admins: true` would enforce all of this in GitHub too, at the cost of Davit's emergency override.
  - Branches do not have to be up to date with their base before merging (`strict: false`). That keeps stacked PRs mergeable after a squash, and CI runs again on the push to `dev` anyway.
  - Promotions (`dev` → `staging` → `main`) are PRs with a normal merge commit, not a squash. Their history is related since the first promotion.
- **Harder:** a hotfix also needs a PR with green CI (about 2 to 3 minutes). Admin override exists, but it skips the safety.
- **Follow-ups:** none.

## Alternatives considered

- **Protect only `main` and `staging`:** quicker fixes on `dev`, but `dev` is where every change lands, so it is the one that most needs green CI.
- **Require one approving review:** impossible with a single maintainer whose PRs are opened under his own account.
- **Rulesets instead of classic branch protection:** more flexible (for example per-path rules), but more settings to keep track of for three branches. Classic protection does what is needed.
