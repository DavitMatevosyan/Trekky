# ADR-0012: Keep UI test screenshots on a `ui-evidence` branch that Claude may push

- **Status:** Agreed
- **Date:** 2026-10-30
- **Deciders:** Davit, Claude

## Context

Claude now validates each TRK in a real browser through the Playwright MCP server and posts a pass/fail report as a PR comment. Screenshots are the proof, but GitHub has no public API for attaching images to a comment: drag-and-drop on github.com uses an internal upload endpoint, and neither `gh` nor the REST API can reach it. Gists reject binary files. Every test run must also end clean, with no screenshots, test data or servers left behind, so the images can't simply stay on Davit's machine. CLAUDE.md says Claude never pushes. The repo is public, so anything pushed to it is public.

## Decision

Screenshots from a test run go on one orphan branch, `ui-evidence`, which holds only evidence and is never merged. As the only exception to "never push", Claude may commit to that branch and push it fast-forward, then embed the images in the PR comment with commit-pinned URLs.

## Rules

- **Layout:** `TRK-<n>/<yyyy-mm-dd>/<nn>-<what-it-shows>.png`, plus the report as `report.md` in the same folder.
- **How:** use a temporary `git worktree` of `ui-evidence` outside the repo. Copy the images in, commit `TRK-<n>: UI evidence <date>`, run `git push Trekky ui-evidence` (the remote is named `Trekky`), and remove the worktree. The working tree and the TRK branch are never touched. After the push, delete the local screenshots (see the clean-run rule).
- **Links:** `https://github.com/DavitMatevosyan/Trekky/blob/<commit-sha>/<path>?raw=true`. Pin them to the commit SHA, not the branch name, so a comment keeps showing the images that were taken for it.
- **Content:** only synthetic data (`uitest-*@example.com`, test orgs) on a local stack. No tokens, cookies, real emails, devtools panels or anything outside the browser viewport. Check every image before pushing, because it becomes public.
- **Size:** CSS-scale PNGs of the viewport. Use full-page shots only when the check needs them (for example, a long list).
- **Limits:** no force push, no other branch, no deleting the branch. Pruning old evidence rewrites history, so only Davit does it.

## Consequences

- Easier: PR comments show the screenshots inline; evidence is versioned per TRK and run, and the local machine still ends every run clean.
- Harder: every clone grows by the images, since a normal `git clone` fetches all branches (`--single-branch` avoids it); every image is public; one more thing for Claude to get right, so the `code-reviewer` agent should flag a push to any other branch.
- CI is unaffected: `ci.yml` runs only on `dev`, `staging` and `main`.
- Follow-ups once Agreed: add the exception to CLAUDE.md ("How we work"), and add the evidence step to the UI-validation routine (a skill or the `/trk` finish step).

## Alternatives considered

- Drag-and-drop by Davit: works, but adds a manual step to every run and means keeping the images locally until he's done.
- Private claude.ai page with embedded images, linked from the PR: no repo growth, but other PR readers can't see it, and the proof lives outside GitHub.
- GitHub release assets or a separate evidence repo: also pushes, with more setup and less obvious ties to the TRK.
- Committing screenshots on the TRK branch: pollutes `dev` through the squash merge.
