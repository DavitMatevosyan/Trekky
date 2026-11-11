# TRK-17 manual verification: pass

Commit 0980303 on TRK-17-board-reorder, local stack (Postgres, API :5080, Vite :5173), test user uitest-trk17-*@example.com.

| # | Check | Result |
| --- | --- | --- |
| 1 | New items go on top of To Do (TRK-4, TRK-3, TRK-2, TRK-1) | pass |
| 2 | Mouse: TRK-1 dragged to the top; order TRK-1, TRK-4, TRK-3, TRK-2 kept after reload | pass |
| 3 | Keyboard: Space, Down, Down announces "TRK-1 is over To Do, place 3 of 4." | pass |
| 4 | Space drops it: TRK-4, TRK-3, TRK-1, TRK-2; TRK-3 dragged to In Progress; kept after reload | pass |
| 5 | TRK-1 history has only "created": reorders record no events | pass |
