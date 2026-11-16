# TREK-44 manual verification, 2026-11-16: pass

Tree: PR #34 head d9db19c, run locally (API :5080 and web dev server :5173 from the TREK-44 worktree, Postgres 17), Playwright Chromium. A test user with project TRK and five task cards spread over To Do, In Progress and In Review.

| Width | Result |
| --- | --- |
| 1280 px (01) | Four columns side by side, filling the page; no horizontal scrollbar |
| 768 px (02) | Still one row; it scrolls sideways (Done off to the right) |
| 390 px (03) | Still one row (all four column headings at the same height, top 215 px); row 944 px wide in a 358 px viewport |
| 390 px, keyboard (04) | Focusing the "Board columns" row (focus ring visible) and pressing ArrowRight scrolls it to Done (scrollLeft 586) |

Cleanup: the test user, its org and data deleted; screenshots deleted locally; API, dev server and browser context stopped.
