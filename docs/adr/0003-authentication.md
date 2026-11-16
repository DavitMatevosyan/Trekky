# ADR-0003: Authentication — ASP.NET Core Identity + OpenIddict in-app

- **Status:** Agreed
- **Date:** 2026-10-06 (agreed in planning, 2026-10-05)
- **Deciders:** Davit, Claude

## Context

The POC needs email + password sign-up and login, no paid services, and one codebase for SaaS and self-hosted. Later it needs per-org SSO (OIDC first, SAML later) and machine access for the AI agent. A separate identity server (Authentik, Keycloak) would be one more service to run and configure.

## Decision

Authentication runs **inside the Trekky API**:

- **ASP.NET Core Identity** stores users (in the `identity` schema), hashes passwords and manages security stamps and lockout.
- **OpenIddict** is the OAuth 2.0 / OIDC server in the same process.
- **Web app flow: Authorization Code + PKCE.** The React login page posts credentials to `POST /api/v1/auth/login`, which sets an HttpOnly, Secure, `SameSite=Strict` Identity cookie. The app then calls `/connect/authorize` (code + PKCE) and exchanges the code at `/connect/token`. The resource-owner password grant is not used (removed in OAuth 2.1).
- **Tokens:** JWT access tokens, **15 minutes**, signed RS256 and published via JWKS. **Refresh tokens rotate** on every use, and reuse of an old refresh token revokes the chain.
- **Token storage in the browser:** access and refresh tokens live in memory only. After a page reload the app silently re-runs the code flow (`prompt=none`) using the HttpOnly cookie.
- **Claims:** `sub` (user id), `org_id`, `role` (Owner, Admin, Member, Viewer), `email`.
- **Sign-up** (`POST /api/v1/auth/sign-up`) creates the user, the organization and an Owner membership in one transaction. In v0.1 a user belongs to exactly one organization; switching organizations comes with invites (v1.1).
- **Machine access (later):** the agent uses the **client credentials** flow as a service user with its own role.
- **Keys:** development uses OpenIddict's development certificates; production loads an X.509 signing and encryption certificate from configuration, never from the repo.

## Consequences

- No extra service to run; self-hosting stays `docker compose up`.
- We own security-critical code paths, so they get integration tests: sign-up, login, wrong password, lockout, refresh rotation, reuse detection, logout revokes tokens.
- Per-org SSO later becomes an external login provider per organization (an `sso_connection` row, routing by email domain, just-in-time provisioning) without changing the token format.
- Email confirmation and password reset need SMTP; they arrive with invites (v1.1). Until then, sign-up is open only on the POC.

## Alternatives considered

- **Authentik / Keycloak** — mature, but one more service and configuration surface; dropped in planning.
- **Hosted IdP (Auth0, Clerk, Entra ID)** — paid at scale and blocks fully self-hosted installs.
- **Cookie-only BFF without tokens** — simplest for the SPA, but the MCP server and agent need bearer tokens anyway; the code flow serves both.
