# Is "Trekky" free to use? (TRK-22, 2026-11-16)

"Trekky" is a working name (Notion "Gaps & open questions", #10: "check before going public"). This is what a check on 2026-11-16 found. It is a quick web and registry search, not legal advice or a full trademark search.

## Other products called Trekky

| Where | What |
| --- | --- |
| `trekky.app` (registered 2026-04-23) | **"Trekky — Job Application Tracker for Engineers"**: a live software product with the same name, also a "tracker". This is the closest conflict. |
| `trekkyapp.com` (2026-06-01), `gettrekky.com` (2026-10-16), redirects to `trekky.gr` | "Trekky", a travel guides and packing lists app |
| App stores | Similar names: "Trekka – Task Tracker" (iOS), "Task Trekk" (task management) |
| US trademarks (search) | No "TREKKY" mark found. "PROJECT TREK" is registered for internet-based construction project management. EU (EUIPO) and other registers were not searched. |
| Wider | "Trekkie" is the well-known name for Star Trek fans. |

## Domains

| Domain | Status |
| --- | --- |
| `trekky.com` | Registered since 2008 (holding page, no product) |
| `trekky.app` | Registered: the job tracker above |
| `trekky.net` | Registered 2026-02-28 |
| `trekky.io` | Registered (has name servers) |
| `trekky.dev` | No registration found (RDAP), probably free |
| `trekky.org` | No registration found (RDAP), probably free |

## Assessment

The name is **not clearly free**. Another software "tracker" already ships as Trekky on `trekky.app`, and the obvious `.com`, `.app` and `.io` domains are taken. Using it for a public product risks confusion and a trademark complaint, and the best domains are unavailable.

**Decision (Davit, 2026-11-16): keep "Trekky", publicly too (option 2 below), accepting the risk above.** The options that were considered:
1. Keep "Trekky" internally (repository, `TREK` keys) and choose a product name before any marketing. This needs no code changes now.
2. Keep "Trekky" publicly with a qualified domain (e.g. `trekky.dev`) and accept the risk.
3. Rename now, while there are no users.

Until then, nothing in the code depends on the decision. The name appears in UI text (`messages.appName`), the desktop app's product name and identifier (`app.trekky.desktop`), and the repository name.
