# Notion import (TRK-21)

Moves Trekky's own task list from Notion into a running Trekky, so Trekky tracks itself (v1.0 Dogfood).

- `tasks.json` is a snapshot of the Notion page "Project Management - Task tracker", section Tasks. Each section becomes an
  Epic, each TRK a Task (the known issue a Bug) under it, in its Notion status.
- It imports into project `TREK`, so `TRK-n` keeps meaning the Notion item (branches, commits) and new work is
  `TREK-n`. Trekky numbers items itself, so the Notion key goes first in the title (`TRK-6.1 · Remove MinIO from the local stack`):
  searching the list for an old key finds its item. The description ends with where it came from.
- Running it again is safe: items are found by their Notion key (by title for the epics and the bug), never created
  twice, and statuses only move forward. Renaming an imported item in Trekky is fine; keep its `TRK-x · ` prefix.

## Run it

Needs Node 22.18 or later (it runs the TypeScript directly) and a Trekky account to import into; the project `TREK` is
created if it does not exist.

```bash
cd tools/notion-import
export TREKKY_EMAIL=you@example.com
read -s -p "Trekky password: " TREKKY_PASSWORD && export TREKKY_PASSWORD   # kept out of the shell history
node import.ts --dry-run   # what it would do
node import.ts
```

| Variable | Default | |
| --- | --- | --- |
| `TREKKY_URL` | `http://localhost:5080` | The API |
| `TREKKY_REDIRECT_URI` | `http://localhost:5173/auth/callback` | A redirect URI the API allows for `trekky-web`; it signs in like the web app (session cookie, then authorization code + PKCE) |

For the hosted instance (ADR-0015): `TREKKY_URL=https://<TREKKY_HOST>` and `TREKKY_REDIRECT_URI=https://<TREKKY_HOST>/auth/callback`.
Sign up there first in the web app; the import goes into that account's organization.

To refresh the snapshot, update `tasks.json` from Notion and run it again: new items are added, existing ones move forward.

## Checks

```bash
npm test                                      # node:test, from this folder
../../frontend/node_modules/.bin/tsc -p .     # strict type check with the frontend's TypeScript
```

CI runs both in the frontend job.
