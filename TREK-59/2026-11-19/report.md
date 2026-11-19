# TREK-59 manual verification: pass

PR #57, head f3af0e2, local stack (API :5080, Vite :5173), Playwright Chromium, 1280×800, light and then dark. One throwaway user, project TRK, five items: TRK-1 Epic and TRK-2 Story in To Do, TRK-3 Task In Progress, TRK-4 Task In Review, TRK-5 Bug Done.

| # | Page | Result |
|---|------|--------|
| 1, 4 | List | A status chip per row (To Do an empty ring, In Progress a half ring, In Review a ring with a dot, Done a filled check), visibly quieter than the type badges. |
| 2, 5 | Board | Each column heading starts with its status dot; the headings still read "To Do (2)", "In Progress (1)", "In Review (1)", "Done (1)". |
| 3, 6 | Item page TRK-2 | The To Do dot inside the status select; the children (In Progress, In Review) as chips. |

Cleanup: the test user and org were deleted with their data; the API, Vite and browser were stopped; the local screenshots were deleted.
