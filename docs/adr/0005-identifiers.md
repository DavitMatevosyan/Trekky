# ADR-0005: Identifiers — UUIDv7 internally, human keys externally

- **Status:** Agreed
- **Date:** 2026-10-06 (agreed in planning, 2026-10-05)
- **Deciders:** Davit, Claude

## Context

Every entity needs a stable identifier that is safe to expose, cheap to index and generated without a database round trip. People and AI agents also need short, readable references for work items (`TRK-42`) in branches, commits, comments and docs.

## Decision

- **Internal IDs:** every entity's primary key is a **UUIDv7** generated in the application with `Guid.CreateVersion7()` and stored as Postgres `uuid`. APIs use these IDs in URLs and payloads.
- **Project key:** 2–10 characters, uppercase letters and digits, starting with a letter (`^[A-Z][A-Z0-9]{1,9}$`). Unique per organization, chosen at creation and **immutable**.
- **Work item key:** `<PROJECT_KEY>-<number>`, e.g. `TRK-42`. The number is per project, starts at 1 and is never reused.
  - Numbers come from a counter row per project in the work-items module (`work_items.item_counters`), incremented with `UPDATE … RETURNING` in the same transaction that inserts the item, so numbers are gapless and safe under concurrency.
  - The work item stores its own copy of the project key and number, so it never needs to read the projects module to show its key.
  - Lookup by key: `GET /api/v1/work-items/by-key/{key}`.
- Keys are for display and lookup; relationships always use UUIDs.

## Consequences

- Time-ordered UUIDs keep B-tree inserts mostly sequential and give a rough creation order for free.
- IDs can be created before saving, which simplifies outbox events and tests.
- The per-project counter row serializes item creation within one project; that is fine at our scale.
- Moving an item to another project is out of scope; if added later, the item gets a new key and keeps the old one as an alias.

## Alternatives considered

- **Bigint identity columns** — compact, but leak volume, need a round trip and collide across tenants in exports.
- **UUIDv4** — random inserts fragment indexes.
- **Postgres `uuidv7()`** — only in Postgres 18; we are on 17 and generating in .NET works everywhere.
- **Postgres sequences per project** — one sequence object per project is awkward to manage and not transactional (gaps on rollback).
