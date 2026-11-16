# ADR-0007: Reading a user's own memberships before a tenant is known

- **Status:** Proposed
- **Date:** 2026-10-23
- **Deciders:** Davit, Claude

## Context

ADR-0002 isolates tenants with Postgres RLS keyed on `app.org_id`, set per connection from the access token's `org_id` claim. Login and token refresh (ADR-0003) run *before* there is a token: they must find which organization a user belongs to in order to put `org_id` into the token. `identity.memberships` is tenant-owned and RLS-protected, so with no tenant set the lookup returns nothing. The API role `trekky_app` has no `BYPASSRLS`, and `FORCE ROW LEVEL SECURITY` applies RLS to the table owner too, so a `SECURITY DEFINER` function would not help either.

## Decision

A tenant table that must be read before a tenant is known may get a second, **SELECT-only** policy, `user_self_read`: `user_id = app.user_id`. `app.user_id` is only ever set **transaction-local** (`set_config(..., true)`) by the identity module, after the user is authenticated (session cookie or a redeemed code / refresh token). Today only `identity.memberships` has it. The SQL comes from `UserRowLevelSecurity` in `Trekky.Persistence.Rls` and is never hand-written. Writes still require `app.org_id`; sign-up sets `app.org_id` transaction-local to the new organization's id while it creates that organization and its Owner membership.

## Consequences

- Login and refresh can resolve `org_id` and `role` from current data without any role that bypasses RLS.
- A second RLS setting exists; misuse would let code read one user's memberships across organizations. Mitigations: SELECT only, transaction-local only, one table, and integration tests (`IdentityTenantIsolationTests`) that prove it cannot read organizations or write memberships.
- Any further table that wants this policy needs a new ADR.
- When users can belong to several organizations (v1.1), the same lookup lists their organizations for an org switcher.

## Alternatives considered

- **A `BYPASSRLS` login role for identity lookups** — a second connection string with full read access to every tenant; too broad.
- **`SECURITY DEFINER` function owned by `trekky_owner`** — blocked by `FORCE ROW LEVEL SECURITY`; would need the owner to bypass RLS, which ADR-0002 rules out.
- **Store `org_id` on the user row** — duplicates memberships and breaks once a user joins a second organization.
