# User stories — v0.1 to v1.0

Each story is small enough to verify by hand. Acceptance criteria (AC) are what the TRK's tests and review check against. Items marked **(proposal)** are a first answer to confirm in review.

Applies to every story:

- Every change to data is recorded in the activity log (who, when, what changed).
- A user only ever sees their own organization's data. Another organization's resource answers **404**, never 403, so its existence is not revealed.
- UI meets WCAG AA (keyboard reachable, visible focus, labels, contrast); user-facing text is ready to move into i18n files.
- Errors are shown in plain words next to what caused them; the API returns RFC 9457 problem details.

---

## v0.1 Skeleton

### US-01 Sign up — TRK-7, TRK-8

As a new user, I want to create an account and an organization in one step, so I can start using Trekky immediately.

- AC1: The form asks for email, password and organization name. All are required.
- AC2: Email must be valid and not already registered; a taken email shows "An account with this email already exists."
- AC3: Password is at least 12 characters. **(proposal)** No other composition rules.
- AC4: On success the organization is created, I am its **Owner**, and I land on the projects page, logged in.
- AC5: User, organization and membership are created in one transaction; a failure leaves none of them behind.

### US-02 Log in and log out — TRK-7, TRK-8

As a user, I want to log in and out, so only I can use my account.

- AC1: Wrong email or password shows one generic message: "Email or password is incorrect."
- AC2: After 5 failed attempts the account is locked for 15 minutes. **(proposal)**
- AC3: Reloading the page keeps me logged in (silent re-authentication, see ADR-0003).
- AC4: Log out ends the session and revokes my refresh token; the back button does not show protected pages.
- AC5: The access token expires after 15 minutes and is refreshed without me noticing.

### US-03 Tenant isolation — TRK-6

As an organization, I want my data invisible to every other organization, so I can trust Trekky with real work.

- AC1: An integration test with two organizations proves neither can read, list, update or delete the other's rows, through the API and directly against the database as the app role.
- AC2: With no tenant set, queries return no rows and inserts fail.
- AC3: A pooled database connection never carries one request's tenant into the next.

### US-04 Trekky runs online with backups — TRK-9

As the team, we want Trekky reachable over HTTPS with nightly backups, so we can use it daily without risk of losing work.

- AC1: The web app and API are served over HTTPS on the POC domain; HTTP redirects to HTTPS.
- AC2: A nightly `pg_dump` runs and a copy is stored off the VM; 14 days are kept.
- AC3: One restore into an empty database has been done and documented in `deploy/README.md`.

---

## v0.2 Work items

### US-05 Create and list projects — TRK-10

As a member, I want projects with short keys, so work is grouped and items get readable keys.

- AC1: A project has a name (1–80 characters) and a key matching `^[A-Z][A-Z0-9]{1,9}$`, unique in the organization.
- AC2: The key is suggested from the name (e.g. "Trekky" → `TRK`), editable at creation, and **cannot change** afterwards.
- AC3: The projects page lists projects by name with their key and item count.
- AC4: Creating a project records a `ProjectCreated` event.

### US-06 Create a work item — TRK-11

As a member, I want to create work items of the right type, so the work is captured with its structure.

- AC1: Fields: type (Epic, Story, Task, SubTask, Bug, TechDebt), title (1–200 characters, required), description (Markdown, optional), parent (optional unless the type requires one).
- AC2: The item gets the next key in its project (`TRK-1`, `TRK-2`, …); numbers are never reused.
- AC3: New items start in **To Do**.
- AC4: Parent rules **(proposal)**:

  | Item type | Allowed parent |
  | --- | --- |
  | Epic | none |
  | Story | Epic, or none |
  | Task | Story or Epic, or none |
  | SubTask | Task or Story (required) |
  | Bug | Epic or Story, or none |
  | TechDebt | Epic, or none |

- AC5: The parent must be in the same project. A rule violation shows which parents are allowed.

### US-07 Edit a work item — TRK-11

As a member, I want to edit an item's title, description, type and parent, so it stays accurate.

- AC1: Changes are saved only if the item has not changed since I opened it; otherwise I see "This item was changed by someone else" and the latest version (ETag / If-Match).
- AC2: Changing type or parent re-checks the parent rules for the item and its children.
- AC3: Each saved change records a `WorkItemUpdated` event listing old and new values.

### US-08 See work items as a list — TRK-11

As a member, I want a list of a project's items, so I can scan everything at once.

- AC1: Columns: key, type, title, status, parent key, updated.
- AC2: Sorted by last updated, newest first; loads 50 at a time with "Load more" (cursor pagination).
- AC3: Clicking a row opens the item detail.

### US-09 See a work item's detail — TRK-11, TRK-12

As a member, I want one page with everything about an item, so I don't have to hunt for context.

- AC1: Shows key, type, title, status, description (rendered Markdown), parent (link), children (links with status), created and updated.
- AC2: Shows the item's history, newest first: who, when, and what changed.
- AC3: The URL contains the item key (`/TRK/items/TRK-42`) so it can be shared and pasted into commits.

### US-10 Move an item through the workflow — TRK-12

As a member, I want to change an item's status, so everyone sees where the work is.

- AC1: Statuses: **To Do → In Progress → In Review → Done**.
- AC2: Allowed transitions **(proposal)**: forward one step, back to any earlier step, and Done can reopen to In Progress.

  | From | Allowed to |
  | --- | --- |
  | To Do | In Progress |
  | In Progress | To Do, In Review |
  | In Review | To Do, In Progress, Done |
  | Done | In Progress |

- AC3: A disallowed change is rejected with the allowed targets listed.
- AC4: Each change records a `WorkItemStatusChanged` event and appears in history.

---

## v1.0 Dogfood

### US-11 Work on a board — TRK-13

As a member, I want a Kanban board per project, so I can see and move work at a glance.

- AC1: One column per status, in workflow order, with a count per column.
- AC2: Cards show key, type, title and assignee.
- AC3: Dragging a card to another column changes its status; a disallowed move snaps back with the reason.
- AC4: Dragging within a column reorders it, and the order persists for everyone (rank).
- AC5: Cards can be moved with the keyboard as well as the mouse.

### US-12 Comment on an item — TRK-14

As a member, I want to discuss an item in comments, so decisions stay with the work.

- AC1: Comments are Markdown, shown oldest first with author and time.
- AC2: I can edit or delete my own comments; edited comments show "edited".
- AC3: Adding a comment records a `CommentAdded` event.

### US-13 Assign an item — TRK-15

As a member, I want to assign an item to a person, so it is clear who owns it.

- AC1: The assignee is one member of the organization, or nobody.
- AC2: The assignee shows on the card, the list and the detail page.
- AC3: Changing it records a `WorkItemAssigned` event.
- Note: until invites arrive (v1.1) the only member is the organization's Owner.

### US-14 Trekky tracks itself — TRK-16

As the team, we want our own Trekky work tracked in Trekky, so we find what hurts before anyone else does.

- AC1: Open TRK items move from Notion into a `TRK` project with the same numbers where possible.
- AC2: New work is created in Trekky only; the Notion list is frozen with a link to Trekky.
