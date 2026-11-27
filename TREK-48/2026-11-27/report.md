# TREK-48 Members page: manual verification (2026-11-27)

Stack: web (Vite :5174) → API (:5091) on commit 794b4fe, own local database, Mailpit for invite mail.
Run: a Playwright script in Chromium driving the real app. An Owner signs up and creates a project. Alan (Admin) and Mia (Member) join by invite, and Mia creates TRK-1.

| # | Check | Result |
| --- | --- | --- |
| 01 | Owner sees 3 members with name, email, Person badge and role; no controls on their own row (last Owner) | PASS |
| 02 | Choosing Viewer for Mia only shows "Save role"; nothing is sent yet | PASS |
| 03 | Saving the role announces "Mia Member is now Viewer." | PASS |
| 04 | Mia's next page load signs her in again with the new role; as Viewer she sees a read-only list | PASS |
| 05 | Admin is offered only Member and Viewer, and no controls for the Owner | PASS |
| 06 | API: an Admin's DELETE of the Owner answers 403 problem details | PASS |
| 07 | API: the only Owner demoting themselves answers 409 | PASS |
| 08 | Remove asks for confirmation in place | PASS |
| 09 | After removal Mia is gone from the list and the removal is announced | PASS |
| 10 | Mia's open tab goes to login; logging in again is refused ("no longer belongs to an organization") | PASS |
| 11 | TRK-1's history still says "Mia Member created this Task" | PASS |

Tokens for 06 and 07 were taken from the users' own requests and are not shown.
