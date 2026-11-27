# TREK-75 UI evidence (2026-11-27)

Tree: the app code of `36f85bf`, taken right before it was committed (PR #77 head `fd979cf` adds only a merge of `dev`, which changed no app code, and an ADR renumbering). Stack: the branch's API on its own throwaway database, the Vite dev server, and the
Node fake GitHub of `frontend/e2e/support/fakeGitHub.ts` (one repository, `acme/handbook`, docs on `dev`). Data is
synthetic: a `uitest-*@example.com` user in a new organization.

| # | Shot | What it shows | Result |
| --- | --- | --- | --- |
| 1 | 01-not-linked.png | A project without a docs repository: the message and, for an owner, the link to its settings | pass |
| 2 | 02-readme-light.png | Docs tab on the README: folder tree, breadcrumb, last change with commit link, Open on GitHub, GFM table and task list, Mermaid diagram in theme colours, image from the docs folder (a 1×1 PNG under the diagram) | pass |
| 3 | 03-readme-dark.png | The same in the dark theme; Mermaid redrawn with the dark tokens | pass |
| 4 | 04-relative-link.png | `[setting up](guide/setup.md#install)` opened inside Trekky: tree selection, breadcrumb `Docs / guide / setup.md` | pass |
| 5 | 05-search.png | Search `dock comp`: one doc found, `Docker` and `Compose` marked in the snippet | pass |
| 6 | 06-narrow.png | 420 px wide: the tree and search stack above the doc | pass |

Steps: sign up → Settings → Connect GitHub (the fake's install page redirects back) → create projects DOC and NOD →
NOD Docs (1) → DOC Settings: link `acme/handbook` → DOC Docs (2, 3) → follow the relative link (4) → search (5) →
narrow window (6). The same flow, with assertions, is `frontend/e2e/docs.spec.ts` (passes in the full suite, 19/19).

Cleanup: the run's database `trekky_trek75` is dropped with its users and organizations; API, Vite, fake GitHub and
browser stopped; local screenshots deleted after the push.
