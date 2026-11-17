# TREK-52 manual verification, 2026-11-17: pass

Tree: PR #38 head 465c974. The API ran from the TREK-52 worktree on :5091, because :5080 and :5173 were held by another worktree's servers. TREK-52 changes only the API, so a script called the real API as a fresh test user and rendered each request and its real response as a page; Playwright Chromium took the screenshots. The token stays inside the script, and project ids are shown as {projectId}.

Data: project TRK with open epic TRK-1 (story TRK-2, Done task TRK-3 assigned to me, subtask TRK-4), epic TRK-5 in Done (story TRK-6, task TRK-7 in progress and mine, subtask TRK-8, bug TRK-9, tech debt TRK-10 in review), a loose Done task TRK-11 and a loose bug TRK-12 in progress and mine.

- List (01): by default TRK-5 and everything under it are hidden, while Done TRK-3 under the open epic stays (totalCount 6). completedEpics=show returns all 12. open=true hides each Done item on its own. parent=trk-1 returns everything under TRK-1 at any depth, and the key works in any case. assignee=me leaves out TRK-7, which is under the Done epic. status, type and completedEpics combine with AND, and repeated values combine with OR.
- Board (02): by default TRK-5's cards leave every column, Done included. A status filter keeps all four columns and leaves the others empty with count 0. open=true empties Done. parent with assignee narrows to TRK-3.
- Move (03): TRK-1 is dropped between TRK-13 and TRK-4 while hidden cards TRK-9, TRK-8 and TRK-6 sit between them. The board that shows every card gets 409 Board changed. The default board gets 200, and TRK-1 lands directly below TRK-13 in both views.
- Errors (04): unknown values return 400 problem details naming each parameter in one response, on both the board and the list.

Cleanup: test user, org and their data deleted; screenshots deleted locally; API, page server and browser stopped.
