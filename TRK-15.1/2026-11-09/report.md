# TRK-15.1 manual verification: pass

- **Tree:** `b9b056a` (PR #20), local stack, real browser.
- **Data:** synthetic user `uitest-trk151@example.com`, org "UI Test TRK-15.1", 23 work items.

| Check | Result |
| --- | --- |
| Migrations apply on the local database | Pass, API started and served |
| Editing an old Done item does not bring it back | TRK-1 edited (200); Done shows TRK-21 to TRK-2 |
| New item on the board | TRK-22 created, card in To Do |
| "Show all" in the URL | `?done=all`, 21 cards, TRK-1 last |
| Card, then the breadcrumb | Back to `/TRK/board?done=all`, 21 cards |
| Reload on `?done=all` | 21 cards |
| Browser console before the simulated failures | No errors |
| "Show all" fails (simulated 500) | 20 cards stay, alert under the heading, focus on the heading, Tab lands on "Try again" |
| "Try again" succeeds | 21 cards, no alert |
| Refresh fails (simulated 500) | Notice above the cards, 21 cards stay |

After the run, the test users, orgs and their data, the screenshots and the servers were cleaned up.
