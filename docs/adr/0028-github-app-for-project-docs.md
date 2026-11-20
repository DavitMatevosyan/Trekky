# ADR-0028: Project docs reach GitHub through the Trekky GitHub App, with tokens kept on the server

- **Status:** Agreed. The requirements are Davit's (TREK-73 and TREK-74: a GitHub App installed per organization with access to chosen repositories only, tokens on the server, encrypted, never in the browser; a project links one repository, docs folder and branch, default `dev`); the details below were decided by Claude under ADR-0023.
- **Date:** 2026-11-20
- **Deciders:** Davit, Claude
- **Builds on:** ADR-0002 (tenancy and RLS), ADR-0011 (activity events through the outbox), ADR-0026 (role policies)

## Context

TREK-73 brings project documentation from GitHub into Trekky: read it (TREK-75), edit it as pull requests (TREK-76, TREK-77), link it to work items (TREK-78) and later follow pull requests on the board (TREK-79). Trekky therefore needs lasting, narrow access to some repositories of an organization, acting as itself rather than as one person, and a way to tell which GitHub repositories belong to which Trekky organization without trusting the browser. The server is the home host now and Azure later (ADR-0015), so secrets must come from configuration, not the repository, and the POC has no paid key vault yet.

## Decision

Trekky talks to GitHub only as **one GitHub App**, installed per organization on the repositories its Admin chooses, and **no GitHub credential is ever stored in the database or sent to the browser**:

- **Permissions:** contents and pull requests read and write, metadata read; nothing administrative. "Request user authorization (OAuth) during installation" and expiring user tokens stay on.
- **Connecting:** an Admin or Owner starts the install from organization settings. GitHub redirects back to the web app with `code` and `installation_id`; the API exchanges the code for a user token and connects the installation only if `GET /user/installations` lists it **and** `GET /user/installations/{id}/repositories` shows the user as admin of every repository it reaches (the same count the installation itself sees). Being listed alone is not enough: GitHub lists an installation for anyone who can read one of its repositories, which would let a contractor claim a whole organization's installation. The user token is revoked right after the check. The `installation_id` alone is never trusted (GitHub's own warning). One installation belongs to at most one Trekky organization (unique index; RLS hides the other organization's row, so a clash answers 409). The web app sends a random `state` with the install and connects at once when the same tab gets it back; otherwise (desktop app, or `state` not returned) it asks the user to confirm first.
- **Tokens:** the API signs a short JWT (RS256, issuer = client id, 9 minutes) with the app's private key to get installation tokens, and keeps each one **in process memory only** until five minutes before GitHub's one-hour expiry. A 401 drops it and fetches a new one; disconnecting drops it at once.
- **Secrets:** `GitHub:ClientId`, `GitHub:ClientSecret`, `GitHub:AppSlug` and the private key (`GitHub:PrivateKey`, or `GitHub:PrivateKeyFile` for a Docker secret) come from configuration — environment and secret files on the home host, Azure Key Vault references in Container Apps later. All or none: half a configuration stops the API at startup; none switches GitHub features off (503 and a note in the UI).
- **Project link:** each project has at most one `owner/name` repository, docs folder (default `docs/`) and branch (default `dev`), saved only after GitHub confirms the installation lists the repository and the branch and folder exist. Changes need `If-Match`. Disconnecting an installation removes its links.
- **Who:** connecting, disconnecting, listing repositories and linking use `ManageProjects` (Admin, Owner); reading a project's link uses `ReadWork`, so the docs viewer can use it for every role. ADR-0026's table is unchanged.

Every change writes an activity event (`GitHubConnected`, `GitHubDisconnected`, `ProjectRepositoryLinked`, `ProjectRepositoryChanged`, `ProjectRepositoryUnlinked`).

## Consequences

- Nothing in a database backup or a leaked row can act on GitHub; the private key is the one secret to guard, and rotating it is a configuration change. This is why "encrypted at rest" from TREK-73 is met by not storing tokens at all rather than by encrypting them.
- Each API instance fetches its own installation tokens after a restart (one extra GitHub call per installation and hour); with several instances on Azure each keeps its own cache, which GitHub's rate limits allow easily.
- Davit registers the app on GitHub once (his account): permissions above, callback URL `https://<host>/settings/github/callback`, then puts its settings into `deploy/.env` and the key into `deploy/secrets/github-app.pem` (deploy/README.md). Until then production shows "GitHub is not set up".
- Disconnecting in Trekky does not uninstall the app on GitHub; the settings page links to the installation's GitHub page for that. When the app is uninstalled on GitHub first, Trekky answers 409 and the Admin disconnects it; webhooks that clean this up arrive with TREK-79.
- A repository is matched within the first 1,000 the installation can reach, and the admin check reads at most 1,000; larger installations must be narrowed to the docs repositories.
- Residual risk: someone can send a Trekky Admin a callback link carrying the sender's own code and installation; one click on "Connect" then links the sender's repositories to the Admin's organization, where later docs edits (TREK-76) would go. The confirm step warns against links one did not start. Closing it fully needs the server to keep the install `state` itself; revisit with TREK-76, before any edit leaves Trekky.

## Alternatives considered

- **OAuth App or personal access tokens** — act as one person with access to all of their repositories, and stop working when that person leaves; GitHub recommends Apps for integrations.
- **Store installation tokens encrypted in Postgres** — survives restarts, but adds a key to manage for something that expires within the hour and costs one API call to get again.
- **Trust `installation_id` from the setup redirect** — anyone could connect another account's installation by guessing its id.
- **A separate "ManageIntegrations" policy** — no role would differ from `ManageProjects` today; add it when one does.
