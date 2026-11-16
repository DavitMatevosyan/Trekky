# Deploying Trekky (home host behind Tailscale Funnel)

The production stack from ADR-0015: `deploy/docker-compose.yml` on the home machine (Windows with Docker Desktop, or
any Linux box with Docker), public over HTTPS at `https://<TS_HOSTNAME>.<tailnet>.ts.net` through Tailscale Funnel. No
router ports are opened and no domain is needed.

| Service | What it does |
| --- | --- |
| `tailscale` | Tailscale Funnel (`--profile funnel`): the public HTTPS name, proxied to `web` |
| `web` | Caddy: serves the built React app and proxies `/api`, `/connect` and `/.well-known/{openid-configuration,jwks}` to the API on the same origin (ADR-0010); security headers |
| `api` | Trekky API (`backend/Dockerfile`), as `trekky_app` under RLS |
| `migrate` | Same image, `--migrate`: applies EF Core migrations as `trekky_owner`, then exits; the API starts after it succeeds |
| `postgres` | Postgres 17, not published outside the Docker network; roles created on first start (`postgres/10-roles.sh`) |
| `backup` | Nightly `pg_dump -Fc` into `deploy/backups/`, 14 days kept, optionally copied off the machine (rclone, S3 API) |

Everything that must survive a rebuild lives outside the containers: the database, Data Protection keys, Caddy and
Tailscale state in named volumes; certificates in `deploy/secrets/`; backups in `deploy/backups/`; settings in
`deploy/.env`. That is what lets the same images move to Azure Container Apps + Neon later (TRK-29).

## First setup

1. **Docker:** Docker Desktop (Windows: WSL 2 backend) or Docker Engine with the Compose plugin. Stop the machine from
   sleeping while it serves Trekky. Docker Desktop starts only when you sign in to Windows ("Start Docker Desktop when you
   sign in"), so after a reboot (e.g. Windows Update) Trekky is down until someone signs in; Windows automatic sign-in,
   or Docker Engine inside WSL started as a service, avoids that.
2. **Code:** `git clone https://github.com/DavitMatevosyan/Trekky.git trekky && cd trekky && git switch main`.
3. **Tailscale** (once, in the admin console at login.tailscale.com):
   - Turn on **MagicDNS** and **HTTPS certificates** (DNS page).
   - Allow Funnel: in the access control policy add the `funnel` node attribute, e.g.
     `"nodeAttrs": [{ "target": ["autogroup:member"], "attr": ["funnel"] }]`.
   - Create an **auth key** (Settings → Keys): not ephemeral (the node must survive being offline) and not tagged (the
     `nodeAttrs` above targets members' devices; a tagged node would not get `funnel`). The container uses it on its first
     start only.
   - After the first start (step 6), open the `trekky` machine in the admin console and **disable key expiry**: otherwise
     the node is logged out after the tailnet's key expiry (180 days by default) and the public address stops working.
   - Your tailnet name is on the DNS page (`<tailnet>.ts.net`); the public address is `<TS_HOSTNAME>.<tailnet>.ts.net`.
4. **Settings:** `cd deploy && cp .env.example .env`, keep it private (Linux: `chmod 600 .env`), then fill it in:
   - `TREKKY_HOST` = `<TS_HOSTNAME>.<tailnet>.ts.net`, `TREKKY_SITE_ADDRESS=:80`, `TS_AUTHKEY` = the key from step 3.
   - New random passwords (`openssl rand -base64 32 | tr -d '/+='`).
5. **Certificates** for token signing and encryption: `scripts/generate-certificates.sh` (Git Bash on Windows: prefix
   with `MSYS_NO_PATHCONV=1`; on Linux, run the `sudo chgrp` command it prints). The files go to `deploy/secrets/`
   (ignored by git). Keep a copy somewhere safe: losing them logs everyone out.
   - Linux only: create the backup folder up front so it isn't world-readable: `mkdir -m 700 backups && sudo chown root:root backups`.
6. **Start:** `docker compose --profile funnel up -d --build`. The first build takes a few minutes.
7. **Check:**
   - `docker compose ps`: `migrate` exited with 0, the others are running.
   - `docker compose logs tailscale` shows the node logged in; the machine appears in the Tailscale admin console.
   - `curl https://<TREKKY_HOST>/api/v1/health` from any network returns `{"status":"ok"}`.
   - `curl https://<TREKKY_HOST>/.well-known/openid-configuration` shows `"issuer": "https://<TREKKY_HOST>/"` (the login
     server must see the public name through Funnel; if it shows another name, sign-in fails).
   - If `tailscale` cannot reach `web` (502 from the public address), switch to the shared-network setup: give `web`
     `network_mode: service:tailscale` and proxy to `http://127.0.0.1:80` in `tailscale/serve.json`.
   - Run the e2e suite against it: `cd frontend && E2E_BASE_URL=https://<TREKKY_HOST> npm run e2e`. It signs up throwaway
     `e2e-…@example.com` users; delete them afterwards (see the clean-run rule in CLAUDE.md).

## Deploy a new version

```bash
cd trekky && git pull && cd deploy && docker compose --profile funnel up -d --build
```

`migrate` applies new migrations before the new API starts. Users stay logged in across deploys: the session-cookie
keys live in the `dataprotection-keys` volume, and the token certificates don't change.

## Backups

- **When:** every night at `BACKUP_TIME` (`TZ`, default 02:30 UTC), while the machine is on. Check
  `docker compose logs backup`; a failed or missed night logs `backup: FAILED` or nothing, and the next night tries again.
- **Where:** `deploy/backups/trekky-<UTC timestamp>.dump`, kept `BACKUP_RETENTION_DAYS` (14). A dump holds every
  tenant's data and password hashes: keep the folder private.
- **Off the machine:** a dump on the same disk does not survive the disk. Set `BACKUP_REMOTE` and the `OFFSITE_S3_*`
  values to any S3-compatible bucket, apply with `docker compose up -d backup`, and test once with
  `docker compose exec backup backup-once.sh`; the log must end with `backup: copied to …`.
- **On demand:** `docker compose exec backup backup-once.sh`, e.g. before a risky migration.
- **What:** the whole database via `pg_dump` as the Postgres superuser, because tables force row-level security, so only
  a superuser sees every tenant. Roles are not in the dump; the Postgres init scripts create them on any new stack.

## Restore

**Drill** (after any backup change, and from time to time): restore into a new database next to production, and
compare row counts.

```bash
cd deploy
scripts/restore.sh backups/<file>.dump trekky_restore
scripts/row-counts.sh trekky              # compare with the counts restore.sh printed
docker compose exec backup psql -d postgres -c "DROP DATABASE trekky_restore"
```

**Disaster recovery** (new machine, or the database is lost):

1. Set up as above with the same `.env` and the saved `deploy/secrets/`, then `docker compose up -d postgres backup`;
   this gives a fresh database with roles.
2. Put the dump in `deploy/backups/`, then empty the fresh database: `docker compose exec backup psql -d postgres -c "DROP DATABASE trekky"`.
3. `scripts/restore.sh backups/<file>.dump trekky`.
4. `docker compose --profile funnel up -d` (migrate finds nothing to apply, then the API, web and Funnel start).

### Restore log

| Date | Where | Backup | Result |
| --- | --- | --- | --- |
| 2026-10-26 | Local production stack, fresh volumes (TRK-9 first attempt, Oracle plan) | `trekky-20261026T151341Z.dump` (36 KB) | Restored into empty `trekky_restore`; exact row counts of all 11 tables matched |
| 2026-11-13 | Local production stack on Windows (Docker Desktop), laptop mode (TRK-9, home host plan) | `trekky-20261113T102143Z.dump` (156 KB, after a full e2e run) | Restored into empty `trekky_restore`; exact row counts of all 21 tables matched; `trekky_app` without a tenant saw 0 work items (RLS still forced) |
| _to do_ | Home host, after the first nightly backup | | |

## Try the production stack locally

In `deploy/.env` set `TREKKY_HOST=localhost:8443` and `TREKKY_SITE_ADDRESS=localhost`; Caddy then serves HTTPS with its
own local CA on `https://localhost:8443` (the browser warns once; HSTS is skipped). Start without the funnel profile:
`docker compose up -d --build`, then `E2E_BASE_URL=https://localhost:8443 npm run e2e` in `frontend/`. The stack only
publishes loopback ports 8080/8443, so it runs next to the dev stack. On Windows Git Bash, prefix the scripts with
`MSYS_NO_PATHCONV=1`.

## Not yet

- **Staging** is not set up; there is one stack.
- **Deploy from CI** comes with Azure (TRK-29). Until then, deploys are `git pull` + `docker compose up` on the machine.
- **Monitoring:** a failed backup or a stopped machine shows only in `docker compose logs`; check now and then until
  there are logs + Sentry (v1.1).
