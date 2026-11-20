# ADR-0027: Invites are single-use email links, read by token hash before a tenant is known

- **Status:** Agreed. The requirement is Davit's (TREK-47: invite by email with a role, expiring single-use link, SMTP2Go in production and Mailpit locally, reject an email that belongs to another organization, resend and revoke, activity events); building it first with Mailpit and SMTP off in production, with the link shown to copy, is Davit's choice (2026-11-20). The token design, the RLS policy, the roles an Admin may invite and the link format were decided by Claude under ADR-0023.
- **Date:** 2026-11-20
- **Deciders:** Davit, Claude
- **Builds on:** ADR-0003 (sign-up and the session cookie), ADR-0007 (`user_self_read`), ADR-0011 (outbox), ADR-0026 (role policies)

## Context

Until now the only way into an organization was to create one at sign-up. TREK-47 lets Admins and Owners bring people in. The person opening an invite link has no account and no token, so the API cannot know the tenant from the request, yet invites are tenant rows under forced RLS (ADR-0002). A user belongs to exactly one organization (TREK-46). The production host has no mail account yet.

## Decision

- **The link.** An invite carries 32 random bytes as base64url (43 characters); only their SHA-256 hash is stored (`identity.invites.token_hash`, unique). The link is `{web app origin}/invite#{token}`: the fragment never reaches a server, so the token stays out of access logs and Referer headers, and the page sends it to the API in a request body (`POST /api/v1/auth/invites/preview` and `/accept`). The origin is `Invites:LinkBaseUrl`, or else that of the web client's first redirect URI (the public address in production).
- **Single use, 7 days.** Accepting, withdrawing or resending ends the current link; resending makes a new token and restarts the 7 days. An expired invite stays open and can be resent. At most one open invite per address per organization (partial unique index).
- **Reading without a tenant.** A read-only policy `invite_token_read` on `identity.invites` shows the one row whose `token_hash` equals `app.invite_token_hash` (transaction-local, like `user_self_read`). Preview and accept set it, find the invite, then act as its organization (`app.org_id`) for every write. Nothing else can list invites across organizations.
- **Accepting** creates the user with the invite's email, a membership with the invited role in the inviter's organization, and marks the invite accepted, in one transaction; then it signs the user in like sign-up does. It never creates an organization. The email is **not** marked confirmed: the inviter is also given the link, so using it proves nothing about the inbox.
- **Races.** Invites carry a row version (`xmin`). Accept, resend and withdraw each check it, so when two of them hit one invite at once only the first wins: a withdrawn invite never also lets someone in. The loser writes nothing: an accept answers as the link now stands (`410` withdrawn or used, `404` replaced), a resend or withdraw answers `409`.
- **Who invites whom.** `ManageMembers` (Owner, Admin) for every invite endpoint. Invitable roles are Admin, Member and Viewer; only an Owner sends, resends or withdraws an invite for an Admin. Nobody is invited as Owner: ownership is given to an existing member (TREK-48).
- **One organization per user.** An address that already has an account is refused when inviting (`409`: already a member, or in another organization) and again when accepting, if it got an account in the meantime.
- **Mail.** MailKit over SMTP (`Smtp:Host`, `Port`, `Username`, `Password`, `From`). With a login, TLS is required (from the start on port 465, STARTTLS otherwise), so a password is never sent in the clear; without one (Mailpit), TLS only if offered. The whole exchange has a 10-second deadline: Mailpit locally and in CI, SMTP2Go in production once Davit adds an account. Mail goes out after the invite is saved; a missing or failing server never fails the request. The response always carries the link and `emailSent`, and the web app shows the link to copy when no email went out. The link is returned only in that response.
- **Events.** The identity module gets its own outbox. `InviteSent` (payload `resent`), `InviteRevoked` and `InviteAccepted` (actor: the new member) with entity type `invite`; no token or hash in any event.

## Consequences

- Production works without a mail account: an Admin copies the link from the Members page. Adding SMTP2Go is configuration only (`SMTP_*` in `deploy/.env`).
- Whoever can read the database cannot turn it into working links, and a leaked link stops working once used, withdrawn, resent or 7 days old.
- Admins can find out whether an address already has a Trekky account (the 409). That is what the one-organization rule needs, and only Admins and Owners can ask.
- An Admin holding a link could accept it themselves and so take an address that has no account yet; under one organization per user its owner could then not sign up elsewhere. The account is not marked confirmed, so a later confirm or reset-password flow can reclaim it; until then this is a trust boundary inside the organization's Admins.
- `GET /invites` (like `GET /members`) is not paginated: an organization's open invites stay few. Add a cursor if that changes.
- The identity schema now has an outbox and is drained by the dispatcher like the other modules.
- TREK-48 builds on this: the members list, role changes and removal sit on the same page.

## Alternatives considered

- Token in the path or query (`/invite/{token}`) — simpler, but it lands in proxy logs (Caddy, Funnel) and Referer headers.
- A security-definer SQL function to look up invites — works, but hides tenant access in a function; a SELECT-only policy next to `tenant_isolation` follows ADR-0007's pattern and is tested the same way.
- Store the token itself, encrypted — needed only to show old links again, which a resend covers.
- Fail the invite when mail cannot be sent — would block production until SMTP2Go exists, and a mail outage would lose invites.
