# ADR-0010: Frontend stack and token handling in the web app

- **Status:** Proposed
- **Date:** 2026-10-26
- **Deciders:** Davit, Claude

## Context

TRK-8 built the first real screens (sign-up, login, logout) and with them chose the libraries every later screen will use. ADR-0001 fixes only the backend; the Notion baseline lists the frontend as a proposal (React + TypeScript, Vite, shadcn/ui + Tailwind, TanStack Query, a client generated from OpenAPI, Vitest + Playwright, WCAG AA, i18n-ready). ADR-0003 fixes the auth flow (code + PKCE, tokens in memory, `prompt=none` after reload) but not how the browser code carries it out. With about 1 hour a week, the next TRKs (projects, work items, board) should reuse these choices, not reopen them. Davit asked for this to be recorded in its own ADR (PR #9 review).

## Decision

The web app uses **React Router**, **TanStack Query**, **Tailwind CSS v4 + shadcn/ui** and an **openapi-fetch** client generated from the API's OpenAPI document, tested with **Vitest + Testing Library** and **Playwright** e2e in CI. Tokens stay **in memory** in one `AuthSession` per tab, and the session cookie authenticates only `/connect/authorize` and `POST /api/v1/auth/logout`.

How:

- Generated types: `npm run generate:api` → `src/api/schema.d.ts`, committed. shadcn components are copied into `src/components/ui`, with Radix underneath.
- `sessionStorage` holds only the PKCE verifier, `state` and return path, for the redirect round trip. Return paths are limited to the same origin.
- `AuthSession` adds the bearer token, refreshes before expiry and after a 401, and runs one refresh at a time, because refresh tokens are single-use.
- Logout accepts the session cookie as well as the bearer token (TRK-8.1), so logout works even after the tab's tokens are gone. No other API endpoint accepts the cookie. The security stamp is checked on every cookie read, so a cookie from before a logout is rejected at once.
- The authorization flow is a full-page redirect. One origin serves the web app, `/api` and `/connect` (the Vite proxy in development). User-facing strings live in `src/i18n/messages.ts` until i18next arrives.

## Consequences

- SameSite=Strict on the session cookie is the only CSRF defence for logout, and it is acceptable: the worst outcome is a forced logout. SameSite works per registrable domain, not per origin, so production hosting (deferred, see ADR-0004) must not host untrusted sites on sibling subdomains of the app's domain.

- Easier: new screens follow one pattern (route, then a query or mutation through the typed client, then shadcn components). API changes show up as TypeScript errors once `schema.d.ts` is regenerated.
- Easier: no token in `localStorage`, so a cross-site script cannot read a long-lived token from storage. A reload costs one silent redirect.
- Harder: `schema.d.ts` must be regenerated and committed whenever an endpoint changes. A CI drift check can come later if this is forgotten.
- Harder: same-origin is a hosting requirement. Production hosting (deferred, see ADR-0004) must serve the web app, `/api` and `/connect` behind one origin (reverse proxy), or the `SameSite=Strict` session cookie never reaches `/connect/authorize`.
- Harder: e2e needs Postgres and the API in CI, so the job is slower than the unit tests.
- Follow-ups: i18next when a second language is needed. axe accessibility checks in Playwright (listed in the baseline) are not wired up yet; add them with the first screen that has real content (TRK-10).

## Alternatives considered

- **Tokens in `localStorage`/cookies readable by JavaScript.** Simpler reloads, but they expose long-lived tokens to any injected script. Rejected by ADR-0003.
- **Hidden-iframe silent auth instead of full-page redirects.** No visible redirect on reload, but more code, and browsers increasingly limit storage in iframes. Not worth it for the POC.
- **Backend-for-frontend (cookie-only, no tokens in the browser).** Rejected in ADR-0003: the MCP server and the agent need bearer tokens anyway.
- **Hand-written fetch wrappers or orval/openapi-generator.** Hand-written types drift from the API. The full generators produce much more code; `openapi-typescript` + `openapi-fetch` is types plus a thin client.
- **Redux / Zustand for server data.** TanStack Query already handles caching, loading and error states. Local UI state stays in React state until a real need appears.
- **A component library such as MUI or Chakra.** Heavier and harder to restyle. shadcn/ui copies plain components into the repo, which we own and can keep accessible.
