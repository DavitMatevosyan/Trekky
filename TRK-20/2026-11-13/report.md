# TRK-20 manual verification: pass

Commit bc4c916 on TRK-20-assignee, local stack (Postgres, API :5080, Vite :5173), test user uitest-trk20-*@example.com.

| # | Check | Result |
| --- | --- | --- |
| 1 | Item sidebar: choosing yourself as Assignee saves, is announced, and the history adds "assigned this to …" | pass |
| 2 | List: Assignee column shows the name (other items "Nobody") | pass |
| 3 | Board: the card shows the assignee under the title | pass |
| 4 | Back to Nobody: announced, and the history adds "unassigned …" | pass |

Note: the list read right after assigning can show the cached row for a moment before its refresh; the refreshed list shows the assignee.
