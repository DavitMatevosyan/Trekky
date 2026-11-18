# TREK-46 manual verification: pass

PR #52, head 3fb80b4. The run used a local stack built from this branch: API on :5091 and Vite on :5174, because another worktree held :5080/:5173. It had its own throwaway database and Playwright Chromium at 1280×860.

Data: three throwaway users (`uitest-trek46-*@example.com`) and project ROLE. The Owner signed up and created ROLE-1 with a comment. The Viewer and Member each signed up and were then moved into the Owner's organization with their role in the database, because there are no invites yet (TREK-47). Each logged out and in again for a token carrying the role.

| # | Who | Step | Result |
|---|-----|------|--------|
| 1 | Owner | Projects page | "You are the Owner of Roles Demo." and "+ New project" shown. |
| 2 | Viewer | Projects page | "You are the Viewer of Roles Demo." The project is listed; no "New project". |
| 3 | Viewer | Board | The card shows; no "New item". The card link has no "movable card" role description or drag instructions. |
| 4 | Viewer | ROLE-1 | Description, comments and history are readable. Status ("To Do") and assignee ("Nobody") are plain text. No "Edit" and no comment form. |
| 5 | Viewer | API with the Viewer's own token | `GET /work-items/{id}` 200. `POST` work item, comment and project each return **403** `application/problem+json`. |
| 6 | Member | Projects page | "You are the Member of Roles Demo."; no "New project". |
| 7 | Member | ROLE-1 | Has "Edit" and the Status and Assignee selects. Moved ROLE-1 to In Progress (announced) and posted a comment, which has Edit and Delete. |
| 8 | Member | API with the Member's own token | `POST` work item 201 (ROLE-2, created by the member). `POST /projects` returns **403** problem details. |

The access tokens were taken from the tabs' own requests and used inside the page; they appear in no screenshot.

Note on 04: the comment shows `**viewers**` unrendered because the lazily loaded Markdown renderer had not arrived yet when the screenshot was taken. 07 shows the same comment rendered.

Cleanup: the throwaway database (every test user, org and their data) was dropped. The API, Vite and browser were stopped, and the local screenshots were deleted.
