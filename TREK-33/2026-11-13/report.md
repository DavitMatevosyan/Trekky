# TREK-33 (TRK-24) manual verification, 2026-11-13: pass

Tree: PR #29 head d32aaf2. Local stack: API on :5080 (Development), web app on :5173, Postgres 17, Playwright Chromium.

1. Signed up `uitest-trek33@example.com` in the web app (01).
2. With that browser session, `GET /connect/authorize` as `trekky-desktop` → 302 to `trekky://auth/callback?code=…`.
3. From a page on origin `http://tauri.localhost` (the desktop webview's origin): cross-origin `POST /connect/token` → 200, access token `client_id=trekky-desktop`; `GET /api/v1/me` with it → 200; refresh as `trekky-desktop` → 200 with a rotated refresh token (02).
4. From `http://evil.localhost`, the same call with a valid token is blocked by the browser: no `Access-Control-Allow-Origin` (03).

Note: Chromium's Local Network Access asks for permission when a non-loopback-resolved page calls localhost; the run granted it for the two test origins. In the Tauri app the webview calls the hosted HTTPS server, not localhost, so this does not apply there (worth checking in TREK-34).

Tokens and codes are redacted on screen. Cleanup: test user and org deleted, screenshots deleted locally, API, dev server and browser stopped.
