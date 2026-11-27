# Token usage (TREK-91)

Adds up the tokens of Claude Code sessions in this repo from Claude Code's local transcripts, so each token change
can be measured before and after (TREK-90). What the numbers mean and the baseline: [docs/token-usage.md](../../docs/token-usage.md).

Nothing leaves the machine: it reads `~/.claude/projects` (or `$CLAUDE_CONFIG_DIR/projects`) and prints Markdown or
JSON. The transcripts hold whole conversations, so paste only the tables into PRs and docs, never the files.

## Run it

Needs Node 22.18 or later (it runs the TypeScript directly) and git (to find the repo's transcript folders).

```bash
cd tools/token-usage
node usage.ts                                        # all history
node usage.ts --since 2026-11-20 --until 2026-11-26  # a period (UTC days, both ends included)
node usage.ts --trk TREK-92 --trk TREK-93            # only requests on these TRKs' branches
node usage.ts --top 30                               # longer tables (default 15 rows)
node usage.ts --json                                 # the full report as JSON
node usage.ts --dir ~/.claude/projects/<folder>      # other transcript folders (repeatable)
```

By default it reads every transcript folder of this repo: Claude Code names a folder after the session's working
path, so the main checkout and every path that starts with it (`trekky-backend`, `trekky-wt-TREK-n` worktrees) count.

## How it counts

- One API response is one request: Claude Code writes a line per content block, all with the same message id. A
  resumed session repeats some responses of the one it continues; each id counts once, in the oldest file.
- `<synthetic>` responses are placeholders Claude Code writes for failed requests and are skipped.
- Usage is read from `message.usage` (uncached input, 5-minute and 1-hour cache writes, cache reads, output).
- A subagent's transcript is `<session>/subagents/agent-<id>.jsonl`; its type comes from the `.meta.json` next to it.
- The TRK is taken from the request's git branch (`TREK-91-token-baseline` → `TREK-91`).
- Tool outputs are matched to their tool call; shell calls are named by the command they ran (`Bash: dotnet test`),
  MCP tools by server (`mcp:playwright`); paths, option values and inline code are left out of the name. A re-read is
  a later request on the same branch until a compaction, counted only when that request is inside the window.
- `prices.json` holds API prices per million tokens to weight token kinds into one number; models not in it are
  counted but left out of that number. Update it when prices or models change.

## Checks

```bash
npm test                                      # node:test, from this folder
../../frontend/node_modules/.bin/tsc -p .     # strict type check with the frontend's TypeScript
```

CI runs both (`.github/workflows/ci.yml`, frontend job).
