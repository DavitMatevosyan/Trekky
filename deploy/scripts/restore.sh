#!/usr/bin/env bash
# Restores a backup into a database of the running production stack. Run from deploy/:
#
#   scripts/restore.sh backups/trekky-20261026T023000Z.dump trekky_restore   # drill: a new, separate database
#   scripts/restore.sh backups/trekky-20261026T023000Z.dump trekky           # disaster recovery (see README)
#
# The target database must not exist yet; it is created owned by trekky_owner. The roles come from the Postgres
# init scripts, so a brand-new VM restores into a freshly started stack. Prints exact row counts per table.
set -euo pipefail

dump="${1:?usage: scripts/restore.sh <dump file under deploy/backups> <target database>}"
target="${2:?usage: scripts/restore.sh <dump file under deploy/backups> <target database>}"
[ -f "$dump" ] || { echo "restore: $dump not found" >&2; exit 1; }
[ "$(basename "$(dirname "$dump")")" = backups ] || { echo "restore: the dump must be in deploy/backups/" >&2; exit 1; }
[[ "$target" =~ ^[a-z_][a-z0-9_]*$ ]] || { echo "restore: invalid database name $target" >&2; exit 1; }

in_backup() { docker compose exec -T backup "$@"; }

if [ "$(in_backup psql -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname = '$target'")" = 1 ]; then
  echo "restore: database $target already exists; drop it first or choose another name" >&2
  exit 1
fi

in_backup psql -v ON_ERROR_STOP=1 -d postgres -c "CREATE DATABASE $target OWNER trekky_owner"
in_backup psql -v ON_ERROR_STOP=1 -d "$target" -c "GRANT CONNECT ON DATABASE $target TO trekky_app"
if ! in_backup pg_restore --exit-on-error --dbname="$target" "/backups/$(basename "$dump")"; then
  echo "restore: FAILED; drop the half-restored database before retrying:" >&2
  echo "  docker compose exec backup psql -d postgres -c \"DROP DATABASE $target\"" >&2
  exit 1
fi

echo "restore: done. Row counts in $target (compare with: scripts/row-counts.sh <source database>):"
"$(dirname "$0")/row-counts.sh" "$target"
