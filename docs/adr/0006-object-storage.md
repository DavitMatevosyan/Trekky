# ADR-0006: Object storage — remove MinIO, code against the S3 API, choose the server with attachments

- **Status:** Agreed (removal) · Proposed (replacement)
- **Date:** 2026-10-23
- **Deciders:** Davit, Claude
- **Supersedes:** the MinIO parts of ADR-0004 (the `minio` service and "MinIO via the S3 API")

## Context

ADR-0004 and the planning baseline chose MinIO for attachments, locally and on the POC VM. Since then MinIO's community edition has ended:

- October 2025: MinIO stopped publishing free binaries and images; the community repository was later marked unmaintained and archived.
- September 2026: the `minio/minio` and `minio/mc` images were deleted from Docker Hub, so `docker compose up` fails with "pull access denied for minio/minio".
- The last community build (2025-09-07) gets no security fixes, and copies on other registries are not a dependable source.

Nothing in Trekky stores files yet. Attachments are not part of v0.1–v1.0.

## Decision

1. **Remove MinIO now.** No object-storage service in `docker-compose.yml`, no storage settings, until a TRK needs files. (Agreed.)
2. **Code only against the S3 API** when attachments arrive (AWS SDK for .NET with a configurable endpoint), so the storage server is a deployment choice, not a code dependency. (Agreed.)
3. **Replacement — recommended, confirm when the attachments TRK is planned:** (Proposed.)
   - **Local development and self-hosting: SeaweedFS** (`chrislusf/seaweedfs`, pinned version) with its S3 gateway.
   - **POC production:** deferred together with hosting (ADR-0004, deferred 2026-10-30); choose an S3-compatible store with the host.

## Consequences

- `docker compose up` works again and runs only what the app uses (Postgres, Mailpit).
- No storage code or configuration to maintain until it is needed.
- The attachments TRK must: confirm the replacement in this ADR (or supersede it), add the service to Compose, and add an integration test against the chosen server.
- The free MinIO console disappears from the local setup; SeaweedFS has a plainer UI.

## Alternatives considered

| Option | Why not (now) |
| --- | --- |
| Pin the last MinIO community image from another registry | Unmaintained, no security fixes, availability not guaranteed. |
| MinIO AIStor (commercial) free tier | Licence terms and telemetry; ties self-hosters to a vendor. |
| Garage | Lightweight and solid, but AGPL-3.0 and fewer S3 features; a good fallback. |
| RustFS | Apache-2.0 and MinIO-like, but young; revisit when it has a longer track record. |
| Ceph RGW | Far too heavy for one VM and local development. |
| Store files in Postgres | Simple at first, but bloats the database and backups. |
