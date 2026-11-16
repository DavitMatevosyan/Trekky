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
npm test                 # Rust unit tests (server address, navigation guard)
npm run lint             # cargo fmt --check + clippy, warnings are errors
npm run dev              # Vite dev server + the app window on http://localhost:5173
npm run build:debug      # debug exe without an installer: src-tauri/target/debug/trekky-desktop.exe
npm run build            # release MSI: src-tauri/target/release/bundle/msi/
```

- `build` and `build:debug` build the web app first (`npm run build` in `../frontend`).
- To offer a different server on first run, set `TREKKY_DEFAULT_SERVER_URL` when building, for example the home host's `https://<name>.<tailnet>.ts.net`. Without it the default is `http://localhost:5080`.

## How it fits together

- The window loads the built web app from `http://tauri.localhost`.
- The web app sees it runs in Tauri and shows `DesktopGate` (`frontend/src/platform/desktop/`). The gate asks the shell for the saved server (`server_settings`) and asks for one on first run (`set_server_url`). It then checks `GET /api/v1/health` and only mounts the app once the server answers.
- The server must allow the origin `http://tauri.localhost` in `Cors:AllowedOrigins` (ADR-0017). Development and the home host already do.
- The address is stored in `%APPDATA%\app.trekky.desktop\settings.json`. It must be https, except `localhost` or `127.0.0.1` for local development. Delete the file to see the first-run screen again.
- Sign-in through the system browser and the `trekky://` deep link come in TREK-35 (TRK-26). Until then the login page shows, but signing in does not complete.

## Installer

- The MSI includes the WebView2 offline installer.
- It keeps the WiX upgrade code fixed (`tauri.conf.json`), so a new version replaces the old one. Never change that code.
- The MSI is unsigned for now, so Windows SmartScreen warns on install (TREK-36).
