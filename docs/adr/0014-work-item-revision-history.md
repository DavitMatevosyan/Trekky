# ADR-0014: Keep every revision of a work item in a Postgres table

- **Status:** Proposed
- **Date:** 2026-11-05
- **Deciders:** Davit, Claude

## Context

The activity log (`docs/tech/activity-events.md`, ADR-0011) records *what changed*: field diffs such as `title: from → to`. It does not record *what the item was*. A description edit stores only `{ "changed": true }`, so the old text is lost, and there is no way to show an item as it was. Davit asked for an item's whole history to be kept.

The constraints:
- It is a POC built about 1 hour a week, with no paid services and one Postgres in Docker Compose.
- It is open core, so it must self-host with nothing extra.
- Every tenant table needs `org_id` + RLS (ADR-0002).
- A change and its event commit in one transaction.
- Events stay small, because notifications, webhooks and the AI context layer read the same stream.

## Decision

Every revision of a work item is a full JSONB snapshot in an append-only table, `work_items.work_item_revisions`, in the existing Postgres. The work items module writes it in the same transaction as the change and its activity event. There is no separate non-relational database. The rules:

- **Columns:** `work_item_id` and `revision` (PK), `org_id`, `event_id` (null only for a backfilled baseline), `actor_type` and `actor_id` (copied from the event's actor), `changed_at`, `snapshot_version` (starts at 1), and `snapshot jsonb`. The snapshot holds every stored field except `rank` and the `xmin` row version (the ETag). That includes `description_md` and `custom`. There is no foreign key to `work_items` and no cascade. Index: `(org_id, work_item_id, revision desc)`.
- **Revision numbers:** `work_items.current_revision` is incremented in the same UPDATE, under the row lock that edits already take (`ItemLocks`), so two edits never get the same number. Create writes revision 1. After that, every change that records an activity event writes a revision. A no-op edit and a `rank` move write nothing.
- **Append-only:** RLS comes from `EnableTenantRowLevelSecurity`. The migration revokes `UPDATE`, `DELETE` and `TRUNCATE` from `trekky_app`, as the activity migration does. A retention purge or GDPR delete (v2) runs as `trekky_owner`, never by granting DELETE back.
- **Existing items:** the migration copies each item's current row into revision 1 with `event_id` null (a baseline, not the create state) and sets `current_revision = 1`. It never reads the activity schema.
- **Events stay diffs.** Item events gain an optional `revision` payload field (additive, so still `schemaVersion` 1), so a history entry can link to its snapshot.
- **API:** `GET /api/v1/work-items/{id}/revisions` (cursor-paged) and `/revisions/{n}`. A revision number is not the ETag; edits still use `If-Match`.

## Consequences

**Easier:**
- An item can be shown at any revision.
- Old descriptions can be read and diffed, and later restored.
- The AI context gets full text history.
- It is still one database, one backup and one transaction, with RLS like everything else.

**Harder:**
- Every change writes one more row.
- Storage grows with description size × number of edits. Postgres compresses large JSONB (TOAST), and deletion follows the tenant retention and purge rules (data-model.md, GDPR delete in v2).
- The snapshot shape needs `snapshot_version` as fields arrive, for example the assignee in TRK-20.

**Next:**
- Implementing this needs a TRK key from Davit (migration with backfill, writes in `WorkItemService`, the API, tests).
- Once the table exists, TRK-14 can link history entries to revisions.
- Comments can follow the same pattern with TRK-19.

## Alternatives considered

- **Events only, with the item rebuilt by replaying them.** Description text is still lost, and replay gets fragile as event schemas evolve.
- **A full snapshot in every event.** It bloats the outbox and the shared stream, and it mixes "what changed" with "what it was".
- **A trigger that copies the old row on UPDATE.** It hides logic from the domain. It cannot see the actor or the event without extra session settings, and it is harder to test and to migrate with EF Core.
- **Postgres temporal extensions (`temporal_tables`, `periods`).** They are not in the stock `postgres:17` image or on every host.
- **A separate non-relational store (MongoDB, EventStoreDB, DynamoDB).**
  - It is one more service to run, back up and secure.
  - It has no RLS, which goes against ADR-0002.
  - It cannot commit in our transaction, so it would need outbox delivery and would only be eventually consistent.
  - JSONB already covers document storage.
  - Revisit only if volume outgrows Postgres.
- **Event-sourcing the work item.** It is a rewrite, far beyond the smallest change.
