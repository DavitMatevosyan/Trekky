---
name: trk
description: Start, implement and finish one Trekky TRK task end to end — branch, implement, test, verify, review, commit, push and open the PR. Use when Davit says "do TRK-n", "start TRK-n" or "next TRK".
---

# Work one TRK

## 1. Pick the TRK

- Use the key Davit gives. "Next TRK" = the lowest unchecked TRK on the Notion task list, or after the TRK-21 switch the next item in To Do on the `TREK` board in Trekky (ADR-0016: its key `TREK-<n>` replaces `TRK-<n>` in the branch, commit and PR below). Never skip ahead or invent a key.
- Restate the TRK's goal and acceptance criteria in two or three lines. If it is unclear or needs a decision, ask before writing code.
- Check whether it can build and run on its own. If it depends on a later TRK to be buildable, propose combining them and wait for Davit's yes.

## 2. Branch

```bash
git fetch Trekky
git switch --no-track -c TRK-<n>-<short-description> Trekky/dev
```

Branch from `Trekky/dev`, not local `dev`: merges happen on GitHub, so local `dev` falls behind.

## 3. Implement

- Follow CLAUDE.md: module boundaries, `org_id` + RLS, activity events via outbox, `/api/v1`, problem details.
- Smallest change that meets the TRK. Nothing from later versions.
- Write or update tests alongside the code (use the `test-writer` agent for larger sets).
- A decision that sets a rule → write an ADR with `/adr`.

## 4. Verify and review

- Run `/verify`. Fix every failure.
- Run the `code-reviewer` agent on the diff. Fix every "must fix" finding.
- After any change, including fixes for review findings, run both again. The tree you commit must be the one that passed. This applies to every push, including later fix commits on an open PR.

## 5. Commit

```bash
git status                       # stage only the files that were reviewed
git add <reviewed files>
git commit -m "TRK-<n>: <what changed and why>" -m "<body: details, trade-offs, follow-ups>"
```

- A fix for this TRK found later stays under the same key.
- Never stage secrets, local config (for example `.env` or `.claude/settings.local.json`) or files the review did not see. Avoid `git add -A`.

## 6. Push and open the PR (ADR-0013)

- Push only if `/verify` passed and the `code-reviewer` agent reported no must-fix findings, both on the exact tree being committed. If anything could not be run, do **not** push; tell Davit what is missing.
- `git push -u Trekky TRK-<n>-<short-description>`: TRK branches (`TRK-<n>-…`) only. Never `dev`, `staging` or `main`, never `--force`, never rewrite commits already on the remote.
- Open the PR with `gh pr create --repo DavitMatevosyan/Trekky --base dev --head TRK-<n>-<short-description>`. If the PR already exists, the push updates it; use `gh pr edit` to update its description. The description lists what changed, the verification results, and anything Davit should check by hand. It ends with the Claude Code attribution line.
- Post the manual verification comment (ADR-0012), wait for every required check to be green, then check `gh pr view <PR> --json state`. If Davit closed the PR, don't merge: stop and ask (ADR-0032). If it is still `OPEN`, merge it yourself, pinned to the reviewed commit: `gh pr merge <PR> --repo DavitMatevosyan/Trekky --squash --match-head-commit <full 40-character sha of the reviewed commit> --subject "TREK-<n>: <title> (#<PR>)"` (ADR-0023). If GitHub answers with an error, run `git fetch Trekky` and check `git log Trekky/dev` (or `gh pr view <PR> --json state,mergeCommit`) before any retry. Never `--admin`.
- After the merge into `dev`, the `TREK` item is Done. Until TREK-49's bot account exists, tell Davit it is ready to move. For old `TRK` keys, tick the Notion checkbox. Then remove the worktree.

## 7. Stop at `dev` (ADR-0032)

- The TRK is finished once it is merged into `dev`. Don't open `dev` → `staging` or `staging` → `main` PRs, deploy `main` to the home host, or tag a desktop release. Promotions stay paused until Davit asks to restart them.
- In the final report, say that the work is on `dev` only: the home host runs `main`, and installed desktop apps use it as their server.
- When Davit restarts promotions, ADR-0032 lists what the first promotion must check: the `main..dev` log and diff, new migrations, a fresh backup, then deploy and check.
