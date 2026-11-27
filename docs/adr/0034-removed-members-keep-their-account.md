# ADR-0034: Removed members keep their account, marked removed; role changes and removals revoke their tokens

- **Status:** Agreed. The requirement is Davit's (TREK-48: members with name, email, role and person or bot; Admins change roles and remove members, only an Owner makes or removes Admins and Owners; the last Owner stays; a removed member's tokens stop on the next request and their name stays in history; activity events). Keeping the row, the token handling, the last-Owner lock and not re-inviting removed members for now were decided by Claude under ADR-0023.
- **Date:** 2026-11-27
- **Deciders:** Davit, Claude
- **Builds on:** ADR-0026 (role policies, the role read from the token), ADR-0027 (invites), ADR-0011 (outbox)

## Context

Until TREK-48 nobody left an organization. Past work names people by user id (creator, assignee, comment author, history actor), and other modules find those names through `IUserDirectory`, which looks users up through the organization's memberships, because users have no RLS of their own. A user belongs to exactly one organization (TREK-46), so an account without a membership can do nothing. The access token carries the role (ADR-0026); it lives 15 minutes, but token entries are checked on every request, so revoking them takes effect at once.

## Decision

- **Removing keeps the row.** `DELETE /members/{userId}` sets `memberships.removed_at`; the membership and the user stay. The members list, `/me`, the authorize and token endpoints and assignment only see members with no `removed_at`; name lookups (`FindMembersAsync`) also find removed members, so their past work keeps their name.
- **Tokens.** A removal revokes all of the member's authorizations (refresh and access tokens) and rotates their security stamp (session cookie), in the same transaction as the change. A role change revokes the authorizations but keeps the cookie, so the web app's silent sign-in gets a token with the new role on the next request. Logging in again as a removed member answers `403` after the password check.
- **Who and how many.** `ManageMembers` for both endpoints; Admins change and remove Members and Viewers; only an Owner makes, changes or removes Admins and Owners, and may make another member an Owner. A change that would leave no Owner answers `409`. Each change locks the organization's row (`FOR NO KEY UPDATE`) first, so two Owners cannot demote each other at once, and then judges the caller by the role they have now, read under that lock, not by the token's. A role change needs `If-Match` with the member's etag (`xmin`).
- **The page.** The web app offers no change of your own role and none for the last Owner; the API still lets an Owner step down while another Owner remains. A chosen role is applied with "Save role", so moving through the select with the keyboard changes nothing.
- **No way back yet.** A removed member's address keeps its account, so inviting it again answers `409`, and it cannot sign up elsewhere (one organization per user).
- **Events.** `MemberRoleChanged` (`from`, `to`) and `MemberRemoved` (`role`), entity type `member` with the user's id, both with the member's id, email and name.

## Consequences

- History, comments, created items and assignees read the same after someone leaves; an item stays assigned to a removed member until someone changes it, and the picker shows them only as its current value. The assignee filter, built from the members list, cannot pick removed members.
- A changed member's open web tabs reload once through the silent sign-in; the desktop app loses its refresh token and signs in again through the system browser, which is quick while that browser's session cookie is valid. A removed member's tabs go to the login page; logging in again tells them they no longer belong to an organization.
- Bringing someone back, or letting a removed person join another organization, needs its own task: for example restoring the membership, or accepting an invite with the existing account's password.
- Bots (TREK-49) are members too: the list already has `kind` (`person` for everyone until then), and these rules apply to them unchanged.

## Alternatives considered

- **Delete the membership** — the person would vanish from the organization's name lookups, and past work would show no name.
- **Delete or anonymise the account** — frees the address for a new sign-up, but loses the name history needs and cannot be undone if a user may later belong to several organizations.
- **Read the role from the database on every request** — makes a role change apply without revoking tokens, but adds a query to every request (ADR-0026 rejected this).
- **Check "last Owner" without a lock** — two Owners demoting each other at the same moment could both pass and leave none.
