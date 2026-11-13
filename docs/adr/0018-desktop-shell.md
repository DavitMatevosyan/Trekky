# ADR-0018: The desktop app is a thin Tauri shell; its screens live in the web app

- **Status:** Proposed
- **Date:** 2026-11-13
- **Deciders:** Davit, Claude
- **Builds on:** ADR-0017 (desktop OAuth client and CORS), ADR-0010 (frontend stack), ADR-0015 (home host).

## Context

v1.1 adds Trekky for Windows (Tauri 2, MSI, online only, one account shared with the web app). It must start without a server address baked in, because the server moves (home host now, Azure later, TRK-29) and teams may run their own. It must tell "server unreachable" apart from "signed out", must not open twice, and must upgrade in place. Screens written twice (React for the web, something else in Rust) would double the UI, i18n, accessibility and test work, at about 1 hour a week.

## Decision

`desktop/` is a **thin Tauri 2 shell** that loads the built web app from `http://tauri.localhost` and does only what a browser cannot: keep the **server address** (`settings.json` in `%APPDATA%\app.trekky.desktop`, https only except `localhost` / `127.0.0.1`), **one instance**, and **keep the window on the app** (no navigation to other origins). Every screen, including first-run "which server?" and "can't reach server", is React in `frontend/`, shown when the page runs inside Tauri, behind the platform layer (TRK-23).

## Consequences

- **Easier:** one UI codebase and test suite; the web app does not change in the browser (the desktop code loads only inside Tauri); the server is a setting, so moving it is a settings change, and a build can set the default (`TREKKY_DEFAULT_SERVER_URL`, else `http://localhost:5080`).
- **Rules:**
  - The shell exposes small commands (`server_settings`, `set_server_url`) and no general file, shell or HTTP access.
  - The app reaches the server with `fetch` under CORS (ADR-0017), checking `GET /api/v1/health` before it mounts.
  - The window loads only `http://tauri.localhost` (`http://localhost:5173` in debug builds) and opens no other windows (`window.open` and `target=_blank` are refused). Sign-in and outside links go to the system browser (TREK-35).
  - Platform code says whether silent sign-in exists (`login.silent`): the desktop app has no browser session, so protected pages go to the login page.
  - The MSI bundles the **WebView2 offline installer** and keeps a **fixed WiX upgrade code** (`6ACA28C5-222C-4774-AA84-12039D71D793`), so every version upgrades the last one in place. Never change it.
- **Harder:**
  - The installer is about 170 MB larger because of the WebView2 offline installer.
  - Building needs Rust (MSVC) and the VS C++ build tools.
  - The shell's Rust tests are not in CI until TREK-36 (TRK-27).
- **Follow-ups:**
  - TREK-35: desktop sign-in through the system browser, the `trekky://` deep link and Credential Manager.
  - TREK-36: build the MSI in CI.
  - Set the default server to the home host's Funnel URL when building releases.

## Alternatives considered

- **Screens in Rust or a separate HTML page:** no React on the start-up path, but a second UI to style, translate, make accessible and test.
- **Load the server's own web app (remote URL):** no CORS and always current, but the window would be blank when the server is down, the app could not show its own "can't reach server" screen, and remote pages in a webview with IPC are a larger attack surface.
- **A bootstrapper MSI that downloads WebView2:** a smaller installer, but it fails offline and on locked-down machines; Windows 11 ships WebView2, but Windows 10 may not.
- **No single-instance lock:** simpler, but two windows would fight over the refresh token in Credential Manager (TREK-35), and the deep link needs one receiver.
