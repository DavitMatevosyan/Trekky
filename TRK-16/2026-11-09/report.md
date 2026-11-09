# TRK-16 manual verification: pass

- **Tree:** `5856838` (PR #21), local stack, real browser.
- **Data:** synthetic user `uitest-trk16@example.com`, org "UI Test TRK-16", 4 work items.

| Check | Result |
| --- | --- |
| Mouse drag, allowed move | TRK-1 To Do to In Progress; the target column is outlined during the drag |
| Browser console before the refused moves | No errors |
| Mouse drag, disallowed move | TRK-1 In Progress to Done goes back; "TRK-1 can only move to To Do or In Review." |
| Keyboard move | TRK-4 In Progress to In Review with Space, arrow, Space; focus stays on TRK-4; no alert |
| Card changed by someone else (412) | Goes back; "TRK-2 was changed by someone else…"; the new title shows |
| A plain click on a card | Opens TRK-3 |
| Phone width, 390 px, keyboard | Moved to the next column, which is in view; no horizontal scroll |

After the run, the test users, orgs and their data, the screenshots and the servers were cleaned up.
