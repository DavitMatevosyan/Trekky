# TRK-13 manual verification: pass

Date: 2026-11-05. Tree: `7b76e9c` on `TRK-13-history-api` (PR #17). Local stack: Docker Postgres 17, API on :5080, Vite on :5173, Playwright MCP (Chromium).

## Steps

1. Signed up `uitest-trk13@example.com` (org "UI Test TRK-13") through the web app.
2. Created project `HIS` and task HIS-1 "Comments" in the web app.
3. Edited the title to "Comments on items", then changed the status To Do → In Progress → In Review in the item page's status select (screenshot 01).
4. Took the tab's bearer token from an outgoing API request inside Playwright (it is never shown) and called the API. The real responses are rendered on pages (screenshots 02 to 05).

## Results

| Check | Expected | Result |
| --- | --- | --- |
| History lists every change, newest first | 4 events: StatusChanged ×2, Updated, Created | pass (02) |
| Actor display name on every event | `uitest-trk13` | pass (02) |
| Payload shows what changed | title from/to, status from/to | pass (02) |
| Cursor paging `limit=2` | 2 + 2 events, nextCursor then null | pass (03) |
| `createdBy` on item detail | id, displayName, email | pass (04) |
| Unknown item | 404 problem | pass (05) |
| `?limit=abc` | 400 problem+json (was 500) | pass (05) |
| `?cursor=nope` | 400 validation problem with `errors.cursor` | pass (05) |
| No token | 401 | pass (05) |

Not covered here (covered by integration tests): another organization's item returning 404.

Cleanup: test user, org and their data deleted; screenshots deleted locally; API, Vite and browser stopped.
