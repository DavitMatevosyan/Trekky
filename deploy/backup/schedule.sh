#!/usr/bin/env bash
# Runs backup-once.sh every day at BACKUP_TIME (HH:MM, in TZ). A failed run is logged and retried the next night;
# check `docker compose logs backup`.
set -uo pipefail

BACKUP_TIME="${BACKUP_TIME:-02:30}"
if ! [[ "$BACKUP_TIME" =~ ^([01][0-9]|2[0-3]):[0-5][0-9]$ ]]; then
  echo "backup: invalid BACKUP_TIME '${BACKUP_TIME}', expected HH:MM" >&2
  exit 1
fi
echo "backup: scheduled daily at ${BACKUP_TIME} (${TZ:-UTC})"

# Stop promptly on `docker compose down` instead of waiting for SIGKILL.
trap 'exit 0' TERM

while true; do
  now=$(date +%s)
  next=$(date -d "today ${BACKUP_TIME}" +%s)
  if [ "$next" -le "$now" ]; then
    next=$(date -d "tomorrow ${BACKUP_TIME}" +%s)
  fi
  sleep $((next - now)) &
  wait $!
  /usr/local/bin/backup-once.sh || echo "backup: FAILED at $(date -Iseconds)" >&2
done
