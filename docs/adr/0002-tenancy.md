# ADR-0002: Multi-tenancy — shared database, `org_id` + Postgres row-level security

- **Status:** Agreed
- **Date:** 2026-10-06 (agreed in planning, 2026-10-05)
- **Deciders:** Davit, Claude

## Context

Trekky is multi-tenant from day one: every organization's data must be invisible to every other organization, including to bugs in our own queries. The POC runs one Postgres instance on one VM, and the same code must also run single-tenant when self-hosted.

## Decision

All tenants share one database and one set of module schemas. **Every tenant-owned table has a non-null `org_id uuid`**, and Postgres **row-level security** enforces it:

```sql
ALTER TABLE <schema>.<table> ENABLE ROW LEVEL SECURITY;
ALTER TABLE <schema>.<table> FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON <schema>.<table>
  USING      (org_id = current_setting('app.org_id', true)::uuid)
  WITH CHECK (org_id = current_setting('app.org_id', true)::uuid);
```

- **Fail closed:** `current_setting(..., true)` returns NULL when no tenant is set, so a query without a tenant sees no rows and cannot write.
- **Setting the tenant:** an EF Core connection interceptor runs `SELECT set_config('app.org_id', @orgId, false)` when a connection opens for a request, using the `org_id` claim from the access token. Npgsql resets session state when a connection returns to the pool, so the value cannot leak to the next request.
- **Roles:** the API connects as `trekky_app` (no superuser, no `BYPASSRLS`, not the table owner). Migrations run as `trekky_owner`. Tables are owned by `trekky_owner`.
- **Sign-up and other pre-tenant work** (creating the organization itself) run in a narrow, explicit path that sets `app.org_id` to the new organization's id before inserting tenant rows.
- `WHERE org_id = …` in application queries is allowed for clarity and indexes, but RLS is the guarantee.

## Consequences

- One isolation mechanism for every module; a forgotten filter returns nothing instead of another tenant's data.
- Every tenant table needs an index starting with `org_id`.
- Migrations must create the policy together with the table (the `new-module` skill includes it).
- TRK-6 must include an integration test that: (1) org A cannot read or write org B's rows, (2) no rows are visible without a tenant, (3) a pooled connection reused by a second request does not keep the first request's tenant.
- Background jobs must set the tenant explicitly per unit of work.

## Alternatives considered

- **Database per tenant** — strongest isolation, but many databases to migrate and back up; overkill for the POC.
- **Schema per tenant** — conflicts with schema-per-module and multiplies migrations.
- **Application filters only (EF global query filters)** — one missed filter or raw SQL query leaks data. May be added later as a convenience, never as the guarantee.
