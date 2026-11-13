# TRK-9 manual verification: pass (local production stack; real Funnel not tested)

Commit 751ce25 on TRK-9-home-host: deploy/docker-compose.yml in laptop mode (TREKKY_SITE_ADDRESS=localhost, https://localhost:8443, Caddy's local CA), fresh volumes, test user uitest-trk9-*@example.com.

| # | Check | Result |
| --- | --- | --- |
| 1 | Sign-up page served by the production web image over HTTPS | pass |
| 2 | Sign up, create project and item, change status from the item page (header ETag → If-Match through Caddy), history shows it | pass |
| 3 | OpenID discovery: issuer and endpoints are https://localhost:8443/ | pass |
| — | Services: migrate exited 0; api, web, postgres, backup running; health 200; CSP present | pass |

Funnel itself (https://<name>.<tailnet>.ts.net) needs Davit's tailnet and is not tested here.
