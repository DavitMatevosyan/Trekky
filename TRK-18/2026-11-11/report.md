# TRK-18 manual verification: pass

Commit 5cd3e1b on TRK-18-comments, local stack (Postgres, API :5080, Vite :5173), test user uitest-trk18-*@example.com.

| # | Check | Result |
| --- | --- | --- |
| 1 | A new item says "No comments yet." | pass |
| 2 | Posting an empty comment is refused with "Write something to comment." and no request | pass |
| 3 | Two comments posted (Markdown: bold, list, code) render, oldest first, with author and time; box cleared; "Comment posted." announced | pass |
| 4 | After reload both comments are kept, and the history shows both "commented “…”" events newest first | pass |
