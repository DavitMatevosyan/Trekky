# ADR-0008: Keep a Postman collection in the repo, updated with the API

- **Status:** Agreed
- **Date:** 2026-10-26
- **Deciders:** Davit, Claude

## Context

Davit tests the API by hand between TRKs, about 1 hour a week, so every session must start fast. Since TRK-7, getting a token takes several steps: sign up or log in for the session cookie, then authorization code + PKCE at `/connect/authorize` and `/connect/token`. Postman's built-in OAuth 2.0 helper cannot do this, because its browser window has no session cookie and Trekky has no login page. The OpenAPI draft (`docs/tech/openapi.yaml`) describes the contract but does not run the flow. A collection kept outside the repo would drift from the code.

## Decision

`postman/Trekky.postman_collection.json` is the shared manual-testing collection. Every TRK that adds or changes an endpoint updates it in the same commit, with one folder per module, and keeps the auth flow working by scripts that store tokens as collection variables.

## Consequences

- Easier: a fresh checkout can be tested by importing one file; the `code-reviewer` agent checks the collection against changed endpoints.
- Harder: one more file to keep in step; Postman exports are verbose JSON, so reviews look at the request list, not the formatting.
- No secrets in the collection: only local defaults (localhost URLs, a test email and the public test password). Personal values go in a local Postman environment, which is not committed.
- Later: if the collection drifts often, generate its request skeleton from OpenAPI and keep only the scripts by hand.

## Alternatives considered

- Postman OAuth 2.0 "Get New Access Token": cannot reach the session cookie, so `/connect/authorize` answers `login_required`.
- Rider / VS Code `.http` files: work in the IDE, but handling PKCE and cookies takes more scripting, and Postman is what Davit uses.
- Only the OpenAPI document: describes the API but cannot run the multi-step auth flow.
