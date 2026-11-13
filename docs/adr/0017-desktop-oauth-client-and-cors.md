# ADR-0017: The desktop app signs in as its own OAuth client; CORS origins come from configuration

- **Status:** Proposed
- **Date:** 2026-11-13
- **Deciders:** Davit, Claude
- **Extends:** ADR-0003 (authentication). Everything there stays in force for the web app.

## Context

v1.1 adds a Windows desktop app (Tauri 2, online only, one account shared with the web app; TRK-23 to TRK-28). It loads the built web app in a WebView2 window whose origin is `http://tauri.localhost`, not the API's, and it signs in through the system browser, because a webview login page cannot use the browser's existing session and teaches users to type passwords into an app window. ADR-0003 keeps the web app's tokens in memory and re-runs the code flow with the session cookie after a reload; a desktop app has no shared cookie with the system browser, so it must keep a refresh token (TRK-26: in Windows Credential Manager) to start signed in. The API must therefore accept a second OAuth client and calls from a second origin, without weakening the web app. POC, about 1 hour a week, no paid services.

## Decision

The desktop app signs in as a separate public, native OAuth client **`trekky-desktop`** (authorization code + PKCE through the system browser, redirect `trekky://auth/callback`, rotating refresh tokens that last **90 days**, the web app keeping 14), registered on startup like `trekky-web`; browser origins other than the API's own may call the API only when listed in **`Cors:AllowedOrigins`** (bearer tokens only, no credentials).

## Consequences

- **Easier:** the desktop app opens signed in for up to 90 days of not being used (each refresh rotates the token and restarts the 90 days); its tokens can be told apart (`client_id` claim) and revoked without touching web sessions, although logging out everywhere (US-02) still ends both; the redirect URI and origins are settings, so the Azure move (TRK-29) changes configuration, not code.
- **Rules:** `trekky-desktop` has no client secret and requires PKCE, like `trekky-web`; its redirect URIs come from `Identity:DesktopClient:RedirectUris` (default `trekky://auth/callback`). CORS allows `GET, POST, PATCH, DELETE`, the `Authorization`, `Content-Type` and `If-Match` headers, exposes `ETag` and `Location`, and never allows credentials, so the session cookie is never sent cross-origin. No origin is allowed by default; Development and the home host allow `http://tauri.localhost`.
- **Harder:** a stolen desktop refresh token works longer than a web one; reuse detection (ADR-0003) and Credential Manager storage (TRK-26) are the mitigations. Any other `trekky://` handler on the machine could receive a code, which PKCE makes useless without the verifier.
- **Follow-ups:** TRK-25 builds the Tauri shell on `http://tauri.localhost`; TRK-26 registers the `trekky://` deep link and stores the refresh token; TRK-29 keeps the CORS origin when moving to Azure.

## Alternatives considered

- Reuse `trekky-web` for the desktop app: one client, but its refresh-token lifetime and redirect URIs would have to change for both apps, and web and desktop sessions could not be told apart.
- Loopback redirect (`http://127.0.0.1:<port>`, RFC 8252) instead of a custom scheme: no scheme registration, but the app has to run a local HTTP listener and the browser tab stays on a localhost page; Tauri's deep-link plugin makes the custom scheme simpler.
- Sign in inside the webview: no deep link needed, but RFC 8252 advises against embedded user agents, and the user's browser session would not be reused.
- Serve the desktop app from the API's origin (remote URL in the webview): no CORS, but the app would fail to start without the server and could not show its own "can't reach server" screen (TRK-25).
- A refresh-token lifetime that never expires: fewer sign-ins, but a lost laptop would keep access indefinitely; 90 days matches common native-app defaults.
