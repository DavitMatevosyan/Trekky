# ADR-0019: The desktop app signs in through the system browser; its refresh token lives in Credential Manager

- **Status:** Proposed
- **Date:** 2026-11-15
- **Deciders:** Davit, Claude
- **Builds on:** ADR-0003 (authentication), ADR-0017 (desktop OAuth client), ADR-0018 (desktop shell).

## Context

The desktop app (ADR-0018) runs the web app from `http://tauri.localhost` and talks to the server with bearer tokens under CORS, without cookies. Login sets an HttpOnly, SameSite=Strict session cookie that `/connect/authorize` reads (ADR-0010), so the app's window cannot log in the way the web app does. RFC 8252 advises native apps to sign in through the system browser rather than an embedded page, so passwords never pass through the app and an existing browser session can be reused. There is no server-rendered login page: until now `/connect/authorize` without a session answered `login_required`. The app must start signed in (TRK-26) without keeping the password.

## Decision

The desktop app opens `/connect/authorize` for `trekky-desktop` in the **system browser**. Without a session, an interactive GET request (no `prompt=none`) is **redirected to the web app's `/login?returnTo=<the request>`**, and the web login and sign-up pages **continue to a `returnTo` that is the server's own `/connect/authorize?…`** after login instead of signing in themselves. The code comes back on `trekky://auth/callback` to the running app, and the **refresh token is kept in Windows Credential Manager**, one per server.

## Consequences

- **Easier:**
  - A browser that is already logged in to Trekky returns to the app at once, without asking again.
  - Passwords stay in the browser.
  - The web app needs no second login form.
  - The same redirect gives any future OAuth client a login page.
- **Rules:**
  - `prompt=none` and POST requests still get `login_required`, so the web app's silent sign-in is unchanged.
  - `returnTo` is followed back to the server only when it starts with `/connect/authorize?` on the same origin. OpenIddict still checks the client, the registered redirect URI and PKCE, so an unknown redirect URI gets an error page and never the login redirect.
  - Desktop sign-up opens `/sign-up?returnTo=<the request>` the same way.
  - The shell opens only http(s) addresses in the browser.
  - The shell passes on only `trekky://auth/callback?…` URLs. The web app acts on one only when its `state` matches the request it started in this window. Any other is ignored, so a link from some web page cannot cancel a sign-in in progress or move a signed-in user. "Open the browser again" reopens the same request instead of starting a new one.
  - The refresh token is stored under the service `Trekky` with the server's origin as the user, with **Local** persistence (never copied to other computers by a roaming profile). Writes happen one after the other, so the newest token is the one kept. The token is removed on logout or when the server rejects it.
  - **Refresh tokens are reference tokens** (OpenIddict `UseReferenceRefreshTokens`) for every client: a short random handle, with the content kept in the token table. A self-contained encrypted refresh token is about 3.3 KB, more than Credential Manager's 2,560-byte limit, and a copied handle is worthless once it is revoked. Refresh tokens issued before the switch stop working, so users log in once more.
  - Logout uses `POST /api/v1/auth/logout` with the bearer token, which ends every session of the user (US-02).
  - The desktop server address must serve the web app as well as the API. Locally that is the Vite dev server `http://localhost:5173`.
- **Harder:**
  - Signing in leaves a browser tab on the login page or the "open Trekky?" prompt.
  - A sign-in started before the app was closed cannot finish after a restart, because the PKCE verifier lives in the window's session storage. The user starts again.
  - Anyone who can read the user's Credential Manager can use the refresh token. Rotation, reuse detection (ADR-0003) and the 90-day limit (ADR-0017) bound that.
- **Follow-ups:** TREK-36 builds the MSI in CI; the smoke e2e (tauri-driver) runs locally until then.

## Alternatives considered

- **A login form in the app's window posting to the API:** the session cookie would need SameSite=None and credentialed CORS, which weakens the web app. It also asks for the password inside the app (RFC 8252).
- **A loopback redirect (`http://127.0.0.1:<port>`) instead of `trekky://`:** the app would need a local HTTP listener. ADR-0017 already chose the custom scheme.
- **A server-rendered login page:** a second login UI to build, style and translate, while the web app already has one.
- **Keep tokens in memory only, as the web app does:** the user would sign in through the browser on every start, which TRK-26 rules out.
- **Split a self-contained refresh token across several Credential Manager entries:** keeps the server unchanged, but adds fragile chunking code, and a stolen token still carries its encrypted claims.
- **Store the refresh token in a file or `localStorage`:** readable by anything with the user's file access and not encrypted per user; Credential Manager is the Windows store meant for this.
