# ADR-0033: The docs viewer reads GitHub through a per-organization cache, searches it in Postgres and renders Markdown in the browser

- **Status:** Agreed. The requirements are Davit's (TREK-73 and TREK-75: a docs page per project with a folder tree, last commit, breadcrumb and a GitHub link; GitHub-flavoured Markdown and Mermaid; relative links inside Trekky; relative images through the server; HTML sanitised; content cached in Postgres per organization with RLS, keyed by blob SHA, refreshed on open when stale and by hand; Postgres full-text search with snippets; web and desktop; WCAG AA); the details below were decided by Claude under ADR-0023.
- **Date:** 2026-11-25
- **Deciders:** Davit, Claude
- **Builds on:** ADR-0002 (tenancy and RLS), ADR-0025 (colours from theme tokens), ADR-0026 (role policies), ADR-0028 (the GitHub App)

## Context

A project's docs live in a folder of its linked repository (ADR-0028). Reading GitHub on every page view would be slow, would spend the installation's rate limit (5,000 requests an hour) and would leave the page empty whenever GitHub is down; search needs the text anyway. The docs are written by the team, but they arrive from GitHub, so the page must treat them as untrusted: raw HTML, links and images included. The desktop app runs the same web app under a strict content security policy, and an `<img>` cannot send the bearer token the API needs.

## Decision

**Cache.** Two tables in the `github` schema, both with `org_id` and RLS: `doc_trees` (one per project link: the branch head commit it was read at, when GitHub was last asked, whether the folder is missing or cut short, and the repository, branch and folder it was read from) and `doc_files` (path, blob SHA, title, text, size and the file's last commit). Deleting a link or a connection deletes its cache through foreign keys; changing a link to another repository, branch or folder discards the cached files at the next read.

**Refresh.** A read refreshes the cache when it is older than 5 minutes; `GET /docs?refresh=true` refreshes it now. A refresh asks for the branch head first (the branches API, which sends the commit without its file patches) and stops there when it has not moved. Otherwise it reads the docs folder's git tree in one call and fetches only the blobs whose SHA changed, at most 100 per refresh (4 at a time); others are fetched on later refreshes or when opened. Files over 1 MB and beyond 1,000 per folder are not cached. All GitHub calls happen before one short write transaction; two refreshes racing end with the same result. When GitHub cannot be reached, the last cache is served marked `stale`. Refreshing changes only a copy of GitHub's data, so it is a read (`ReadWork`), and no activity events are written for it.

**Search.** `doc_files.search` is a generated `tsvector` (title weight A, the first 100,000 characters of the text weight B; Postgres caps a `tsvector` at 1 MB, and a doc of many distinct words, such as a pasted base64 image, would otherwise fail its insert and every refresh after it) with a GIN index, using the `simple` configuration, which treats every language alike and keeps code names as written. Every word of a query must match, by prefix; only letters, digits and `_` are taken from the query, so nothing the user types is read as `tsquery` syntax. Snippets come from `ts_headline` with private-use markers, split on the server into plain and matched runs, so the browser never renders HTML from a snippet.

**Paths and images.** The API takes paths inside the docs folder only (no leading `/`, no `.` or `..`, no empty segments). `/docs/image` serves PNG, JPEG, GIF, WebP and SVG up to 5 MB, read at the cached commit (an image never starts a refresh of its own), with `X-Content-Type-Options: nosniff` and a sandboxing `Content-Security-Policy`. The browser fetches images with the user's token and shows them as `data:` URLs, which the desktop policy already allows and which cannot be opened as a page of the app's origin (an SVG's scripts never run). Images from outside the docs folder or from other hosts are shown as links and never loaded, as in item descriptions; `<source srcset>` inside `<picture>` is dropped for the same reason.

**Rendering.** The browser renders Markdown with react-markdown: GitHub-flavoured Markdown, raw HTML parsed and then sanitised with GitHub's own allow-list (`rehype-sanitize`'s default schema), heading anchors, and highlight.js classes added after sanitising and coloured from four new theme tokens (`--code-keyword`, `-string`, `-number`, `-title`) whose contrast the theme test checks. Mermaid is loaded only for a page with a diagram, runs with `securityLevel: 'strict'` and takes its colours from the theme tokens. Relative links to Markdown files in the docs folder open in Trekky (`/KEY/docs/<path>`); other repository files open on GitHub. Heading anchors keep plain ids; ids from a doc's HTML and its footnotes keep the sanitiser's `user-content-` prefix, and in-page links find either form, as on GitHub. The editor of TREK-76 reuses this renderer for its preview.

## Consequences

- Opening the docs costs one GitHub call when nothing changed and a handful when something did; GitHub being down leaves the last copy readable.
- A large folder is complete after a few refreshes rather than on the first open; search says when it was incomplete. Text past a doc's first 100,000 characters is shown but not searched.
- A forced refresh is not throttled: one that finds the branch unmoved costs one GitHub call, so a reader would need thousands of clicks an hour to reach the installation's limit. Add a throttle if that ever happens.
- `GET /docs/file` sends the blob SHA as its ETag, which TREK-76 uses for its conflict check.
- The cache can hold up to 1 GB per project in theory (1,000 files of 1 MB); real docs folders are far smaller. If one is not, lower the limits.
- New theme colours for code follow ADR-0025: a token in both themes and a contrast pair in the test.

## Alternatives considered

- **Rendering on the server.** One renderer for web and desktop, but the editor's live preview (TREK-76) would need a round trip per keystroke, and the sanitising would be the same either way.
- **The contents API per file instead of the tree and blob APIs.** One call per file on every refresh, and no SHA to skip unchanged files.
- **The `english` search configuration.** Stemming helps English prose, but mangles code names and other languages; prefix matching covers most of what stemming would.
- **Blob URLs for images.** They need `blob:` in the desktop policy, and an SVG opened from one runs in the app's origin.
- **Refresh as `POST`.** ADR-0026 makes every non-GET need more than read access; refreshing a cache of GitHub's data is a read, and viewers should be able to do it.
