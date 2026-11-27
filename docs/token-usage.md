# Token usage (TREK-90)

Where Claude's tokens go when it works on Trekky, measured before the TREK-90 changes, so each change can show what it
saved. Every task in TREK-90 records its before and after numbers in the [log](#before-and-after) at the end, in the
same PR.

## How to measure

The numbers come from Claude Code's own session transcripts on this machine (`~/.claude/projects/<folder>/*.jsonl`,
subagents in `<session>/subagents/`). Every API response there carries its token usage, model and git branch.
`tools/token-usage` adds them up:

```bash
cd tools/token-usage
node usage.ts --since 2026-11-20 --until 2026-11-26   # one week, all TRKs
node usage.ts --trk TREK-92                           # one TRK, every session that worked on it
node usage.ts --json > usage.json                     # every field, for your own comparisons
```

It prints Markdown tables: totals, by model, main loop against each subagent type, by TRK, and the tool outputs that
fill the context. See [its README](../tools/token-usage/README.md) for every option.

What the columns mean:

- **Input (all)**: every input token sent: uncached, cache writes and cache reads. **Avg context** is that per request.
- **Cache hit**: the share of input read from the prompt cache. Cache reads cost a twentieth of normal input on
  Opus 5.5, so a falling hit rate is a cost even when the token count stays the same.
- **API-price eq.**: tokens weighted at API prices (`tools/token-usage/prices.json`). This is not a bill (a Claude
  plan is not charged per token). It folds cheap cache reads, cache writes, output and the model into one number that
  can be compared before and after.
- **Input by source** (approximate):
  - **start prefix**: what the session's first request held (system prompt, tool definitions, CLAUDE.md, memory,
    skill list), sent again with every request;
  - **carried over**: what a TRK inherited from earlier work in the same session, on another branch;
  - **tool outputs**: command and tool results, once and on every later request that reads them again;
  - **conversation**: the rest (prompts, Claude's replies, the file contents in Write and Edit calls, reminders).
- **Tool output: Carried**: an output's tokens times the later requests on the same branch that read it again from
  the cache. A 2k-token log read early in a 300-request session costs about 600k tokens of cache reads; this column
  ranks what outputs really cost. With `--since`, `--until` or `--trk`, only re-reads by requests inside the window
  count.

Two views in Claude Code itself complete the picture. Neither can be read from a script:

- `/context` at the start of a session lists what the start prefix is made of (system prompt, tools, MCP tools,
  memory files, skills). Run it when a change targets the prefix (TREK-94, TREK-95, TREK-96).
- `/usage` shows plan usage, split by skill, subagent, plugin and MCP server, with its own recommendations. It covers
  all projects on the account, not only Trekky.

### Measuring a TREK-90 task

The TRK is read from the git branch of the session's working folder, so:

- Start the session in the TRK's worktree, or switch the checkout the session runs in to the TRK branch before the
  work starts. Work done in a worktree from a session in the main checkout counts under the main checkout's branch.
- After a merge, switch the main checkout back to `dev` (or end the session). Otherwise everything after the merge
  still counts under the merged TRK. TREK-98's own numbers are mixed for this reason: a quarter of its requests came
  after its merge, and this TREK-91 work counted under it too.
- Compare like with like: a TRK of the same kind, in a session of its own, against the baseline rows below.

### Limits

- Only Claude Code CLI sessions on this machine count: not cloud sessions, other machines or claude.ai chats.
- Tool output tokens are estimated at 4 characters per token and 1,600 per screenshot. When the estimate is larger
  than the input left for it, the report says so and the split is unreliable.
- Re-reads ignore cache expiry: a request after a long pause writes the cache again instead of reading it, and it is
  still counted as a re-read. A session that goes back to an earlier branch counts that branch's old outputs as
  re-reads again.
- The default reads every transcript folder whose name starts with this repo's path, so a sibling folder such as
  `trekky-host` counts too. Use `--dir` to choose folders.
- The transcript format belongs to Claude Code and can change. The tool skips records it does not know, so check that
  the totals look plausible after a Claude Code update.

## Baseline (2026-11-27)

All sessions ran on Claude Opus 5.5 (subagents too, except the built-in web-fetch one on Haiku 5.5). Data: 26 local
sessions in this repo from 2026-10-26 to 2026-11-26. Shares are rounded.

| Period or TRK | Days | Sessions | Requests | Subagent runs | Input (all) | Avg context | Output | Cache hit | API-price eq. | Input by source: prefix / carried over / tool outputs / conversation | Subagents |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |
| One week | 2026-11-20..26 | 5 | 563 | 16 | 74.5M | 132k | 506k | 96% | $44.40 | 38% / 28% / 10% / 24% | general-purpose 9× $7.89, code-reviewer 5× $5.21, web-fetch 2× $0.03 |
| TREK-81 (small UI fix) | 2026-11-23 | 1 | 74 | 2 | 7.5M | 102k | 36k | 98% | $2.91 | 49% / 17% / 12% / 22% | code-reviewer 1× $0.38, web-fetch 1× $0.01 |
| TREK-53 (list filter bar, early in its session) | 2026-11-18 | 1 | 110 | 1 | 18.0M | 164k | 114k | 97% | $9.95 | 30% / 9% / 20% / 42% | code-reviewer 1× $1.99 |
| TREK-58 (themes across the app, three TRKs later in the same session) | 2026-11-18..19 | 1 | 96 | 1 | 40.6M | 423k | 75k | 98% | $15.56 | 10% / 78% / 4% / 8% | code-reviewer 1× $1.78 |
| TREK-59 (status tones, UI feature) | 2026-11-19..23 | 3 | 114 | 9 | 29.0M | 254k | 205k | 97% | $14.50 | 21% / 69% / 3% / 7% | general-purpose 8× $5.01, code-reviewer 1× $0.57 |
| TREK-35 (largest, desktop app) | 2026-11-13..19 | 5 | 1094 | 10 | 411.1M | 376k | 937k | 99% | $135.96 | 12% / 38% / 12% / 39% | code-reviewer 10× $13.13 |
| All history | 2026-10-26..11-26 | 26 | 3917 | 57 | 1071.8M | 274k | 3.8M | 98% | $420.54 | 17% / 43% / 10% / 30% | code-reviewer 46× $50.22, general-purpose 9× $7.89, web-fetch 2× $0.03 |

Context at session start: 55k tokens (median of the first requests). Compactions: 2 in all history.

Tool outputs by what they cost (all history, the largest rows; "Output" counts each result once, "Carried" counts
every later re-read):

| Tool | Calls | Output | Carried | Share of carried |
| --- | ---: | ---: | ---: | ---: |
| Bash: cat | 280 | 389k | 18.8M | 17% |
| Read | 487 | 676k | 16.3M | 15% |
| Bash: sed | 230 | 199k | 11.4M | 11% |
| mcp:claude_ai_Notion | 61 | 113k | 7.1M | 7% |
| Bash (no command name: variables, quoted paths) | 176 | 61k | 6.1M | 6% |
| mcp:playwright | 321 | 67k | 6.0M | 6% |
| Bash: grep | 281 | 140k | 6.0M | 6% |
| Bash: python | 257 | 44k | 5.3M | 5% |
| Bash: shell loop | 84 | 75k | 3.3M | 3% |
| Bash: git diff | 84 | 198k | 3.0M | 3% |
| Bash: ls | 79 | 55k | 2.3M | 2% |
| Bash: git status | 112 | 164k | 2.3M | 2% |
| All build, test and CI commands together (dotnet build/test/restore/run, npm ci/test/run, npx vitest/playwright/tsc, gh run/pr) | 409 | 66k | 4.1M | 4% (each under 1%) |

### What the baseline says

- **Cache reads are almost all of the input (98%).** Cost follows context length times the number of requests, so
  the levers that shorten sessions and keep the context small matter most.
- **Context carried over from an earlier TRK in the same session is the largest share (43%).** TREK-53, 54, 56 and
  58 ran one after another in one session: carried-over context grew from 9% of the first one's input to 78% of the
  fourth one's, and its average context from 164k to 423k tokens. That is the TREK-95 lever (one TRK per session,
  `/clear` after a merge).
- **The start prefix is 55k tokens and 17% of input** (38% in the last week, which had shorter sessions). TREK-94
  (Playwright MCP out), TREK-95 (lean CLAUDE.md) and TREK-96 shrink or grow it; `/context` shows its parts.
- **Build, test and CI output is small (4% of carried tool tokens).** Claude Code already shortens long command
  output. The outputs that cost most are file reads (`cat`, `Read`, `sed`: 43%). TREK-92 should measure what it
  saves before adding hooks or rtk.
- **Subagents are 14% of the API-price equivalent** (30% in the last week). code-reviewer runs on Opus 5.5 at about
  $1.10 a run. Moving it to Sonnet 5.5 (half the price per token) is the TREK-93 lever.

## Before and after

Each TREK-90 task adds its row in its own PR: what it measured, the baseline value and the value after the change,
taken with `node usage.ts` on comparable sessions (or `/context` for the start prefix).

| Task | PR | Measure | Before | After |
| --- | --- | --- | --- | --- |
| TREK-92 Trim tool output | | | | |
| TREK-93 Model and effort per subagent | | | | |
| TREK-94 Playwright CLI instead of MCP | | | | |
| TREK-95 Session habits, lean CLAUDE.md | | | | |
| TREK-96 Code intelligence plugins | | | | |
| TREK-97 Token rules for agent orchestration | | | | |
