---
name: trk
description: Start, implement and finish one Trekky TRK task end to end — branch, implement, test, verify, review, commit, push and open the PR. Use when Davit says "do TRK-n", "start TRK-n" or "next TRK".
---

# Work one TRK

## 1. Pick the TRK

- Use the key Davit gives. "Next TRK" = the lowest unchecked TRK on the Notion task list. Never skip ahead or invent a key.
- Restate the TRK's goal and acceptance criteria in two or three lines. If it is unclear or needs a decision, ask before writing code.
- Check whether it can build and run on its own. If it depends on a later TRK to be buildable, propose combining them and wait for Davit's yes.

## 2. Branch

```bash
git fetch Trekky
git switch --no-track -c TRK-<n>-<short-description> Trekky/dev
```

Branch from `Trekky/dev`, not local `dev`: Davit merges on GitHub, so local `dev` falls behind.

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
- Davit reviews and merges. Once he confirms it is merged, tick the TRK checkbox on the Notion task list.
