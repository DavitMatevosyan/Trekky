# TREK-81 manual verification: pass

Run on 2026-11-23 against commit 17bab7c (PR #64). Local stack: API on :5080, Vite on :5173, Chromium through the Playwright MCP, 1280×800, light theme. The test data is synthetic: user `uitest-trek81-…@example.com` and org "UI Test TREK-81". The user created project TRK with Epic TRK-1 and three children (Story TRK-2, Task TRK-3, Bug TRK-4).

| # | Check | Result |
| --- | --- | --- |
| 1 | Open TRK-1 from the list. Hovering a child row gives it the hover background and underlines both key and title | pass (`01-hover-title.png`, with `:hover` forced through DevTools because the MCP screenshot drops it; computed style while hovering: underline, `bg-muted`) |
| 2 | Each row is one link: hrefs `/TRK/items/TRK-2`, `-3`, `-4`; Tab from row 1 lands on row 2 (one tab stop per row), with a visible focus ring | pass (`02-keyboard-focus.png`) |
| 3 | Clicking the child's **title** opens TRK-3, and its breadcrumb returns to the list it came from (`TRK → /TRK`) | pass (`03-child-opened.png`) |

Cleanup: the test user, org and data were deleted, screenshots were removed locally, and the API, Vite and the browser were stopped.
