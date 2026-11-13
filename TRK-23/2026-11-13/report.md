# TRK-23 manual verification: pass (web behaviour unchanged)

Commit 8954a9e on TRK-23-frontend-platform-layer, dev stack (API :5080, Vite :5173), test user uitest-trk23-*@example.com.

| # | Check | Result |
| --- | --- | --- |
| 1 | Sign up signs the user in (code + PKCE through the web platform) | pass |
| — | Reload: signed back in silently (prompt=none) | pass |
| — | OAuth requests stay on the page's origin: GET /connect/authorize, POST /connect/token on http://localhost:5173 | pass |
| 2 | Markdown links render as before; the external link keeps its title ("Tauri 2") | pass |
| 3 | Log out, then log in again | pass |
