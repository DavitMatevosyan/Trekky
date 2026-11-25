# ADR-0029: The code graph is built by CI and published on a `graphify` branch

- **Status:** Superseded by ADR-0030 (2026-11-25: graphify removed). Was Agreed. The requirement is Davit's (TREK-82, 2026-11-23): CI keeps the graph current on a separate `graphify` branch, nobody rebuilds it locally, `graphify-out/` never enters the working branches, and the docs pass is manual and optional. Claude decided the rest under ADR-0023: the code-only build, the committed docs layer, the session fetch and the guarded hooks.
- **Date:** 2026-11-23
- **Deciders:** Davit, Claude
- **Builds on:** ADR-0012 (an evidence-only branch outside the PR flow), ADR-0022 (protected branches), ADR-0023

## Context

graphify builds a knowledge graph of the repo (`graphify-out/graph.json`, about 6 MB) that Claude queries instead of grepping and reading whole files. Committing it to `dev` would put a multi-megabyte rewrite into every PR. Every session would also have to rebuild it and keep it in sync by hand. The graph has two parts:
- **Code:** local AST extraction, which is free and takes about 30 s.
- **Docs, ADRs and images:** a model pass that cost about 620k tokens on 2026-11-20.

Sessions also run where graphify is not installed (cloud sessions, a fresh PC). There, the graph hooks must not fail.

## Decision

The `Code graph` workflow builds the graph from `dev` and publishes it on the orphan branch `graphify`. Sessions restore it into the git-ignored `graphify-out/`. The rules:

- **Who writes the branch:** only `.github/workflows/graphify.yml`, on a push to `dev` (or a manual run on `dev`). Each build replaces the branch with a single new orphan commit (a force-update), so the branch never grows. This is the repo's only force-update, and Claude never pushes to the branch. It is not protected (ADR-0022), because it holds no source.
- **What it holds:** `graphify-out/graph.json`, `GRAPH_REPORT.md`, `graph.html`, `manifest.json` (relative paths and hashes) and `source.json` (dev commit, graphify version, build time, run). Nothing with a machine path in it is published.
- **How CI builds it:** `graphify extract . --code-only` with a pinned `graphifyy` version. No model and no API key. It merges in the docs layer, then `graphify cluster-only . --no-label`, so community names are placeholders. A failed or empty build fails the job and publishes nothing. `.graphifyignore` leaves out generated files, lock files and data.
- **Token safety:** the build job installs graphify and its dependencies with a read-only token and no persisted credentials, and hands the files on as an artifact. Only the publish job holds `contents: write`, and it runs git alone (no checkout, no installed tool). The one non-GitHub action (`astral-sh/setup-uv`) is pinned by commit SHA.
- **PRs:** a PR that changes the workflow, `.graphifyignore` or `tools/graphify/` runs the build without publishing.
- **Docs layer:** a manual docs pass (`/graphify .` locally, or `--backend claude` with a key) is exported to `tools/graphify/docs-layer.json` and committed through a normal PR. CI merges it into every build until the next pass. Nodes from deleted docs, and links to code that no longer exists, are dropped at merge time.
- **Sessions:** a SessionStart hook (`tools/graphify/fetch-graph.sh`, also runnable by hand) restores `graphify-out/` when the branch has moved or the local `graph.json` no longer matches the published one. It stages nothing, and stays quiet offline or when the branch does not exist yet. The graphify PreToolUse and compact hooks run only when `graphify` is on the PATH.
- **Never:** commit `graphify-out/`, install graphify's git hooks or `graph.json` merge driver, or run `graphify update` after edits.

## Consequences

- **Easier:**
  - PRs carry no graph churn.
  - Every session, worktree and cloud session starts with the graph of the current `dev`, with no local build.
  - The docs layer is reviewed like code.
  - A new version of graphify is one line to change in the workflow.
- **Harder:**
  - The graph matches `dev`, not the open branch. A session's own changes are not in it until they merge.
  - Community names are placeholders.
  - The docs layer goes stale until someone runs a docs pass.
  - Fetching costs one `git ls-remote` per session start.
- **Follow-ups:** none required. Possible later: named communities in CI (needs a model, so it costs money and is Davit's call), strict hook mode, and graphify's MCP server (TREK-82 leaves both off; see its PR).

## Alternatives considered

- **Commit `graphify-out/` to `dev` and rebuild it in git hooks:** a large diff in every PR, merge conflicts on `graph.json`, and every machine needs graphify installed.
- **Each session builds its own graph:** about 30 s per session plus a local install. Cloud sessions can't do it, and the docs layer would be lost.
- **Run the docs pass in CI with an API key secret:** costs money on every docs change. Davit asked to keep CI code-only.
- **Keep the docs layer on the `graphify` branch:** only CI may write there, so a docs pass run on a PC could not reach it without a secret in CI.
- **`graphify merge-graphs` for the docs layer:** it is built for graphs from different repos. It prefixes every id, so docs and code nodes would not join.
