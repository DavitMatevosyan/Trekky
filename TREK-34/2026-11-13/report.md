# TREK-34 (TRK-25) manual verification, 2026-11-13: pass

Tree: PR #30 head fec8450, debug build of the real Tauri app (`desktop/src-tauri/target/debug/trekky-desktop.exe`), driven over WebView2 CDP (`WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9222`). Local API on :5080 (Development, CORS allows http://tauri.localhost). Settings folder `%APPDATA%\app.trekky.desktop` deleted first, so this is a real first run.

| Step | Result |
| --- | --- |
| First run, API stopped | "Connect to your Trekky server", pre-filled `http://localhost:5080` (01) |
| Connect | "Can't reach the server" with Try again / Change server (02) |
| Start API, Try again | Login page at `http://tauri.localhost/login?returnTo=%2F` with "Server: localhost:5080 · Change server" (03) |
| `window.open('http://localhost:5080/')` | returns null, still 1 page target |
| `location.assign('http://localhost:5080/api/v1/health')` | blocked, window stays on `/login` |
| Change server → `http://trekky.example.com` | refused: "Use an https:// address. Plain http is only allowed for localhost." (04); Keep the current server returns to login |
| Second launch | exits; one `trekky-desktop` process remains |
| Restart | goes straight to the login page; `settings.json` holds `http://localhost:5080` |

Release MSI (`npm run build`): `Trekky_0.1.0_x64_en-US.msi`, 207 MiB, ProductVersion 0.1.0, UpgradeCode {6ACA28C5-222C-4774-AA84-12039D71D793}, Upgrade table present. Not installed (needs elevation).

Sign-in does not complete in this build by design (TREK-35).

Cleanup: app closed, settings folder deleted, API stopped, screenshots deleted locally. No test users were created.
