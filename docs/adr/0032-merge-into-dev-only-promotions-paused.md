# ADR-0032: Merge into `dev` only; promotions and deploys are paused

- **Status:** Agreed. Davit decided it on 2026-11-25 (TREK-98: "until he decides otherwise, work is merged into dev only"). Restarting on his request and the core checks (diff `dev..main`, new migrations, fresh backup) are his too. Claude decided the rest under ADR-0023, including the open-PR check before every merge.
- **Date:** 2026-11-25
- **Deciders:** Davit, Claude
- **Supersedes:** the promotion, deploy and release parts of ADR-0023: Claude "promoting `dev` → `staging` → `main`, deploying to the home host, tagging releases", and the rules for promotions, deploying `main` and tagging desktop releases. The rest of ADR-0023 stays in force: Claude implements, verifies, reviews, merges TRK PRs into `dev` and keeps the task list current, and the "still never" and "still Davit's" lists are unchanged.

## Context

Under ADR-0023, Claude took every merged change through `staging` and `main` and deployed `main` to the home host. On 2026-11-25 Davit decided to stop at `dev` until he says otherwise.

The state when promotions stopped:
- **`dev`, `staging` and `main` all hold the TREK-82 graphify removal:** #72 into `dev`, #73 into `staging`, #74 into `main` (`7c4ecd7`).
- **#74 should not have been merged.** Davit closed it at 12:35 UTC to keep `main` where it was (`166a23d`). Claude merged it at 12:37 without checking that it was still open, and `gh pr merge` does not refuse a closed PR. `gh` acts as Davit, so GitHub shows both the close and the merge under his account.
- **#74 changes no app code:** only CI, `.claude/`, `tools/graphify/` and docs. Davit chose to keep `main` as it is rather than revert it.
- **The home host:** its checkout was moved to `7c4ecd7` right after the merge, but no `up` was run and no backup taken. The running images are still the `166a23d` build from 2026-11-24.
- **Desktop:** no release has been tagged. Installed desktop apps get no updates and use the home host as their server.

## Decision

Claude merges TRK PRs into `dev` and stops there. It does not open or merge `dev` → `staging` or `staging` → `main` PRs, deploy `main` to the home host, or tag desktop releases, until Davit asks for promotions to restart.

## Consequences

- **Rules:**
  - Feature PRs are merged as ADR-0023 says: `/verify` and `code-reviewer` clean on the exact tree, the evidence comment posted, every required check green, squash into `dev` pinned with `--match-head-commit`.
  - **New:** right before every `gh pr merge`, Claude checks `gh pr view <n> --json state` and merges only an `OPEN` PR. A PR that Davit closed is a no, so Claude stops and asks.
  - A Trekky item is Done once its PR is merged into `dev`. Until Claude has a bot account (TREK-49), Davit moves it on Claude's word.
  - Production fixes are no exception. If something on the home host needs a fix, Claude merges it into `dev` and asks Davit whether to promote it.
- **Promoting again:**
  - **One-off:** a single promotion Davit asks for (for example a fix) needs no new ADR, but it runs the checks below.
  - **Standing:** a restart of promotions for good is recorded in a later ADR that supersedes this one.
- **Checks before the first promotion and deploy:**
  - review `git log --oneline Trekky/main..Trekky/dev` and `git diff --stat Trekky/main Trekky/dev`, and list every TREK item they contain in the promotion PR;
  - list the new migrations with `git diff --name-only Trekky/main Trekky/dev -- '*/Migrations/*'`, and stop for Davit if one drops, renames or rewrites data;
  - take a fresh backup, then deploy and check as in `deploy/README.md` (`migrate` exited 0, the health endpoint answers);
  - before the first desktop tag, check that the secrets `TAURI_SIGNING_PRIVATE_KEY` and `TAURI_SIGNING_PRIVATE_KEY_PASSWORD` and the variable `TREKKY_DEFAULT_SERVER_URL` are set, that the version was bumped, and that `node scripts/release.mts` passes (`desktop/RELEASING.md`).
- **Harder:**
  - The home host runs `main`, and installed desktop apps use it as their server. Work merged into `dev` isn't visible there until promotions restart, even though its Trekky item says Done.
  - The gap between `dev` and `main` grows with every merge, so the first promotion carries more change and more migrations at once.
- **Easier:** production stays unchanged while Davit decides how to release. The hosted Trekky that tracks this work keeps running as it is.

## Alternatives considered

- **Keep promoting, but don't deploy:** `main` would drift from what the home host runs. Deploying later would then mean a `main` that nobody has checked.
- **Promote to `staging` only:** `deploy/README.md` notes there is no staging stack. Moving `staging` would add no checks, only one more branch to keep in step.
- **Revert `main` to `166a23d`:** Davit chose to keep it. The commit has no app change, and a revert PR into `main` would be one more promotion.
