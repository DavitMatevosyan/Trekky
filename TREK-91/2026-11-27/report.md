# TREK-91 manual verification (2026-11-27): pass

Commit a359784 (PR #78). TREK-91 adds a command-line tool and a doc, with no UI, so the check runs the tool on this
machine's real Claude Code transcripts and shows its output, rendered on a local page. Only aggregate numbers are
shown, never transcript content.

| # | Check | Result |
| --- | --- | --- |
| 1 | `node usage.ts --since 2026-11-20 --until 2026-11-26`: one week, all TRKs. Totals, by model, main loop against subagents, by TRK, tool outputs (once and carried) | pass: 5 sessions, 563 requests, 96% cache hit, $44.40, as in docs/token-usage.md |
| 2 | `node usage.ts --trk TREK-58 --until 2026-11-26`: one TRK from a session that ran several | pass: 78% of input carried over from earlier TRKs, start context "n/a" (the session started on another TRK), as in the doc |
| 3 | `node usage.ts --top 0`: bad arguments | pass: refused with exit 2 and a message |
| 4 | `node --test usage.test.ts` | pass: 28 tests |

Screenshots: `01-week-report.png`, `02-trk-report.png`, `03-args-and-tests.png`.
