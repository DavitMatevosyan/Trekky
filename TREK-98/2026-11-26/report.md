# TREK-98: merge into dev only (PR #76): manual verification

**Result:** pass

The change is process docs only: ADR-0032, ADR-0023's status line, CLAUDE.md, the /trk skill, the code-reviewer agent, README.md, deploy/README.md and desktop/RELEASING.md. No app code changed.

1. **PR checks:** all green (`01-pr76-checks.png`): CI (Backend, Frontend, E2E, Docker images) and Desktop (Windows MSI). The Draft release job is skipped on PRs.
2. **Local `/verify` on the committed tree:** backend build with 0 warnings, 733 tests pass. Frontend lint, 638 tests and build pass. Health on the branch's own API (:5091) returned `{"status":"ok"}`.
3. **Acceptance criteria:**
   - ADR-0032 supersedes the promotion, deploy and tag parts of ADR-0023.
   - It covers the restart (on Davit's request) and the checks then (`main..dev` diff, migrations, fresh backup).
   - It notes that the home host runs `main` and that items are Done on merge into `dev`.
   - CLAUDE.md is updated, and /trk §7 is now "Stop at dev".
4. **Leftover instructions:** `code-reviewer` found none left that tell Claude to promote, deploy `main` or tag.

After this PR merges into `dev`, nothing is promoted, under ADR-0032 itself.
