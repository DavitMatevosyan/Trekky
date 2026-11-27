// TREK-91: turns parsed transcripts into the baseline tables of docs/token-usage.md.
import { contextOf, type ApiRequest, type ToolOutput, type Usage } from './transcript.ts'

export type ModelPrice = { input: number; output: number; cacheRead: number }
export type Prices = Record<string, ModelPrice>

export type Totals = Usage & {
  requests: number
  /** Share of input tokens read from the prompt cache, 0..1. */
  cacheHit: number
  /** API-price equivalent in USD; requests on models without a price are left out and counted in `unpriced`. */
  cost: number
  unpriced: number
}

export type Row = Totals & { key: string; sessions: number; subagentRuns: number }

export type ToolRow = {
  tool: string
  calls: number
  /** Tokens of the outputs themselves, counted once. */
  approxTokens: number
  /** The same tokens times the later requests that carry them (cache reads until the session ends or compacts). */
  carriedTokens: number
  /** Share of all carried tokens: the ranking that matches what the outputs cost. */
  share: number
}

export type Session = {
  sessionId: string
  /** Tokens sent with the first main request: system prompt, tools, CLAUDE.md, memory (what /context shows at start). */
  startContext: number
  compactions: number
}

export type Report = {
  from: string
  to: string
  sessions: number
  medianStartContext: number
  compactions: number
  total: Totals
  byModel: Row[]
  byAgent: Row[]
  byTrk: Row[]
  byTool: ToolRow[]
  composition: Composition
}

/** Where the input tokens of all requests came from, approximately. */
export type Composition = {
  /** Each run's first request (system prompt, tools, CLAUDE.md, memory, skill list) resent with every request of the run. */
  prefix: number
  /** Context a TRK inherited from earlier work in the same session (another branch before it). */
  carriedOver: number
  /** Tool outputs, once and as carried by later requests on the same branch. */
  toolOutputs: number
  /** Everything else: prompts, Claude's replies and tool calls (file contents of Write/Edit), reminders. */
  conversation: number
  /** The tool output estimate (4 characters per token) was larger than the input left for it and was cut to fit. */
  clamped: boolean
}

/** Roughly what Claude counts for one screenshot; tool output text is counted as 4 characters per token. */
export const IMAGE_TOKENS = 1600

export function requestCost(request: ApiRequest, prices: Prices): number | undefined {
  const price = prices[request.model]
  if (!price) return undefined
  const u = request.usage
  const perToken =
    u.input * price.input +
    u.cacheWrite5m * price.input * 1.25 +
    u.cacheWrite1h * price.input * 2 +
    u.cacheRead * price.cacheRead +
    u.output * price.output
  return perToken / 1_000_000
}

export function totals(requests: ApiRequest[], prices: Prices): Totals {
  const t: Totals = { input: 0, cacheWrite5m: 0, cacheWrite1h: 0, cacheRead: 0, output: 0, requests: 0, cacheHit: 0, cost: 0, unpriced: 0 }
  for (const request of requests) {
    t.requests++
    t.input += request.usage.input
    t.cacheWrite5m += request.usage.cacheWrite5m
    t.cacheWrite1h += request.usage.cacheWrite1h
    t.cacheRead += request.usage.cacheRead
    t.output += request.usage.output
    const cost = requestCost(request, prices)
    if (cost === undefined) t.unpriced++
    else t.cost += cost
  }
  const allInput = t.input + t.cacheWrite5m + t.cacheWrite1h + t.cacheRead
  t.cacheHit = allInput === 0 ? 0 : t.cacheRead / allInput
  return t
}

/** The TRK a branch belongs to (TREK-91-token-baseline → TREK-91), or the branch itself (dev, main). */
export function trkOf(branch: string): string {
  const match = /^(TRK|TREK)-\d+(?:\.\d+)?/i.exec(branch)
  if (match) return match[0].toUpperCase()
  return branch === '' ? '(no branch)' : branch
}

export function groupBy(requests: ApiRequest[], keyOf: (request: ApiRequest) => string, prices: Prices): Row[] {
  const groups = new Map<string, ApiRequest[]>()
  for (const request of requests) {
    const key = keyOf(request)
    const group = groups.get(key)
    if (group) group.push(request)
    else groups.set(key, [request])
  }
  return [...groups.entries()]
    .map(([key, group]) => ({
      key,
      sessions: new Set(group.map((r) => r.sessionId)).size,
      subagentRuns: new Set(group.filter((r) => r.agent !== 'main').map((r) => r.run)).size,
      ...totals(group, prices),
    }))
    .sort((a, b) => b.cost - a.cost || b.requests - a.requests)
}

export function toolRows(outputs: ToolOutput[]): ToolRow[] {
  const byTool = new Map<string, { calls: number; approxTokens: number; carriedTokens: number }>()
  let all = 0
  for (const output of outputs) {
    const tokens = Math.ceil(output.chars / 4) + output.images * IMAGE_TOKENS
    const carried = tokens * output.rereads
    all += carried
    const row = byTool.get(output.tool) ?? { calls: 0, approxTokens: 0, carriedTokens: 0 }
    if (output.arrived) {
      row.calls++
      row.approxTokens += tokens
    }
    row.carriedTokens += carried
    byTool.set(output.tool, row)
  }
  return [...byTool.entries()]
    .map(([tool, row]) => ({ tool, ...row, share: all === 0 ? 0 : row.carriedTokens / all }))
    .sort((a, b) => b.carriedTokens - a.carriedTokens || b.approxTokens - a.approxTokens)
}

export function median(values: number[]): number {
  if (values.length === 0) return 0
  const sorted = [...values].sort((a, b) => a - b)
  const middle = Math.floor(sorted.length / 2)
  return sorted.length % 2 === 1 ? sorted[middle]! : Math.round((sorted[middle - 1]! + sorted[middle]!) / 2)
}

export function composition(requests: ApiRequest[], tools: ToolRow[]): Composition {
  let prefix = 0
  let carriedOver = 0
  let all = 0
  for (const request of requests) {
    const context = contextOf(request.usage)
    all += context
    prefix += Math.min(request.runStart, context)
    carriedOver += Math.max(0, Math.min(request.branchStart, context) - request.runStart)
  }
  const estimate = tools.reduce((sum, row) => sum + row.approxTokens + row.carriedTokens, 0)
  const room = all - prefix - carriedOver
  const toolOutputs = Math.min(room, estimate)
  return { prefix, carriedOver, toolOutputs, conversation: room - toolOutputs, clamped: estimate > room }
}

export function buildReport(requests: ApiRequest[], outputs: ToolOutput[], sessions: Session[], prices: Prices): Report {
  const times = requests.map((r) => r.timestamp).filter((t) => t !== '').sort()
  const byTool = toolRows(outputs)
  return {
    from: times[0]?.slice(0, 10) ?? '',
    to: times.at(-1)?.slice(0, 10) ?? '',
    sessions: sessions.length,
    medianStartContext: median(sessions.map((s) => s.startContext).filter((n) => n > 0)),
    compactions: sessions.reduce((sum, s) => sum + s.compactions, 0),
    total: totals(requests, prices),
    byModel: groupBy(requests, (r) => r.model || '(unknown)', prices),
    byAgent: groupBy(requests, (r) => r.agent, prices),
    byTrk: groupBy(requests, (r) => trkOf(r.branch), prices),
    byTool,
    composition: composition(requests, byTool),
  }
}

const tokens = (n: number): string => (n >= 1_000_000 ? `${(n / 1_000_000).toFixed(1)}M` : n >= 1000 ? `${Math.round(n / 1000)}k` : `${n}`)
const percent = (share: number): string => `${Math.round(share * 100)}%`
const usd = (n: number): string => `$${n.toFixed(2)}`

function rowTable(title: string, rows: Row[], total: number, top: number): string[] {
  const lines = [
    `### ${title}`,
    '',
    '| | Sessions | Requests | Subagent runs | Input (all) | Avg context | Output | Cache hit | API-price eq. | Share |',
    '| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |',
  ]
  for (const row of rows.slice(0, top)) {
    const input = row.input + row.cacheWrite5m + row.cacheWrite1h + row.cacheRead
    lines.push(
      `| ${row.key} | ${row.sessions} | ${row.requests} | ${row.subagentRuns} | ${tokens(input)} | ${tokens(row.requests === 0 ? 0 : Math.round(input / row.requests))} | ${tokens(row.output)} | ${percent(row.cacheHit)} | ${usd(row.cost)} | ${percent(total === 0 ? 0 : row.cost / total)} |`,
    )
  }
  if (rows.length > top) lines.push(`| … ${rows.length - top} more | | | | | | | | | |`)
  return [...lines, '']
}

export function formatReport(report: Report, top: number): string {
  const t = report.total
  const allInput = t.input + t.cacheWrite5m + t.cacheWrite1h + t.cacheRead
  const c = report.composition
  const shareOfInput = (n: number): string => percent(allInput === 0 ? 0 : n / allInput)
  const lines = [
    `## Token usage ${report.from} to ${report.to}`,
    '',
    `- Sessions: ${report.sessions}; API requests: ${t.requests}; compactions: ${report.compactions}`,
    `- Input: ${tokens(allInput)} (uncached ${tokens(t.input)}, cache writes ${tokens(t.cacheWrite5m + t.cacheWrite1h)}, cache reads ${tokens(t.cacheRead)}); output: ${tokens(t.output)}`,
    `- Cache hit: ${percent(t.cacheHit)}; API-price equivalent: ${usd(t.cost)}${t.unpriced > 0 ? ` (${t.unpriced} requests on unpriced models left out)` : ''}`,
    `- Context at session start (median of first requests): ${report.medianStartContext > 0 ? tokens(report.medianStartContext) : 'n/a (no session started inside the window)'}`,
    `- Input by source (approximate): start prefix sent with every request ${shareOfInput(c.prefix)}, carried over from an earlier TRK in the session ${shareOfInput(c.carriedOver)}, tool outputs ${shareOfInput(c.toolOutputs)}, conversation ${shareOfInput(c.conversation)} (rounded)${c.clamped ? '. The tool output estimate was larger than the input left for it and was cut to fit: treat this split as unreliable' : ''}`,
    '',
    ...rowTable('By model', report.byModel, t.cost, top),
    ...rowTable('Main loop and subagents', report.byAgent, t.cost, top),
    ...rowTable('By TRK (git branch)', report.byTrk, t.cost, top),
    '### Tool output entering the context',
    '',
    `Approximate: 4 characters per token, screenshots at ${IMAGE_TOKENS} tokens. "Output" counts each result once; "Carried" multiplies it by the later requests that re-read it from the cache, until the session ends or compacts. Share is of carried tokens.`,
    '',
    '| Tool | Calls | Output | Carried | Share |',
    '| --- | ---: | ---: | ---: | ---: |',
    ...report.byTool.slice(0, top).map((row) => `| ${row.tool} | ${row.calls} | ${tokens(row.approxTokens)} | ${tokens(row.carriedTokens)} | ${percent(row.share)} |`),
    '',
  ]
  return lines.join('\n')
}
