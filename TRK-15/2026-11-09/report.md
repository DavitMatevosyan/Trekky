# TRK-15 manual verification: pass

- **Tree:** `ec703d5` (PR #19), local stack, real browser.
- **Data:** synthetic user `uitest-trk15@example.com`, org "UI Test TRK-15", 26 work items.

| Check | Result |
| --- | --- |
| One column per status in workflow order, with counts | To Do (2), In Progress (1), In Review (1), Done (22) |
| Cards show key, type and title; a long title stops at 3 lines | Pass |
| API with 22 Done items | `count` 22 with 20 items; with `allDone=true`, 22 items |
| Done shows the latest 20 and "Show all" | 20 cards, TRK-22 to TRK-3 |
| "Show all" | 22 cards, button gone, focus on the "Done (22)" heading |
| A card opens its item | TRK-24 opened |
| A status change reaches the board | TRK-24 moved to Done: In Review (0), Done (23), TRK-24 first |
| Board and List tabs | Current tab marked with `aria-current="page"` |
| Phone width, 390 px | One column, no horizontal scroll |
| Browser console errors during the board checks | None |

A failed "Show all" is covered by component tests, not this run.

After the run, the test users, orgs and their data, the screenshots and the servers were cleaned up.
