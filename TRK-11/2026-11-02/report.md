# TRK-11 UI validation, 2026-11-02

Local stack (Postgres in Docker, API on :5080, Vite on :5173), synthetic user `uitest-trk11@example.com`, org "UI Test Org".

| # | Check | Result |
| --- | --- | --- |
| 01 | Project list `/TRK`: key, type, title, status, parent key, updated; newest first | pass |
| 02 | SubTask with an Epic parent is refused, with the allowed types on the parent field (US-06 AC4/AC5) | pass |
| 03 | Item detail `/TRK/items/TRK-2`: rendered Markdown, status, parent link, created/updated (US-09) | pass |
| 04 | Parent Epic lists its child with status | pass |

Also run on the same tree: Playwright e2e 4/4 (`frontend/e2e/work-items.spec.ts` covers create → parent → list → detail → edit).
Browser console: only the expected 422 from check 02.
Test data, screenshots and servers were removed after the run.
