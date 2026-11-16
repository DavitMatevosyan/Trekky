# ADR-0015: Host on the home machine behind Tailscale Funnel; Azure Container Apps + Neon later

- **Status:** Proposed
- **Date:** 2026-11-13
- **Deciders:** Davit, Claude
- **Supersedes:** ADR-0004

## Context

ADR-0004 chose an Oracle Cloud Always Free VM, but the VM could not be created, and in 2026 Oracle halved the free Arm allowance and reclaims idle VMs. The app is now built (v1.0) and has to run somewhere so Trekky can track itself (TRK-21) and the desktop app (TRK-23 to TRK-27) has a server. It is a POC worked on about an hour a week, with no paid services, and no domain bought yet. Davit decided on 2026-11-13: the home machine now, Azure Container Apps + Neon Postgres when SaaS starts (TRK-29).

## Decision

Trekky runs on the home machine as the Compose stack in `deploy/`, reachable only through Tailscale Funnel (public HTTPS on a `ts.net` name, no open ports or domain), and every part of it is built so it can move to Azure Container Apps + Neon unchanged: all settings from environment variables, certificates and keys outside the containers, nothing kept on a container's disk.

## Consequences

- **Easier:** free, no router or DNS changes, HTTPS without managing certificates; the stack runs the same on a laptop (`TREKKY_SITE_ADDRESS=localhost`) for testing.
- **Harder:** uptime follows the home machine (sleep, reboots, power) and its internet connection; the public name is `<name>.<tailnet>.ts.net` until a domain is bought; Funnel needs a Tailscale account, the Funnel node attribute in the tailnet policy, and an auth key.
- **Backups:** nightly `pg_dump` into a host folder, kept 14 days; the off-machine copy (rclone over the S3 API) is optional and should be turned on as soon as there is a bucket, because a dump on the same disk does not survive the disk.
- **Moving to Azure (TRK-29):** the images become Container Apps, `migrate` a Container Apps job, the `.env` values Container Apps secrets (the web image reads its API address from `TREKKY_API_UPSTREAM`), and Neon's direct (unpooled) connection replaces the `postgres` service, because `TenantConnectionInterceptor` sets `app.org_id` for the whole session, which a transaction-mode pooler does not keep. Data Protection keys live in a volume at the default path today; Container Apps has no persistent home folder, so TRK-29 adds `PersistKeysTo…` (Blob Storage or the database) with `SetApplicationName`. They are stored unencrypted at rest until then. Tailscale and Caddy's local site address are dropped there.
- **Open on the internet:** anyone who finds the address can sign up (which creates an organization); there is no rate limiting yet. Acceptable for the POC; revisit before sharing the address widely.
- **Follow-ups:** TRK-21 imports the task list into this instance; TRK-25 uses its URL as the desktop app's default server.

## Alternatives considered

- Oracle Cloud Always Free VM (ADR-0004): could not be created; the free allowance shrank and idle VMs are reclaimed.
- Cloudflare Tunnel: also free with no open ports, but needs a domain on Cloudflare, which there is not yet.
- Vercel (web) + Render (API, Docker) + Neon: proposed on 2026-11-13; the free Render service sleeps after 15 idle minutes (about a minute to wake), and it splits the stack across three providers before SaaS needs it.
- Azure Container Apps + Neon now: the target for SaaS, but it costs money and setup time the POC does not need yet; the stack is kept ready for it instead.
