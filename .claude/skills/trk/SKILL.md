---
name: trk
description: Start, implement and finish one Trekky TRK task end to end — branch, implement, test, verify, review, commit. Use when Davit says "do TRK-n", "start TRK-n" or "next TRK".
---

# Work one TRK

## 1. Pick the TRK

- Use the key Davit gives. "Next TRK" = the lowest unchecked TRK on the Notion task list. Never skip ahead or invent a key.
- Restate the TRK's goal and acceptance criteria in two or three lines. If it is unclear or needs a decision, ask before writing code.
- Check whether it can build and run on its own. If it depends on a later TRK to be buildable, propose combining them and wait for Davit's yes.

## 2. Branch

```bash
git switch dev
git switch -c TRK-<n>-<short-description>
```

## 3. Implement

- Follow CLAUDE.md: module boundaries, `org_id` + RLS, activity events via outbox, `/api/v1`, problem details.
- Smallest change that meets the TRK. Nothing from later versions.
- Write or update tests alongside the code (use the `test-writer` agent for larger sets).
- A decision that sets a rule → write an ADR with `/adr`.

## 4. Verify and review

- Run `/verify`. Fix every failure.
- Run the `code-reviewer` agent on the diff. Fix every "must fix" finding.

## 5. Commit

```bash
git add -A
git commit -m "TRK-<n>: <what changed and why>" -m "<body: details, trade-offs, follow-ups>"
```

- A fix for this TRK found later stays under the same key.
- Do **not** push. Tell Davit: branch name, what changed, verification results (or exactly what could not be run), and anything left for him to check by hand.
- Once Davit confirms it is pushed and merged, tick the TRK checkbox on the Notion task list.
