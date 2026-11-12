# TRK-19 manual verification: pass

Commit ea5b3cd on TRK-19-edit-delete-comments, local stack (Postgres, API :5080, Vite :5173), test user uitest-trk19-*@example.com.

| # | Check | Result |
| --- | --- | --- |
| 1 | Edit opens an inline editor on your own comment | pass |
| 2 | Saving shows "edited", announces "Comment saved.", and the history adds "edited a comment: “…”" | pass |
| 3 | Delete asks inline: "Delete this comment?" with Keep it / Delete comment | pass |
| 4 | Deleting the OLDER comment removes it, announces "Comment deleted.", and the history adds "deleted a comment"; kept after reload | pass |
