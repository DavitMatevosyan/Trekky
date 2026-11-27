// TREK-91: reads Claude Code session transcripts (~/.claude/projects/<project>/<session>.jsonl and
// <session>/subagents/agent-<id>.jsonl) into API requests and tool outputs. Only the fields below are read; the
// transcript format is Claude Code's own and may change, so unknown records are skipped rather than failing.

export type Usage = {
  input: number
  cacheWrite5m: number
  cacheWrite1h: number
  cacheRead: number
  output: number
}

export type ApiRequest = {
  id: string
  sessionId: string
  /** The main session id, or the subagent id: one run of the main loop or of one subagent. */
  run: string
  /** 'main', or the subagent type (code-reviewer, Explore, ...). */
  agent: string
  model: string
  timestamp: string
  branch: string
  usage: Usage
  /** Context of the run's first request: the prefix every request of the run sends again. */
  runStart: number
  /** Context of the run's first request on this branch; what it has above runStart was carried over from earlier work. */
  branchStart: number
}

export type ToolOutput = {
  sessionId: string
  agent: string
  branch: string
  timestamp: string
  /** Tool name; MCP tools grouped per server (mcp:playwright), shell commands per command (Bash: dotnet test). */
  tool: string
  chars: number
  images: number
  /** Whether the output itself arrived inside the window; outside it, only its re-reads inside the window count. */
  arrived: boolean
  /** Counted requests on the same branch that re-read this output from the cache, until a compaction drops it. */
  rereads: number
}

export type Transcript = {
  /** Requests inside the window, each response once (also across files, through `seen`). */
  requests: ApiRequest[]
  /** Outputs that arrived inside the window or were re-read inside it. */
  toolOutputs: ToolOutput[]
  /** Compactions inside the window. */
  compactions: number
  /** The run's first request, inside the window or not. */
  start?: { timestamp: string; branch: string; context: number }
}

export type RunInfo = { sessionId: string; run: string; agent: string }

/** Which requests and outputs to count: a period, a set of TRKs. */
export type Window = (timestamp: string, branch: string) => boolean

export type ParseOptions = {
  window?: Window
  /** Message ids already counted in other files: a resumed session repeats some of the session it continues. */
  seen?: Set<string>
}

type Json = Record<string, unknown>

const isObject = (value: unknown): value is Json => typeof value === 'object' && value !== null && !Array.isArray(value)
const num = (value: unknown): number => (typeof value === 'number' && Number.isFinite(value) ? value : 0)
const str = (value: unknown): string => (typeof value === 'string' ? value : '')

/** All input tokens of one request: uncached, cache writes and cache reads. */
export const contextOf = (u: Usage): number => u.input + u.cacheWrite5m + u.cacheWrite1h + u.cacheRead

/** `repeated`: the response was already counted in another file (a resumed session's copy of its history). */
type Parsed = ApiRequest & { segment: number; counted: boolean; repeated: boolean }
type PendingOutput = Omit<ToolOutput, 'arrived' | 'rereads'> & { at: number; segment: number }

export function parseTranscript(text: string, info: RunInfo, options: ParseOptions = {}): Transcript {
  const window = options.window ?? (() => true)
  const seen = options.seen ?? new Set<string>()
  const requests: Parsed[] = []
  const inFile = new Set<string>()
  const branchStarts = new Map<string, number>()
  let runStart: number | undefined
  let start: Transcript['start']
  const toolNames = new Map<string, string>()
  const outputs: PendingOutput[] = []
  let segment = 0 // compactions so far: each one starts a new context
  let compactions = 0 // compactions inside the window

  for (const line of text.split('\n')) {
    if (line.trim() === '') continue
    let record: unknown
    try {
      record = JSON.parse(line)
    } catch {
      continue // a line cut off by a crash or a write in progress
    }
    if (!isObject(record)) continue
    const branch = str(record.gitBranch)
    const timestamp = str(record.timestamp)

    if (record.type === 'system' && record.subtype === 'compact_boundary') {
      segment++
      if (window(timestamp, branch)) compactions++
    }

    const message = record.message
    if (!isObject(message)) continue

    if (record.type === 'assistant') {
      const content = Array.isArray(message.content) ? message.content : []
      for (const block of content) {
        if (isObject(block) && block.type === 'tool_use') toolNames.set(str(block.id), toolLabel(str(block.name), block.input))
      }
      // One API response is written as one line per content block, each with the same id and usage.
      // '<synthetic>' marks an error placeholder Claude Code wrote itself: no request was made.
      const id = str(message.id)
      const usage = message.usage
      const model = str(message.model)
      if (id === '' || !isObject(usage) || model === '<synthetic>' || inFile.has(id)) continue
      inFile.add(id)
      const read = readUsage(usage)
      const context = contextOf(read)
      runStart ??= context
      start ??= { timestamp, branch, context }
      if (!branchStarts.has(branch)) branchStarts.set(branch, context)
      const repeated = seen.has(id)
      const counted = !repeated && window(timestamp, branch)
      seen.add(id)
      requests.push({
        id,
        sessionId: info.sessionId,
        run: info.run,
        agent: info.agent,
        model,
        timestamp,
        branch,
        usage: read,
        runStart,
        branchStart: branchStarts.get(branch) ?? context,
        segment,
        counted,
        repeated,
      })
    } else if (record.type === 'user' && Array.isArray(message.content)) {
      for (const block of message.content) {
        if (!isObject(block) || block.type !== 'tool_result') continue
        outputs.push({
          sessionId: info.sessionId,
          agent: info.agent,
          branch,
          timestamp,
          tool: toolNames.get(str(block.tool_use_id)) ?? 'unknown',
          ...contentSize(block.content),
          at: requests.length,
          segment,
        })
      }
    }
  }

  // An output enters the context with the next request (a cache write) and is re-read by every request after that
  // on the same branch, until a compaction drops it. Only counted requests count as re-reads, so a re-read belongs to
  // the window of the request that made it. Requests on a later branch count it as carried-over context instead.
  const toolOutputs: ToolOutput[] = []
  for (const { at, segment: outputSegment, ...output } of outputs) {
    const carriers = requests.slice(at).filter((r) => r.segment === outputSegment && r.branch === output.branch)
    const rereads = carriers.slice(1).filter((r) => r.counted).length
    // An output first sent by a repeated response is a copy from the resumed session, already counted there.
    const arrived = window(output.timestamp, output.branch) && requests[at]?.repeated !== true
    if (arrived || rereads > 0) toolOutputs.push({ ...output, arrived, rereads })
  }

  return {
    requests: requests.filter((r) => r.counted).map(({ segment: _segment, counted: _counted, repeated: _repeated, ...request }) => request),
    toolOutputs,
    compactions,
    start,
  }
}

function readUsage(usage: Json): Usage {
  const created = num(usage.cache_creation_input_tokens)
  const split = isObject(usage.cache_creation) ? usage.cache_creation : undefined
  const write1h = split ? num(split.ephemeral_1h_input_tokens) : 0
  return {
    input: num(usage.input_tokens),
    cacheWrite5m: Math.max(0, created - write1h),
    cacheWrite1h: write1h,
    cacheRead: num(usage.cache_read_input_tokens),
    output: num(usage.output_tokens),
  }
}

function contentSize(content: unknown): { chars: number; images: number } {
  if (typeof content === 'string') return { chars: content.length, images: 0 }
  let chars = 0
  let images = 0
  if (Array.isArray(content)) {
    for (const part of content) {
      if (!isObject(part)) continue
      if (part.type === 'image') images++
      else chars += str(part.text).length
    }
  }
  return { chars, images }
}

export function toolLabel(name: string, input: unknown): string {
  if (name.startsWith('mcp__')) return `mcp:${name.split('__')[1] ?? ''}`
  if ((name === 'Bash' || name === 'PowerShell') && isObject(input)) {
    const family = commandFamily(str(input.command))
    return family === '' ? name : `${name}: ${family}`
  }
  return name
}

// Tools whose first argument is a sub-command worth keeping (dotnet test, git diff), and those with a second one
// (npm run lint, gh run view). Options listed with a tool take a value, which is skipped (git -C <path> diff).
const withSubcommand = new Set(['dotnet', 'npm', 'npx', 'git', 'gh', 'docker', 'pnpm', 'yarn', 'cargo'])
const withSecondSubcommand = new Set(['npm run', 'gh run', 'gh pr', 'docker compose', 'dotnet ef'])
const optionsWithValue: Record<string, Set<string>> = {
  git: new Set(['-C', '-c', '--git-dir', '--work-tree']),
  npm: new Set(['--prefix', '-C', '-w', '--workspace']),
  npx: new Set(['--prefix', '-p', '--package']),
  gh: new Set(['-R', '--repo']),
  docker: new Set(['-f', '--file', '-p', '--project-name', '--context']),
  dotnet: new Set(['--project', '-p', '-c', '--configuration', '-f', '--framework']),
}
const shellPrefixes = new Set(['if', 'then', 'else', 'do', 'time', '!', '[', '[[', '(', '{', 'sudo', '&'])
const loops = new Set(['for', 'while', 'until', 'foreach', 'ForEach-Object'])
// A tool or sub-command is a plain word: no paths, drive letters, quotes, variables or code.
const plainTool = /^[A-Za-z][\w.+-]*$/
const plainSubcommand = /^[A-Za-z][\w.@-]*(:[\w.-]+)?$/

/** The command a shell call ran, without `cd …&&` prefixes, env assignments, option values, paths and arguments. */
export function commandFamily(command: string): string {
  const segments = command
    .split(/&&|\|\||;|\||\n/)
    .map((segment) => segment.trim())
    .filter((segment) => segment !== '' && !segment.startsWith('#') && !/^(cd|Set-Location|export|set)\b/i.test(segment))
  const words = (segments[0] ?? '').split(/\s+/).filter((word) => !/^[A-Za-z_][A-Za-z0-9_]*=/.test(word))
  while (words.length > 0 && shellPrefixes.has(words[0]!)) words.shift()
  const clean = (word: string): string => word.replace(/^["'&(]+|["')]+$/g, '')
  const tool = (clean(words[0] ?? '').split(/[\\/]/).pop() ?? '').replace(/\.exe$/i, '')
  if (loops.has(tool)) return 'shell loop'
  if (!plainTool.test(tool)) return ''
  if (tool === 'node') return words.some((word) => /^(-e|-p|--eval|--print)$/.test(word)) ? 'node -e' : 'node'
  const withValue = optionsWithValue[tool]
  const args: string[] = []
  for (let i = 1; i < words.length; i++) {
    const word = words[i]!
    if (word.startsWith('-')) {
      if (withValue?.has(word)) i++ // skip the option's value too
      continue
    }
    args.push(clean(word))
  }
  let family = tool
  if (withSubcommand.has(family) && args[0] && plainSubcommand.test(args[0])) family += ` ${args[0]}`
  if (withSecondSubcommand.has(family) && args[1] && plainSubcommand.test(args[1])) family += ` ${args[1]}`
  return family
}
