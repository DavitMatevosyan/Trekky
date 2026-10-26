#!/usr/bin/env bash
# Production roles (ADR-0002), run once by the Postgres entrypoint on an empty volume. Creates the roles with the
# shared script (same grants as local development and tests), then replaces its public dev passwords with the ones
# from deploy/.env.
set -euo pipefail

: "${TREKKY_OWNER_PASSWORD:?TREKKY_OWNER_PASSWORD must be set in deploy/.env}"
: "${TREKKY_APP_PASSWORD:?TREKKY_APP_PASSWORD must be set in deploy/.env}"

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" -f /trekky-init/01-roles.sql

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  -v owner_password="$TREKKY_OWNER_PASSWORD" -v app_password="$TREKKY_APP_PASSWORD" <<'SQL'
ALTER ROLE trekky_owner PASSWORD :'owner_password';
ALTER ROLE trekky_app PASSWORD :'app_password';
SQL
