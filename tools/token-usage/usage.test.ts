import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { buildReport, composition, formatReport, groupBy, median, requestCost, toolRows, totals, trkOf, type Prices } from './report.ts'
import { commandFamily, parseTranscript, toolLabel, type ApiRequest } from './transcript.ts'

const prices: Prices = {
  'claude-opus-5-5': { input: 4, output: 20, cacheRead: 0.2 },
  'claude-haiku-5-5': { input: 0.1, output: 0.5, cacheRead: 0.01 },
}

const main = { sessionId: 's1', run: 's1', agent: 'main' }

type Line = Record<string, unknown>
const jsonl = (...lines: Line[]): string => lines.map((line) => JSON.stringify(line)).join('\n')

function assistant(id: string, content: unknown[], usage: Line = {}, branch = 'TREK-91-token-baseline'): Line {
  return {
    type: 'assistant',
    gitBranch: branch,
    timestamp: '2026-11-25T10:00:00.000Z',
    message: {
      id,
      model: 'claude-opus-5-5',
      content,
      usage: {
        input_tokens: 2,
        cache_creation_input_tokens: 100,
        cache_read_input_tokens: 1000,
        output_tokens: 50,
        cache_creation: { ephemeral_5m_input_tokens: 0, ephemeral_1h_input_tokens: 100 },
        ...usage,
      },
    },
  }
}

const toolUse = (id: string, name: string, input: unknown = {}): Line => ({ type: 'tool_use', id, name, input })
const toolResult = (id: string, content: unknown): Line => ({
  type: 'user',
  gitBranch: 'TREK-91-token-baseline',
  timestamp: '2026-11-25T10:00:01.000Z',
  message: { role: 'user', content: [{ type: 'tool_result', tool_use_id: id, content }] },
})

function request(overrides: Omit<Partial<ApiRequest>, 'usage'> & { usage?: Partial<ApiRequest['usage']> } = {}): ApiRequest {
  return {
    id: 'm',
    sessionId: 's1',
    run: 's1',
    agent: 'main',
    model: 'claude-opus-5-5',
    timestamp: '2026-11-25T10:00:00.000Z',
    branch: 'TREK-91-token-baseline',
    runStart: 0,
    branchStart: 0,
    ...overrides,
    usage: { input: 0, cacheWrite5m: 0, cacheWrite1h: 0, cacheRead: 0, output: 0, ...overrides.usage },
  }
}

describe('parseTranscript', () => {
  it('counts one API response once, although Claude Code writes a line per content block', () => {
    const text = jsonl(assistant('msg_1', [{ type: 'thinking' }]), assistant('msg_1', [{ type: 'text', text: 'hi' }]), assistant('msg_2', []))
    const { requests } = parseTranscript(text, main)
    assert.deepEqual(
      requests.map((r) => r.id),
      ['msg_1', 'msg_2'],
    )
    assert.deepEqual(requests[0]!.usage, { input: 2, cacheWrite5m: 0, cacheWrite1h: 100, cacheRead: 1000, output: 50 })
    assert.equal(requests[0]!.branch, 'TREK-91-token-baseline')
    assert.equal(requests[0]!.agent, 'main')
  })

  it('records the context each run and each branch started with', () => {
    const text = jsonl(
      assistant('msg_1', [], { cache_read_input_tokens: 0 }, 'TREK-90'),
      assistant('msg_2', [], {}, 'TREK-90'),
      assistant('msg_3', [], { cache_read_input_tokens: 5000 }, 'TREK-91-token-baseline'),
      assistant('msg_4', [], { cache_read_input_tokens: 6000 }, 'TREK-91-token-baseline'),
    )
    assert.deepEqual(
      parseTranscript(text, main).requests.map((r) => [r.runStart, r.branchStart]),
      [
        [102, 102],
        [102, 102],
        [102, 5102],
        [102, 5102],
      ],
    )
  })

  it('treats cache writes without the 5m/1h split as 5-minute writes', () => {
    const line = assistant('msg_1', [])
    delete ((line.message as Line).usage as Line).cache_creation
    assert.equal(parseTranscript(jsonl(line), main).requests[0]!.usage.cacheWrite5m, 100)
  })

  it('takes 5-minute cache writes as the writes that are not 1-hour ones', () => {
    const line = assistant('msg_1', [], { cache_creation_input_tokens: 300, cache_creation: { ephemeral_1h_input_tokens: 100 } })
    assert.deepEqual(parseTranscript(jsonl(line), main).requests[0]!.usage.cacheWrite5m, 200)
  })

  it('skips the placeholders Claude Code writes for failed requests', () => {
    const failed = assistant('msg_x', [{ type: 'text', text: 'API Error' }], { input_tokens: 0, cache_creation_input_tokens: 0, cache_read_input_tokens: 0, output_tokens: 0 })
    ;(failed.message as Line).model = '<synthetic>'
    assert.deepEqual(
      parseTranscript(jsonl(failed, assistant('msg_1', [])), main).requests.map((r) => r.id),
      ['msg_1'],
    )
  })

  it('counts a response once across files: a resumed session repeats part of the one it continues', () => {
    const seen = new Set<string>()
    const first = parseTranscript(jsonl(assistant('msg_1', []), assistant('msg_2', [])), main, { seen })
    const resumed = parseTranscript(jsonl(assistant('msg_2', []), assistant('msg_3', [])), { ...main, sessionId: 's2', run: 's2' }, { seen })
    assert.deepEqual(first.requests.map((r) => r.id), ['msg_1', 'msg_2'])
    assert.deepEqual(resumed.requests.map((r) => r.id), ['msg_3'])
  })

  it('does not count a tool output again in the session that copied it, only its new re-reads', () => {
    const seen = new Set<string>()
    const history = [assistant('msg_1', [toolUse('t1', 'Read')]), toolResult('t1', 'x'), assistant('msg_2', [])]
    const first = parseTranscript(jsonl(...history), main, { seen })
    const resumed = parseTranscript(jsonl(...history, assistant('msg_3', [])), { ...main, sessionId: 's2', run: 's2' }, { seen })
    assert.deepEqual(first.toolOutputs.map((o) => [o.arrived, o.rereads]), [[true, 0]])
    assert.deepEqual(resumed.toolOutputs.map((o) => [o.arrived, o.rereads]), [[false, 1]])
  })

  it('counts compactions inside the window only', () => {
    const compaction = (day: string): Line => ({ type: 'system', subtype: 'compact_boundary', timestamp: `2026-11-${day}T10:00:00.000Z`, gitBranch: 'dev' })
    const text = jsonl(compaction('20'), compaction('21'), assistant('msg_1', []))
    const onlyThe21st = (timestamp: string): boolean => timestamp.startsWith('2026-11-21')
    assert.equal(parseTranscript(text, main, { window: onlyThe21st }).compactions, 1)
  })

  it('skips broken lines and records it does not know', () => {
    const text = [JSON.stringify({ type: 'mode', mode: 'auto' }), '{"type":"assist', JSON.stringify(assistant('msg_1', []))].join('\n')
    assert.equal(parseTranscript(text, main).requests.length, 1)
  })

  it('attributes each tool output to its tool, with text size and screenshots', () => {
    const text = jsonl(
      assistant('msg_1', [toolUse('t1', 'Bash', { command: 'cd backend && dotnet test --no-build' }), toolUse('t2', 'mcp__playwright__browser_take_screenshot')]),
      toolResult('t1', 'Passed! 12 tests'),
      toolResult('t2', [{ type: 'text', text: 'saved' }, { type: 'image', source: {} }]),
      assistant('msg_2', []),
    )
    const outputs = parseTranscript(text, main).toolOutputs
    assert.deepEqual(
      outputs.map((o) => [o.tool, o.chars, o.images]),
      [
        ['Bash: dotnet test', 16, 0],
        ['mcp:playwright', 5, 1],
      ],
    )
  })

  it('counts how many later requests re-read an output, stopping at a compaction', () => {
    const text = jsonl(
      assistant('msg_1', [toolUse('t1', 'Read')]),
      toolResult('t1', 'x'.repeat(400)),
      assistant('msg_2', []),
      assistant('msg_3', [toolUse('t2', 'Read')]),
      toolResult('t2', 'y'),
      assistant('msg_4', []),
      { type: 'system', subtype: 'compact_boundary' },
      assistant('msg_5', []),
      assistant('msg_6', []),
    )
    const transcript = parseTranscript(text, main)
    assert.equal(transcript.compactions, 1)
    // t1 arrives after msg_1: msg_2 writes it to the cache, msg_3 and msg_4 re-read it, the compaction drops it.
    assert.deepEqual(
      transcript.toolOutputs.map((o) => o.rereads),
      [2, 0],
    )
  })

  it('counts only re-reads inside the window, also for an output that arrived before it', () => {
    const at = (id: string, day: string, content: unknown[] = []): Line => ({ ...assistant(id, content), timestamp: `2026-11-${day}T10:00:00.000Z` })
    const text = jsonl(
      at('msg_1', '20', [toolUse('t1', 'Read')]),
      { ...toolResult('t1', 'x'.repeat(40)), timestamp: '2026-11-20T10:00:01.000Z' },
      at('msg_2', '20'),
      at('msg_3', '20'),
      at('msg_4', '21'),
      at('msg_5', '22'),
    )
    const onlyThe21st = (timestamp: string): boolean => timestamp.startsWith('2026-11-21')
    const transcript = parseTranscript(text, main, { window: onlyThe21st })
    assert.deepEqual(transcript.requests.map((r) => r.id), ['msg_4'])
    // Arrived on the 20th (not counted itself), re-read once on the 21st; msg_3 and msg_5 are outside the window.
    assert.deepEqual(
      transcript.toolOutputs.map((o) => [o.arrived, o.rereads]),
      [[false, 1]],
    )
    assert.deepEqual(transcript.start, { timestamp: '2026-11-20T10:00:00.000Z', branch: 'TREK-91-token-baseline', context: 1102 })
  })

  it('does not count requests on a later branch as re-reads: they inherit it as carried-over context', () => {
    const text = jsonl(
      assistant('msg_1', [toolUse('t1', 'Read')], {}, 'TREK-91-token-baseline'),
      toolResult('t1', 'x'),
      assistant('msg_2', [], {}, 'TREK-91-token-baseline'),
      assistant('msg_3', [], {}, 'TREK-92-quiet-output'),
      assistant('msg_4', [], {}, 'TREK-92-quiet-output'),
    )
    assert.deepEqual(
      parseTranscript(text, main).toolOutputs.map((o) => o.rereads),
      [0],
    )
  })
})

describe('toolLabel and commandFamily', () => {
  it('groups MCP tools per server', () => {
    assert.equal(toolLabel('mcp__claude_ai_Notion__notion-fetch', {}), 'mcp:claude_ai_Notion')
  })

  it('names shell calls by the command they ran, without cd, env assignments, flags and paths', () => {
    assert.equal(commandFamily('cd /c/repo/backend && dotnet test --no-build -v q'), 'dotnet test')
    assert.equal(commandFamily('CI=1 npm run lint 2>&1 | tail -20'), 'npm run lint')
    assert.equal(commandFamily('gh run view 123 --log-failed'), 'gh run view')
    assert.equal(commandFamily('gh pr create --base dev'), 'gh pr create')
    assert.equal(commandFamily('/usr/bin/git diff --stat'), 'git diff')
    assert.equal(commandFamily(String.raw`C:\Tools\gh.exe pr view 3`), 'gh pr view')
    assert.equal(commandFamily('time npx vitest run'), 'npx vitest')
    assert.equal(commandFamily('if [ -d node_modules ]; then echo ok; fi'), '')
    assert.equal(commandFamily('(dotnet build)'), 'dotnet build')
    assert.equal(commandFamily(''), '')
    assert.equal(toolLabel('PowerShell', { command: 'npm ci' }), 'PowerShell: npm ci')
    assert.equal(toolLabel('Bash', { command: '-x' }), 'Bash')
  })

  it('skips option values and never takes a path, code or a variable as a command name', () => {
    assert.equal(commandFamily('git -C C:/repos/trekky-wt/TREK-91 diff --stat'), 'git diff')
    assert.equal(commandFamily('npm --prefix frontend run lint'), 'npm run lint')
    assert.equal(commandFamily('node -e "console.log(1)"'), 'node -e')
    assert.equal(commandFamily('node C:/Users/someone/scripts/probe.js'), 'node')
    assert.equal(commandFamily('git show HEAD:CLAUDE.md'), 'git show')
    assert.equal(commandFamily('npm run generate:api'), 'npm run generate:api')
    assert.equal(commandFamily('dotnet test C:/repo/backend/Trekky.slnx'), 'dotnet test')
    assert.equal(commandFamily('gh api repos/owner/repo/branches/dev'), 'gh api')
    assert.equal(commandFamily('# check the build\ndotnet build -v q'), 'dotnet build')
    assert.equal(commandFamily('for f in *.jsonl; do wc -l $f; done'), 'shell loop')
    assert.equal(commandFamily('$f = Get-Item x'), '')
  })
})

describe('cost and totals', () => {
  it('weights token kinds at API prices: 1h cache writes 2x input, 5m 1.25x', () => {
    const cost = requestCost(request({ usage: { input: 1_000_000, cacheWrite5m: 1_000_000, cacheWrite1h: 1_000_000, cacheRead: 1_000_000, output: 1_000_000 } }), prices)
    assert.equal(cost, 4 + 5 + 8 + 0.2 + 20)
  })

  it('leaves out requests on models without a price and counts them', () => {
    const t = totals([request({ model: '<synthetic>' }), request({ usage: { cacheRead: 900, input: 100 } })], prices)
    assert.equal(t.requests, 2)
    assert.equal(t.unpriced, 1)
    assert.equal(t.cacheHit, 0.9)
  })

  it('takes the TRK key from the branch', () => {
    assert.equal(trkOf('TREK-91-token-baseline'), 'TREK-91')
    assert.equal(trkOf('trk-11.1-fix'), 'TRK-11.1')
    assert.equal(trkOf('dev'), 'dev')
    assert.equal(trkOf(''), '(no branch)')
  })

  it('groups by TRK with sessions and distinct subagent runs', () => {
    const rows = groupBy(
      [
        request({ id: 'a' }),
        request({ id: 'b', run: 'agent-1', agent: 'code-reviewer' }),
        request({ id: 'c', run: 'agent-1', agent: 'code-reviewer' }),
        request({ id: 'd', sessionId: 's2', run: 's2', branch: 'dev', usage: { output: 1_000_000 } }),
      ],
      (r) => trkOf(r.branch),
      prices,
    )
    assert.deepEqual(
      rows.map((r) => [r.key, r.sessions, r.requests, r.subagentRuns]),
      [
        ['dev', 1, 1, 0],
        ['TREK-91', 1, 3, 1],
      ],
    )
  })

  it('takes the middle value as the median', () => {
    assert.equal(median([3, 1, 2]), 2)
    assert.equal(median([4, 1, 2, 3]), 3)
    assert.equal(median([]), 0)
  })
})

describe('composition and the report', () => {
  const requests = [
    request({ id: 'a', timestamp: '2026-11-25T10:00:00Z', runStart: 100, branchStart: 100, usage: { cacheRead: 100 } }),
    request({ id: 'b', timestamp: '2026-11-25T10:01:00Z', runStart: 100, branchStart: 100, usage: { cacheRead: 150 } }),
    request({ id: 'c', timestamp: '2026-11-25T10:02:00Z', runStart: 100, branchStart: 100, usage: { cacheRead: 200 } }),
  ]
  const outputs = [{ sessionId: 's1', agent: 'main', branch: 'TREK-91', timestamp: '', tool: 'Read', chars: 80, images: 0, arrived: true, rereads: 1 }]

  it('splits input into the start prefix, tool outputs and the rest of the conversation', () => {
    // 450 input tokens: the 100-token prefix three times, a 20-token output plus one re-read, 110 of conversation.
    assert.deepEqual(composition(requests, toolRows(outputs)), { prefix: 300, carriedOver: 0, toolOutputs: 40, conversation: 110, clamped: false })
  })

  it('counts what a TRK inherited from an earlier one in the same session as carried over', () => {
    const later = request({ runStart: 100, branchStart: 160, usage: { cacheRead: 200 } })
    assert.deepEqual(composition([later], []), { prefix: 100, carriedOver: 60, toolOutputs: 0, conversation: 40, clamped: false })
  })

  it('cuts a tool output estimate that does not fit and says so', () => {
    const big = toolRows([{ ...outputs[0]!, chars: 4000, rereads: 0 }])
    assert.deepEqual(composition(requests, big), { prefix: 300, carriedOver: 0, toolOutputs: 150, conversation: 0, clamped: true })
    assert.match(formatReport(buildReport(requests, [{ ...outputs[0]!, chars: 4000, rereads: 0 }], [], prices), 5), /treat this split as unreliable/)
  })

  it('says n/a for the start context when no session started inside the window', () => {
    const report = buildReport(requests, outputs, [{ sessionId: 's1', startContext: 0, compactions: 0 }], prices)
    assert.match(formatReport(report, 5), /Context at session start \(median of first requests\): n\/a/)
  })

  it('counts calls and output tokens only for outputs that arrived in the window, re-reads for all', () => {
    const rows = toolRows([...outputs, { ...outputs[0]!, arrived: false, rereads: 3 }])
    assert.deepEqual(rows.map((r) => [r.calls, r.approxTokens, r.carriedTokens]), [[1, 20, 80]])
  })

  it('ranks tools by carried tokens', () => {
    const rows = toolRows([...outputs, { ...outputs[0]!, tool: 'Bash: cat', chars: 40, rereads: 10 }])
    assert.deepEqual(
      rows.map((r) => [r.tool, r.approxTokens, r.carriedTokens]),
      [
        ['Bash: cat', 10, 100],
        ['Read', 20, 20],
      ],
    )
  })

  it('formats a markdown report with the period, totals and every table', () => {
    const report = buildReport(requests, outputs, [{ sessionId: 's1', startContext: 100, compactions: 0 }], prices)
    const text = formatReport(report, 10)
    assert.match(text, /^## Token usage 2026-11-25 to 2026-11-25/)
    assert.match(text, /Context at session start \(median of first requests\): 100/)
    for (const heading of ['By model', 'Main loop and subagents', 'By TRK (git branch)', 'Tool output entering the context']) {
      assert.ok(text.includes(`### ${heading}`), heading)
    }
  })
})
