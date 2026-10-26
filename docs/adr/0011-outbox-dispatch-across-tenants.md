# ADR-0011: The outbox dispatcher reads pending events across tenants through a scoped RLS policy

- **Status:** Proposed
- **Date:** 2026-10-26
- **Deciders:** Davit, Claude

## Context

TRK-10 introduces the activity log (docs/tech/activity-events.md). A module writes its change and an outbox row in one transaction. A background dispatcher then copies outbox rows into `activity.activity_events`. The dispatcher has no request and no tenant: it must find pending rows of **every** organization. Every table forces row-level security (ADR-0002), even for its owner. Only the Postgres superuser bypasses it, and the API never connects as the superuser. ADR-0007 already allows one scoped exception: `user_self_read` on memberships, a read-only policy that a transaction-local setting turns on.

## Decision

Each module's `outbox` gets, next to `tenant_isolation`, a read-only `outbox_dispatch` policy: `FOR SELECT USING (processed_at IS NULL AND current_setting('app.outbox_dispatch', true) = 'on')`. Only `OutboxDispatcher` sets `app.outbox_dispatch`, transaction-local (`set_config(..., true)`), and only to read pending rows. It delivers each event in a scope whose tenant is the event's `org_id`, and marks the row processed as that tenant, so every write still goes through `tenant_isolation`.

## Consequences

- One dispatcher for all modules, as the spec describes. It is registered by `AddModuleOutbox(schema)`, and the migration helper `EnableOutboxRowLevelSecurity(schema)` adds both policies, so the policy is never hand-written.
- The exception is narrow:
  - read-only;
  - only rows that are not yet processed, so delivered events vanish from it;
  - only outbox tables, never module data;
  - only while the setting is on, inside one transaction.

  Integration tests check that processed rows stay hidden and that the setting allows no writes.
- **Risk:** any code running as `trekky_app` could set the setting and read other tenants' pending events. That is the same trust as ADR-0007's `app.user_id`. Code review keeps `app.outbox_dispatch` out of everything except the dispatcher; `grep` finds it.
- Delivery is at least once. The activity sink ignores an event id it already has (`ON CONFLICT DO NOTHING`).
- There is a single dispatcher, with no `FOR UPDATE SKIP LOCKED`: locking would need UPDATE rights across tenants. When the API runs on more than one instance, duplicates are already harmless, but a lease or advisory lock should be added then.
- `activity.activity_events` is append-only for the app role (UPDATE, DELETE and TRUNCATE are revoked in its migration).

## Alternatives considered

- **Dispatch as a `BYPASSRLS` role (a separate connection string).** Simpler SQL, but it adds a role with unlimited cross-tenant access to every table to the running API. That is a much larger exception than one read policy on outbox rows.
- **Dispatch only after commit, inside the request (the tenant is known).** No cross-tenant read, but an event left behind by a crash would wait until that organization's next request, possibly forever, which breaks "history lags by under a second".
- **Writing `activity_events` directly in the module's transaction.** No outbox and no dispatcher, but every module would write into another module's schema (ADR-0001), and later consumers (notifications, webhooks) would have no queue to read.
- **An SQL function `SECURITY DEFINER` owned by `trekky_owner`.** The owner is subject to forced RLS too, so it would still need `BYPASSRLS`. It moves the problem without solving it.
