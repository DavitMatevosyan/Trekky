# Code graph (graphify)

[graphify](https://pypi.org/project/graphifyy/) turns the repo into a knowledge graph (`graphify-out/graph.json`) that
Claude queries instead of grepping and reading whole files. ADR-0029 sets the rules; this is the how-to.

## How the graph gets to you

1. On every push to `dev`, `.github/workflows/graphify.yml` builds the graph from code only (local AST, no model, no
   API key), merges in the docs layer (`docs-layer.json`, below), and publishes `graphify-out/` as one orphan commit on
   the `graphify` branch, replacing the previous one. `graphify-out/source.json` names the dev commit.
2. When a Claude Code session starts, `.claude/settings.json` runs `fetch-graph.sh`. It restores `graphify-out/`
   from that branch if it changed, never stages anything, and stays quiet when offline or when the branch does not exist
   yet. It works in worktrees and cloud sessions and needs no graphify install.
3. To use the graph, install graphify (below) and run `graphify query "<question>"`, `graphify path "<A>" "<B>"` or
   `graphify explain "<concept>"`. Without graphify, read `graphify-out/GRAPH_REPORT.md`. The graph hooks in
   `.claude/settings.json` do nothing when graphify is not installed.

Restore by hand: `bash tools/graphify/fetch-graph.sh`. Rebuild or republish by hand: run the workflow (Actions →
Code graph → Run workflow on `dev`). Nobody rebuilds the graph locally for day-to-day work. A PR that changes the
workflow, `.graphifyignore` or `tools/graphify/` runs the build (and `test_docs_layer.py`) without publishing.

The branch is one commit with no parents, so a fetch downloads only the current graph. A plain `git fetch` of the remote
also fetches it, like every branch. Replaced graphs stay in `.git` until git's normal cleanup.

## Install graphify (optional, for queries)

```bash
winget install astral-sh.uv              # Windows; elsewhere see https://docs.astral.sh/uv/
uv tool install "graphifyy[sql]==0.9.113"   # two y's, same spec as CI; avoid plain pip on Windows (it can pick another Python)
graphify --version
```

If `graphify` is not found afterwards, run `uv tool update-shell` and open a new terminal. Keep the version in step
with `GRAPHIFY_VERSION` in the workflow.

## Docs pass (optional, manual)

CI reads code only. What a model reads out of docs, ADRs and images (concepts, doc-to-code links) comes from a docs
pass, which costs tokens, so it runs only when the docs have changed a lot. Its result, the docs layer, is committed as
`tools/graphify/docs-layer.json` and merged into every CI build until the next docs pass. A doc that has been deleted,
and a link to code that no longer exists, drop out at merge time.

1. On a `TREK-<n>-…` branch, build the full graph locally with one of:
   - `/graphify .` in Claude Code (the model work runs on your Claude plan, no API key), or
   - `graphify extract . --backend claude` with `ANTHROPIC_API_KEY` set (paid API).
2. Export the layer: `python tools/graphify/docs_layer.py export` (reads `graphify-out/graph.json`).
3. Commit `tools/graphify/docs-layer.json` in a PR as usual. After the merge to `dev`, the workflow publishes a graph
   with the new layer.

The current layer comes from the full build of 2026-11-20 (dev f3af0e2).
