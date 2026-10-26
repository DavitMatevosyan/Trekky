#!/usr/bin/env bash
# One backup: pg_dump (custom format) to /backups, prune local copies older than BACKUP_RETENTION_DAYS, then copy
# off the VM when BACKUP_REMOTE is set (an rclone path such as `offsite:trekky-backups`).
# Connects as the Postgres superuser: tables FORCE row-level security, so only a superuser sees every tenant's rows.
set -euo pipefail
# Dumps hold every tenant's data and password hashes: readable by root (this container) only.
umask 077

retention_days="${BACKUP_RETENTION_DAYS:-14}"
[[ "$retention_days" =~ ^[1-9][0-9]*$ ]] || { echo "backup: BACKUP_RETENTION_DAYS must be at least 1" >&2; exit 1; }
file="/backups/trekky-$(date -u +%Y%m%dT%H%M%SZ).dump"
trap 'rm -f "${file}.partial"' ERR

pg_dump --format=custom --file="${file}.partial" "${PGDATABASE}"
mv "${file}.partial" "$file"
echo "backup: wrote $file ($(du -h "$file" | cut -f1))"

find /backups -name 'trekky-*.dump' -type f -mtime "+$((retention_days - 1))" -print -delete

if [ -n "${BACKUP_REMOTE:-}" ]; then
  rclone copy "$file" "$BACKUP_REMOTE"
  rclone delete --min-age "${retention_days}d" --include 'trekky-*.dump' "$BACKUP_REMOTE"
  echo "backup: copied to $BACKUP_REMOTE"
else
  echo "backup: WARNING BACKUP_REMOTE is not set, the backup exists only on this VM" >&2
fi
