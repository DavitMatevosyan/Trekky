# TREK-54 manual verification: pass

PR #47, head cbac2b4, local stack (API :5080, Vite :5173), Playwright Chromium, 1280×800 and 390×844.

Data: one throwaway user, project TRK.

- Epic TRK-1 is open. Under it: TRK-2 (Done), TRK-3 (In Review) and TRK-4 (In Progress, mine).
- Bug TRK-5 is in To Do, mine.
- Epic TRK-6 is Done, and so is TRK-7 under it.

| # | Step | Result |
|---|------|--------|
| 1 | Default board | To Do (2), In Progress (1), In Review (1), Done (1): TRK-6 and TRK-7 (completed epic) are hidden; Done holds TRK-2 of the open epic. |
| 2 | Open + Mine | URL `?open=true&assignee=me`; Done column gone; headings "To Do (1 of 2)", "In Progress (1 of 1)", "In Review (0 of 1)". |
| 3 | List tab, then Board tab | List at `/TRK?open=true&assignee=me` shows TRK-5, TRK-4; back on `/TRK/board?open=true&assignee=me`. |
| 4 | Epic TRK-1 | URL `?parent=TRK-1`; TRK-4, TRK-3, TRK-2 in their columns; "To Do (0 of 2)". |
| 5 | Status In Progress, keyboard-move TRK-4 to In Review | "TRK-4 no longer matches the filters, so it left the board." shown; focus on "Board columns"; empty state with "Clear filters". |
| 6 | Show completed epics | Done (3): TRK-6, TRK-7, TRK-2. |
| 7 | 390 px with Open | Bar wraps, columns scroll sideways inside their row, page scrollWidth 390. |

Cleanup: the test user and org were deleted with their data; the API, Vite and browser were stopped; the local screenshots were deleted.
