#!/usr/bin/env bash
# Exact row count of every Trekky table in a database of the running stack. Run from deploy/:
#   scripts/row-counts.sh trekky
# Runs as the Postgres superuser, which bypasses row-level security, so it counts every tenant's rows.
set -euo pipefail

database="${1:?usage: scripts/row-counts.sh <database>}"

docker compose exec -T backup psql -v ON_ERROR_STOP=1 -d "$database" -c "
  SELECT table_schema AS schema, table_name AS table,
         (xpath('/row/c/text()', query_to_xml(format('SELECT count(*) AS c FROM %I.%I', table_schema, table_name),
                                              false, true, '')))[1]::text::bigint AS rows
  FROM information_schema.tables
  WHERE table_type = 'BASE TABLE' AND table_schema NOT IN ('pg_catalog', 'information_schema', 'public')
  ORDER BY 1, 2"
