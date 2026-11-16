# TREK-40 manual verification, 2026-11-16: pass

Tree: PR #35 head 5207bd5, local stack (API :5080, web :5173 from the TREK-40 worktree), Playwright Chromium.

- Login page (01): "Source code (AGPL-3.0)" under the card, href https://github.com/DavitMatevosyan/Trekky.
- App shell (02): the same link in the page footer (contentinfo); clicking it opened https://github.com/DavitMatevosyan/Trekky in a new tab.
- Desktop behaviour (system browser, also on the first-run server screens) is covered by Vitest (SourceCodeLink, DesktopGate).

Cleanup: test user and org deleted, screenshots deleted locally, API, dev server and browser stopped.
