# TREK-56 manual verification: pass

PR #50, head be27a14, local stack (API :5080, Vite :5173), Playwright Chromium, 1280×800. One throwaway user, project TRK: an epic, two tasks (one In Progress with a comment) and a bug.

| # | Step | Result |
|---|------|--------|
| 1–3 | Light: list, board, item page | Warm beige page; panels (table, columns, details, comments, history) a step lighter; board cards lighter again; warm brown primary buttons. |
| 4–6 | Dark: list, board, item page | Warm charcoal, not black; panels and cards raised a step each; warm off-white text; native selects follow (color-scheme). |
| 7 | Keyboard on the switch | Focus on Dark, ArrowRight selects System (radio group). |
| 8–9 | Log in page with System, OS light then dark | Follows the OS setting live (emulated prefers-color-scheme), no switch needed there. |

Cleanup: the test user and org were deleted with their data; the API, Vite and browser were stopped; the local screenshots were deleted.
