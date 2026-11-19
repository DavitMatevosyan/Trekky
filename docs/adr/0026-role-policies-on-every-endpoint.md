# ADR-0026: Every endpoint names the role policy for its action

- **Status:** Agreed. The requirement is Davit's (TREK-45 and TREK-46: Owner, Admin, Member and Viewer checked on every endpoint, a policy per action, one organization per user); the policy set, the Admin-only project creation and the claim-based check were decided by Claude under ADR-0023.
- **Date:** 2026-11-18
- **Deciders:** Davit, Claude
- **Builds on:** ADR-0003 (the `role` claim in the access token), ADR-0002 (tenancy by `org_id` and RLS)

## Context

Until TREK-46 every signed-in user could do everything in their organization: the API only asked for an authenticated user, and the `role` claim it already issued (ADR-0003) was never read. That was harmless while every organization had one Owner, but TREK-47 (invites), TREK-48 (members page) and TREK-49 (bot accounts) bring other people and Claude's own bot into an organization. The AI must go through the same rules as people (TREK-45), so the rules have to live in the API, not in which buttons the UI shows. RLS already keeps organizations apart; roles decide what someone may do inside one.

## Decision

Every endpoint names exactly one policy from `TrekkyPolicies` (SharedKernel), or `AllowAnonymous` for sign-up, login, the OAuth endpoints, health and (in Development only) the OpenAPI document. A policy is one kind of action and the roles it lets through, checked against the access token's `role` claim:

| Policy | Actions | Roles |
| --- | --- | --- |
| `ReadWork` | read projects, work items, the board, comments, history, members, `/me` | Owner, Admin, Member, Viewer |
| `EditWork` | create, edit, change status and move work items; add, edit and delete one's own comments | Owner, Admin, Member |
| `ManageProjects` | create projects | Owner, Admin |
| `ManageMembers` | invite, change and remove members (TREK-47, TREK-48) | Owner, Admin |

A refusal is `403` with problem details. The fallback policy (an endpoint that names none) is `ReadWork`, so a token without a known role reaches nothing. Rules about the target rather than the caller's role stay in the handler: only a comment's author edits it, and only an Owner makes or removes Admins and Owners (TREK-48). A user belongs to exactly one organization: `memberships.user_id` is unique, while the `(org_id, user_id)` key stays so this can be relaxed for SaaS.

## Consequences

- `EndpointPolicyTests` lists every endpoint: a new one fails the build's tests unless it names a policy, and a write that only needs `ReadWork` fails too. The same test pins the role × policy matrix.
- The role is read from the token, so a role change applies at the caller's next token refresh (at most 15 minutes, the access token lifetime). Removing a member must also revoke their tokens (TREK-48).
- `/me`, which the web app reads to hide controls, takes the role from the database, so for those minutes the UI can offer what the API still refuses (a 403) or hide what it still allows. TREK-48 (role changes) must close that gap, e.g. by revoking the changed member's tokens: token entries are validated on every request, so the next call fails, and signing in again issues a token with the new role.
- The web app hides what the role cannot do (new project for Members and Viewers; editing, status, drag and drop and comments for Viewers), but that is convenience only.
- Bots (TREK-49) get a `role` claim like people, so these policies apply to them unchanged.

## Alternatives considered

- **Check roles inside each service method** — spreads the rule over every module and is easy to forget; the endpoint list cannot be checked in one place.
- **Read the role from the database on every request** — a role change would apply at once, but every request pays a query, and the token already carries the role by contract (ADR-0003). Revisit if 15 minutes proves too long.
- **One policy per role (`RequireMember`, …)** — reads as "who" rather than "what", so adding a role or moving an action between roles means touching every endpoint instead of one table.
- **Members create projects** — a project sets keys and structure for the whole organization, so it sits with Admins, as in most trackers.
