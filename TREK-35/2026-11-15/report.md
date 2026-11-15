# TREK-35 (TRK-26) manual verification, 2026-11-15: pass

Tree: PR #31 head 1f800c5. The real debug app (`desktop/src-tauri/target/debug/trekky-desktop.exe`, built from that tree), driven over WebView2 CDP, against the local stack: API on :5080, web dev server on :5173 (proxies the API), Postgres 17. The debug build's `TREKKY_OPEN_URL_LOG` hook captured the URL the app hands to the system browser; a **fresh Playwright browser with no Trekky session played the system browser** on the real web pages, and the `trekky://` link was handed to the app the way Windows does (starting a second `trekky-desktop.exe` with it).

| Step | Result |
| --- | --- |
| First run → server `http://localhost:5173` → login screen | "Log in with your browser" (01) |
| Click it | URL opened: `/connect/authorize?client_id=trekky-desktop&redirect_uri=trekky://auth/callback…`; app shows "Finish in your browser" + "Open the browser again" (02) |
| Browser (no session) opens that URL | Server redirects to the web login page `/login?returnTo=/connect/authorize?client_id=trekky-desktop…` (03) |
| Log in in the browser | Web page continues to the authorize request; server redirects to `trekky://auth/callback?code=…&state=<the app's state>` |
| Windows hands the link over (second instance) | Second instance exits; the open window signs in as the test user (04) |
| Credential Manager | Entry `http://localhost:5173.Trekky` (Generic) holds the refresh token |
| Restart the app | Signed in straight away from Credential Manager, no browser opened (05); API log shows the refresh-token grant |
| Log out | Back to "Log in with your browser" (06); the Credential Manager entry is gone |

Also run on this tree: desktop smoke e2e (tauri-driver) passed 2/2; API 203/203, frontend 228/228, Rust 15/15.

Cleanup: test users and orgs (`uitest-trek35-*`, `e2e-desktop-*`) deleted, app settings and Credential Manager entry removed, `trekky://` debug registration removed, API, dev server, app and browser stopped, screenshots deleted locally.
