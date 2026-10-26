# Deploying Trekky (POC: one Oracle Cloud VM)

The production stack from ADR-0004: one Oracle Cloud Always Free ARM64 VM running `deploy/docker-compose.yml`.

| Service | What it does |
| --- | --- |
| `web` | Caddy: HTTPS via Let's Encrypt, HTTP → HTTPS redirect, serves the built React app, proxies `/api`, `/connect` and `/.well-known/{openid-configuration,jwks}` to the API on the same origin (ADR-0010) |
| `api` | Trekky API (`backend/Dockerfile`), as `trekky_app` under RLS |
| `migrate` | Same image, `--migrate`: applies EF Core migrations as `trekky_owner`, then exits; the API starts after it succeeds |
| `postgres` | Postgres 17, not published outside the Docker network; roles created on first start (`postgres/10-roles.sh`) |
| `backup` | Nightly `pg_dump -Fc` into `deploy/backups/`, 14 days kept, copied off the VM with rclone (S3 API) |

## First setup on the VM

1. **VM:** Ubuntu 24.04 on an Ampere A1 shape. Check the current Always Free limits when creating it. In the VCN security list, open TCP 80 and 443 (and UDP 443 for HTTP/3). Also open them in the VM firewall: `sudo iptables -I INPUT -p tcp -m multiport --dports 80,443 -j ACCEPT`, then `sudo netfilter-persistent save`.
2. **DNS:** point an A record for your (dynamic-DNS) host name at the VM's public IP. Let's Encrypt only issues the certificate once the name resolves to the VM.
3. **Docker:** install Docker Engine with the Compose plugin (docs.docker.com/engine/install/ubuntu) and add the deploy user to the `docker` group.
4. **Code:** `git clone https://github.com/DavitMatevosyan/Trekky.git trekky && cd trekky && git switch main`.
5. **Settings:** `cd deploy && cp .env.example .env && chmod 600 .env`, then fill it in:
   - `TREKKY_DOMAIN` = the host name from step 2.
   - New random passwords (`openssl rand -base64 32 | tr -d '/+='`).
   - The off-site bucket (below).
6. **Certificates** for token signing and encryption: `scripts/generate-certificates.sh`, then the `sudo chgrp` command it prints. The files go to `deploy/secrets/` (ignored by git, mode 640), readable only by you and the API container's user. Keep a copy somewhere safe: losing them logs everyone out.
   - Create the backup folder up front, so it isn't created world-readable: `mkdir -m 700 backups && sudo chown root:root backups`.
7. **Start:** `docker compose up -d --build`. The first build on the VM takes a few minutes.
8. **Check:**
   - `docker compose ps`: `migrate` has exited with 0, and the other services are running.
   - `curl https://$TREKKY_DOMAIN/api/v1/health` returns `{"status":"ok"}`.
   - `curl -I http://$TREKKY_DOMAIN` returns `308` to https.
   - From a laptop, run the e2e suite against the live site: `cd frontend && E2E_BASE_URL=https://<domain> npm run e2e`. It signs up a throwaway `e2e-…@example.com` user.

### Off-VM backup copy (Oracle Object Storage)

1. Create a private bucket, e.g. `trekky-backups`.
2. Under your user, open **Customer Secret Keys** and generate a key. The key and its secret are the S3 access key ID and secret.
3. In `.env` set:
   - `BACKUP_REMOTE=offsite:trekky-backups`
   - `OFFSITE_S3_ENDPOINT=https://<namespace>.compat.objectstorage.<region>.oraclecloud.com`
   - `OFFSITE_S3_REGION=<region>`
   - the two keys.
4. Apply with `docker compose up -d backup`, then test once: `docker compose exec backup backup-once.sh`. The log must end with `backup: copied to offsite:trekky-backups`. Without a remote it warns every night that the backup exists only on the VM.

## Deploy a new version

```bash
cd ~/trekky && git pull && cd deploy && docker compose up -d --build
```

`migrate` applies new migrations before the new API starts. Users stay logged in across deploys: the session-cookie keys live in the `dataprotection-keys` volume (stored unencrypted on that volume; the API logs a warning about it once). Signed-in tabs keep working, because the token certificates don't change.

## Backups

- **When:** every night at `BACKUP_TIME` (`TZ`, default 02:30 UTC). Check `docker compose logs backup`; a failed night logs `backup: FAILED` and the next night tries again.
- **Where:** `deploy/backups/trekky-<UTC timestamp>.dump` on the VM (owned by root, mode 600, because a dump holds every tenant's data), and the same file in the bucket. Both keep `BACKUP_RETENTION_DAYS` (14).
- **On demand:** `docker compose exec backup backup-once.sh`, e.g. before a risky migration.
- **What:** the whole database via `pg_dump` as the Postgres superuser, because tables force row-level security, so only a superuser sees every tenant. Roles are not in the dump; the Postgres init scripts create them on any new stack.

## Restore

**Drill** (do it after any backup change, and from time to time): restore into a new database next to production, and compare row counts.

```bash
cd deploy
scripts/restore.sh backups/<file>.dump trekky_restore
scripts/row-counts.sh trekky              # compare with the counts restore.sh printed
docker compose exec backup psql -d postgres -c "DROP DATABASE trekky_restore"
```

To restore from the bucket, first copy the file down: `docker compose exec backup rclone copy offsite:trekky-backups/<file>.dump /backups`.

**Disaster recovery** (new VM, or the database is lost):

1. Set up the VM as above with the same `.env` and the saved `deploy/secrets/`. Then `docker compose up -d postgres backup`; this gives a fresh database with roles.
2. Put the dump in `deploy/backups/`, then empty the fresh database: `docker compose exec backup psql -d postgres -c "DROP DATABASE trekky"`.
3. `scripts/restore.sh backups/<file>.dump trekky`.
4. `docker compose up -d` (migrate finds nothing to apply, then the API and web start).

### Restore log

| Date | Where | Backup | Result |
| --- | --- | --- | --- |
| 2026-10-26 | Local production stack, fresh volumes (TRK-9, `TREKKY_DOMAIN=localhost`) | `trekky-20261026T151341Z.dump` (36 KB) | Restored into empty `trekky_restore`; exact row counts of all 11 tables matched the source. In an earlier drill the same day, `trekky_app` connected to the restored database and RLS was still forced (0 rows without a tenant) |
| _to do_ | Oracle VM, after the first nightly backup | | |

## Try the production stack locally

Set `TREKKY_DOMAIN=localhost` in `deploy/.env`; Caddy then uses its own local CA (the browser warns once) and skips HSTS. Only the production stack publishes ports 80/443, so it can run next to the dev stack (which publishes 5432, 1025 and 8025). Run `docker compose up -d --build` in `deploy/`, then `E2E_BASE_URL=https://localhost npm run e2e` in `frontend/`. On Windows Git Bash, prefix the scripts with `MSYS_NO_PATHCONV=1`.

## Not yet

- **Staging** (ADR-0004: same VM, separate Compose project and subdomain) is not set up yet. Plan: `docker compose -p trekky-staging --env-file .env.staging up -d`, with a different host name. Caddy needs one shared entry point for both, so that arrives with staging.
- **Deploy from CI** comes later (ADR-0004). Until then, deploys are by SSH, as above.
- **Backup alerts:** a failed night shows only in `docker compose logs backup`. Check the log or the bucket now and then, until there is monitoring (v1.1: logs + Sentry).
