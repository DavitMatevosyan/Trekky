# ADR-0004: POC hosting — one Oracle Cloud Always Free VM with Docker Compose

- **Status:** Deferred (2026-10-30) · was Agreed
- **Date:** 2026-10-06 (agreed in planning, 2026-10-05)
- **Deciders:** Davit, Claude

## Deferral (2026-10-30)

Hosting is on hold until the app is built. The Oracle Always Free VM could not be created, and Davit chose to build the product first and decide hosting afterwards. Until then there is no production deployment and no backups; TRK-9 is deferred.

When hosting is picked up again, choose the host anew (candidates: Oracle with a Pay As You Go account, Google Cloud e2-micro, a home machine behind Cloudflare Tunnel, or a paid VM such as Hetzner) and update this ADR. The rest of this ADR is kept as the starting point for that decision, and the production stack already built for the Oracle VM (Dockerfiles, Caddy, production Compose, backup and restore scripts) stays on the branch `TRK-9-oracle-vm-deploy` (PR #11, closed unmerged). This edit to an Agreed ADR is a one-time exception approved by Davit.

## Context

The POC has no budget for hosting and only one real user (our own team). It still needs HTTPS, a database, object storage and nightly backups, and the setup must be reproducible because the same Compose stack is the self-hosted install.

## Decision

Production for the POC is **one Oracle Cloud Always Free Ampere (ARM64) VM** running the same **Docker Compose** stack as local development, plus a reverse proxy:

| Service | Image | Purpose |
| --- | --- | --- |
| `caddy` | caddy | HTTPS (automatic Let's Encrypt), serves the built web app, proxies `/api` and `/connect` to the API |
| `api` | built from `backend/` (linux/arm64) | Trekky API |
| `postgres` | postgres:17 | Database |
| `minio` | minio/minio | Attachments (S3 API) |
| `backup` | small cron container | Nightly `pg_dump` |

- **Domain:** a free dynamic-DNS subdomain until a domain is bought (TRK-17).
- **Backups:** nightly `pg_dump -Fc`, kept 14 days, copied **off the VM** to Oracle Object Storage (S3-compatible, within the free tier). One restore is tested in TRK-9 and after any backup change.
- **Secrets:** a `.env` file on the VM (not in git), readable only by the deploy user.
- **Deploy:** SSH to the VM, `git pull`, `docker compose up -d --build`. CI/CD automation comes later.
- **Staging** runs on the same VM under a separate Compose project and subdomain until it needs its own.
- Always Free limits change over time; check current CPU, memory, storage and egress limits when creating the VM.

## Consequences

- Zero hosting cost; local, self-hosted and production use the same stack.
- Single VM = single point of failure. Acceptable for a POC; backups off the VM make it recoverable.
- **Risk:** Oracle may reclaim Always Free instances it considers idle. Mitigation: off-VM backups, a scripted setup in `deploy/`, and a restore drill.
- Images must build for ARM64; CI keeps building on x64 for tests.
- Before real customers, choose a paid host (open question in the plan).

## Alternatives considered

- **Hetzner / DigitalOcean VM** — simple and cheap, but not free.
- **Azure App Service + managed Postgres** — managed and familiar, but costs money and splits the stack from self-hosting.
- **Fly.io / Render free tiers** — free tiers change often and limit Postgres and storage.
