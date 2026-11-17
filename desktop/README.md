# Trekky for Windows

A Tauri 2 shell around the web app in `../frontend` (ADR-0018). The shell keeps the server address, allows one
instance and keeps the window on the app. Every screen is React, including the first-run "which server?" screen and
"can't reach server".

## Prerequisites

- Rust, stable MSVC toolchain: `winget install Rustlang.Rustup`, then `rustup default stable-x86_64-pc-windows-msvc`.
- Visual Studio Build Tools with the "Desktop development with C++" workload.
- Node 22.18 or later, plus `npm ci` in `../frontend`.
- WebView2: included in Windows 11; the MSI installs it where it is missing.

## Commands

Run these from this folder after `npm ci`:

```bash
npm test                 # Rust unit tests (server address, navigation guard, links, Credential Manager)
npm run lint             # cargo fmt --check + clippy, warnings are errors
npm run dev              # Vite dev server + the app window on http://localhost:5173
npm run build:debug      # debug exe without an installer: src-tauri/target/debug/trekky-desktop.exe
npm run build            # release MSI: src-tauri/target/release/bundle/msi/
npm run e2e              # smoke e2e against the real debug app (see below)
npm run typecheck:e2e    # strict type check of e2e/ with the frontend's TypeScript
```

- `build` and `build:debug` build the web app first (`npm run build` in `../frontend`).
- To offer a different server on first run, set `TREKKY_DEFAULT_SERVER_URL` when building, for example the home host's `https://<name>.<tailnet>.ts.net`. Without it the default is `http://localhost:5173`, the local dev server (`npm run dev` in `../frontend`). It has to be an address that serves the web app as well as the API, because sign-in uses the web app's login page (ADR-0019).

## How it fits together

- The window loads the built web app from `http://tauri.localhost`.
- The web app sees it runs in Tauri and shows `DesktopGate` (`frontend/src/platform/desktop/`). The gate asks the shell for the saved server (`server_settings`) and asks for one on first run (`set_server_url`). It then checks `GET /api/v1/health` and only mounts the app once the server answers.
- The server must allow the origin `http://tauri.localhost` in `Cors:AllowedOrigins` (ADR-0017). Development and the home host already do.
- The address is stored in `%APPDATA%\app.trekky.desktop\settings.json`. It must be https, except `localhost` or `127.0.0.1` for local development. Delete the file to see the first-run screen again.
- Sign-in (ADR-0019): "Log in with your browser" opens the server's `/connect/authorize` for `trekky-desktop` in the system browser. A browser without a session is sent to the web login page and comes back. The server then redirects to `trekky://auth/callback?…`. Windows starts a second `trekky-desktop.exe` with that URL; the single-instance plugin hands it to the open window, which exchanges the code.
- The refresh token is kept in Windows Credential Manager under `Trekky`, one entry per server. Logout or a rejected token removes it. You can see it in Control Panel → Credential Manager → Windows Credentials.
- The MSI registers `trekky://`. Debug builds register it for the current user on start (`HKCU\Software\Classes\trekky`, pointing at the debug exe). Delete that key after testing a debug build if you then install the MSI.
- A sign-in started before the app was closed can't finish after a restart: the browser's link opens the app, which shows the login page again.

## Installer

- The MSI includes the WebView2 offline installer.
- It keeps the WiX upgrade code fixed (`tauri.conf.json`), so a new version replaces the old one. Never change that code.
- The MSI is unsigned for now, so Windows SmartScreen warns on install (TREK-36).

## Smoke e2e

`e2e/smoke.test.ts` drives the real debug app with [tauri-driver](https://v2.tauri.app/develop/tests/webdriver/) and Microsoft Edge WebDriver. It goes through:

1. The first-run server screen.
2. "Log in with your browser".
3. The `trekky://` callback.
4. A restart that is signed in from Credential Manager.
5. Logout, which removes the stored token.
6. An update from a feed the test serves (`TREKKY_UPDATE_FEED`): the app offers it and refuses to install it, as it is not signed with Trekky's key (ADR-0024).

The test plays the system browser itself: debug builds write the address they would open to `TREKKY_OPEN_URL_LOG` instead of opening it.

1. `cargo install tauri-driver --locked`.
2. Download Microsoft Edge WebDriver matching your WebView2 runtime from `https://msedgedriver.microsoft.com/<version>/edgedriver_win64.zip`. The runtime's version is in Edge's `edge://version`, or in the registry under `EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}`, value `pv`. Set `MSEDGEDRIVER` to the path of `msedgedriver.exe`.
3. Start the API and `npm run dev` in `../frontend`, then run `npm run build:debug` and `npm run e2e`.

`npm run build:debug` embeds the web app as it is at that moment (the window loads `http://tauri.localhost`, not the dev server), so run it again after every frontend change, or the e2e tests the old screens.

The test sets the app's settings aside and restores them afterwards. It removes the Credential Manager entry for its server (`http://localhost:5173` unless `TREKKY_E2E_SERVER` is set) and leaves one `e2e-desktop-*@example.com` user, like the web e2e. It does not run in CI yet (TREK-36).
