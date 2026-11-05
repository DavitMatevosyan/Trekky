# TRK-11.1 UI validation — 2026-11-05

Result: **pass**. Local stack (Docker Compose Postgres, API on :5080, Vite on :5173), Chromium via Playwright MCP, viewport 1100×800.
Synthetic user `uitest-trk11-1@example.com`, org "UI Test TRK-11.1", project `TRK`. Tree: commit 4acc27d (PR #15).

| # | Check | Result |
|---|---|---|
| 01 | Epic: the parent field is disabled and says "Epic items have no parent." | pass |
| 02 | Story: typing "dog" finds only TRK-1 (Epic, title match); TRK-2 "v1.1 Team" is not offered | pass |
| 03 | ArrowDown highlights the option with a visible outline | pass |
| 04 | Enter picks it ("TRK-1 · v1.0 Dogfood", clear button, hint "Optional: Epic.") without submitting | pass |
| 05 | SubTask: the empty search offers only the Story TRK-3; both Epics are filtered out by type | pass |
| 06 | Unknown key "TRK-9" typed and Create clicked: nothing is created; the field is marked invalid with "There is no item TRK-9 in this project." | pass |
| 07 | Valid key "trk-3" typed (not picked) and Create clicked: TRK-4 is created with parent TRK-3 (review must-fix #1) | pass |
| 08 | Editing TRK-3 (the only Story) and switching it to SubTask: the old Epic parent is dropped, and TRK-3 is not offered as its own parent ("No Story or Task items match.") | pass |

Console: only the expected 404s for `/work-items/by-key/TRK-9` (one from blur, one from submit). The duplicate is listed as a non-blocking suggestion in the PR.

Cleanup: test user, org and their data deleted; screenshots deleted locally; API, dev server and browser stopped.
