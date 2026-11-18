# TREK-53 manual verification: pass

PR #46, head 67ceb47, local stack (API :5080, Vite :5173), Playwright Chromium, 1280×800 and 390×844.

Data: one throwaway user, project TRK. Items:

- TRK-1 is an epic in Done, with story TRK-3 under it.
- TRK-2 is an open epic, with tasks TRK-4 (mine) and TRK-5 under it.
- TRK-6 is a bug in Done with no epic, assigned to me.

| # | Step | Result |
|---|------|--------|
| 1 | Default list | 4 items match: TRK-6, TRK-4, TRK-5, TRK-2. TRK-1 (epic in Done) and TRK-3 under it are hidden; Done bug TRK-6 with no epic stays. URL plain `/TRK`. |
| 2 | Open + Mine, then reload | URL `?open=true&assignee=me`; only TRK-4; both toggles pressed, chips "Open", "Assignee: Me" after reload. |
| 3 | Status dropdown: To Do + Done | Checkbox group, "Status (2)", two chips, URL `?status=todo&status=done`. |
| 4 | Epic picker: "filt" → TRK-2 | URL `?parent=TRK-2`; TRK-4 and TRK-5 (2 items match). |
| 5 | Show completed epics | URL `?completedEpics=show`; 6 items incl. TRK-1 and TRK-3; chip "Completed epics shown". |
| 6 | `/` then type a search with no match | Focus jumps to search; URL `?q=nothing+like+this`; "No items match these filters." + "Clear filters". |
| 7 | Clear filters | Back to default list, focus in the search box. |
| 8 | 390 px | Bar wraps, no horizontal page scroll (scrollWidth 390). |

Cleanup: the test user and org deleted with their data; API, Vite and browser stopped; local screenshots deleted.
