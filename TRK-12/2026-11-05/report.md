# TRK-12 UI validation — 2026-11-05

Result: **pass**.

- **Setup:** local stack (Docker Compose Postgres, API on :5080, Vite on :5173), Chromium via Playwright MCP, viewport 1280×800.
- **Test data:** synthetic user `uitest-trk12@example.com`, org "UI Test Org", project `TRK` with Epic TRK-1 and Story TRK-2 under it.
- **Code tested:** commit 5439c4f (PR #16).

A native select's open list does not appear in screenshots, so the options column comes from the page's accessibility tree at each step.

| # | Check | Options offered | Result |
|---|---|---|---|
| 01 | A new Story starts in To Do | To Do (selected), In Progress | pass |
| 02 | Picking In Progress saves it and announces "TRK-2 moved to In Progress." | To Do, In Progress (selected), In Review | pass |
| 03 | In Review offers going back to any earlier status, or forward to Done | To Do, In Progress, In Review (selected), Done | pass |
| 04 | Done can only reopen to In Progress | In Progress, Done (selected) | pass |
| 05 | After a full page load, the parent TRK-1 lists TRK-2 as Done | — | pass |
| 06 | The project list shows TRK-2 as Done, updated now | — | pass |
| 07 | With the edit form open, the status select is disabled. The select sits below the fold, so the evidence is the accessibility tree: `combobox "Status" [disabled]`. | — | pass |
| 08 | Changed by someone else: tab B moves TRK-2 Done → In Progress, then tab A (still showing Done) tries Done → In Progress. The API answers 412; tab A shows the latest status (In Progress, with its moves) and "This item was changed by someone else. Its latest status is shown; change it again." | To Do, In Progress (selected), In Review | pass |

**Console:** one error, the expected `412 Precondition Failed` from check 08.

**Observation (cosmetic, not blocking):** when the status error shows (08), the "Status" label is centred vertically against the select and the error text together, instead of lining up with the select.

**Not checked in the browser:** a 422 can't be triggered from the UI, because the select offers only allowed moves. API integration tests and component tests cover it.

**Cleanup:** test user, org and their data deleted; screenshots deleted locally; API, dev server and browser stopped.
