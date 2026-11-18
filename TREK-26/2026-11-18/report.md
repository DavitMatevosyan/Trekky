# TREK-26 manual verification, 2026-11-18

PR #42, head 475ec64, local stack (API + web dev server started by Playwright), Chromium.

Steps: sign up, create a project and a story, open it, post the comment "Posted right before the reload.", then reload the page **at once**: before the outbox (every 500 ms) has delivered the comment's event to the history.

Result: pass. After the reload the History lists "commented “Posted right before the reload.”" above "created this Story". Before this fix, a reload inside that window left the comment out of History until the next change (CI's comments e2e failed on dev this way).

Also run: comments e2e 10/10 with 5 parallel workers; full Playwright suite 13/13.

Cleanup: 98 e2e users from today's local runs and their orgs, projects, items, comments and events deleted; servers and browser stopped; local screenshots deleted.
