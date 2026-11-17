# ADR-0021: Trekky is licensed under AGPL-3.0-only

- **Status:** Proposed
- **Date:** 2026-11-16
- **Deciders:** Davit, Claude

## Context

Trekky is open core: one codebase for the SaaS, for self-hosting and as open source (Notion "Gaps & open questions", #3). The licence was the last open question before going public (TRK-22). The repository was already public on GitHub with no licence file, which grants no rights beyond viewing and forking on GitHub. The licence has to let teams self-host and contribute, while stopping a competitor from taking the code, running it as a closed hosted service and giving nothing back. Paid features may later live outside this repository. The README and the Notion plan called this decision "ADR-0007", but that number had already gone to another decision, so it is ADR-0021.

## Decision

Trekky's code is licensed under the **GNU Affero General Public License, version 3 only** (`AGPL-3.0-only`). `LICENSE` holds the unmodified text from gnu.org, each package declares the licence, and the web app links to its source code from every screen.

## Consequences

- **Easier:**
  - Anyone may use, self-host, change and share Trekky.
  - Anyone who offers a **modified** Trekky to others over a network must also offer those users the modified source (section 13). This closes the hosting loophole that the plain GPL leaves open.
  - The licence is OSI-approved, so Trekky counts as open source. Plane, Cal.com and Grafana use the same model.
- **Rules:**
  - `LICENSE` is the AGPL-3.0 text, unchanged.
  - `package.json` (frontend, desktop, notion import) and `Cargo.toml` say `"AGPL-3.0-only"`.
  - The app shows a "Source code (AGPL-3.0)" link: in the app footer, and on every sign-in and server card. In the desktop app it opens in the system browser, including on the screens shown before a server is chosen. The URL lives in `frontend/src/lib/sourceCode.ts`. A fork points it at the source of the version it runs, e.g. its repository at a tag or commit.
  - "Only" rather than "or later": a future AGPL version from the FSF does not apply automatically.
  - Contributions come in under the same licence ("inbound = outbound"). If dual licensing is ever wanted, a contributor agreement must come first.
- **Harder:**
  - Some companies do not allow AGPL software internally. A commercial licence could be offered to them later, but only for code Davit owns or that contributors licensed for it.
  - Every dependency must stay compatible. Today's are permissive or weak-copyleft licences compatible with AGPL-3.0: MIT, Apache-2.0, BSD, ISC, Zlib, Unicode-3.0, the PostgreSQL licence (Npgsql), and MPL-2.0 for a few Rust crates pulled in by Tauri (`cssparser`, `selectors`). Check a new dependency's licence before adding it.
- **Follow-ups:**
  - Decide the product name before marketing (see `docs/name-check.md`).
  - The MSI is object code, so it should carry the licence: set `bundle.license` / `licenseFile` in `tauri.conf.json` and ship third-party notices for the bundled crates and npm packages.
  - Add a contributor note (`CONTRIBUTING.md`) when outside contributions start.

## Alternatives considered

- **FSL (Functional Source License, as Sentry uses):** stops competing hosted services for two years per release, then each release becomes Apache-2.0. Not OSI open source, and it puts off contributors.
- **Apache-2.0:** permissive, with a patent grant. Anyone, including a competitor, may host it closed.
- **MIT:** the simplest, with the same issue as Apache-2.0 and no patent grant.
- **BSL (Business Source License):** similar to FSL but with longer, per-project terms. More to explain and not open source.
- **AGPL-3.0-or-later:** follows future FSF versions automatically. Rejected to keep control of the terms.
