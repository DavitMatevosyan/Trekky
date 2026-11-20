# TREK-74 manual verification — 2026-11-20

Tree: `5c0e381` (PR #63). Stack: the PR's API on :5091 with its own database, Vite on :5174, and a throwaway fake
GitHub on :5099 that stands in for github.com and api.github.com (the real app is not registered yet). Driven by a
Playwright script as a new user `uitest-trek74-…@example.com`.

| # | Step | Result |
| --- | --- | --- |
| 1 | Sign up, create project HB, open Settings: GitHub section, nothing connected | pass |
| 2 | Connect GitHub → (fake) install page → `/settings/github/callback` with code, installation_id and the tab's state → connected at once, announced | pass |
| 3 | HB → Settings: repositories of the installation offered; docs/ and dev prefilled | pass |
| 4 | Branch `release` (not on GitHub) → 422, shown on the Branch field | pass |
| 5 | Branch `dev` → saved; summary with Open on GitHub | pass |
| 6 | Disconnect asks first (focus on Cancel) | pass |
| 7 | After disconnecting, the project has no docs repository | pass |

The fake's request log showed the code exchange, `GET /user/installations`, the admin check
`GET /user/installations/{id}/repositories` and the token revoke `DELETE /applications/{client_id}/token`.

Cleanup: test user, organization and their data removed with the run's database; API, Vite, fake GitHub and browser stopped.
