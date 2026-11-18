# TREK-44 e2e flake fix, manual verification, 2026-11-18

PR #43, head 8172ef0, local stack (API + web dev server started by Playwright), Chromium. E2E-only change: no app code changed.

- Cause, confirmed from CI run 44268959178's trace ("Move of TRK-1 cancelled." right after the pickup) and reproduced locally: dnd-kit cancels a keyboard drag on window resize, and the test resized the viewport just before the pickup.
- After the fix: `board-layout.spec.ts` passed 24/24 (8 repeats, 6 workers) and the full suite 13/13.
- Screenshots: the end of each keyboard test. The card was moved column by column by keyboard into Done, which starts off screen; the row has scrolled to it and the card keeps the focus.
  1. `01-keyboard-move-390.png`: phone width, 390 px.
  2. `02-keyboard-move-700.png`: 700 px.

Cleanup: today's local e2e users deleted with their orgs and data; servers and browser stopped; local screenshots deleted.
