// TREK-91: token usage of Claude Code sessions in this repo, from the local transcripts. See README.md.
import { execFileSync } from 'node:child_process'
import { existsSync, readdirSync, readFileSync } from 'node:fs'
import { homedir } from 'node:os'
import { basename, dirname, join } from 'node:path'
import { parseArgs } from 'node:util'
import { buildReport, formatReport, trkOf, type Prices, type Session } from './report.ts'
import { parseTranscript, type ApiRequest, type ToolOutput, type Window } from './transcript.ts'

const { values } = parseArgs({
  options: {
    since: { type: 'string' },
    until: { type: 'string' },
    trk: { type: 'string', multiple: true },
    dir: { type: 'string', multiple: true },
    top: { type: 'string', default: '15' },
    json: { type: 'boolean', default: false },
  },
})

const top = Number(values.top)
const day = /^\d{4}-\d{2}-\d{2}$/
if (!Number.isInteger(top) || top < 1) fail('--top takes a whole number of rows, 1 or more')
for (const [name, value] of [['--since', values.since], ['--until', values.until]] as const) {
  if (value !== undefined && !day.test(value)) fail(`${name} takes a day as YYYY-MM-DD`)
}

function fail(message: string): never {
  console.error(message)
  process.exit(2)
}

const prices = (JSON.parse(readFileSync(new URL('./prices.json', import.meta.url), 'utf8')) as { models: Prices }).models
const projectsRoot = join(process.env.CLAUDE_CONFIG_DIR ?? join(homedir(), '.claude'), 'projects')

// Claude Code keeps each working folder's sessions in a folder named after its path, every non-alphanumeric
// character replaced by '-'. The default takes the main checkout and every folder whose path starts with it
// (trekky-backend, trekky-wt-TREK-n worktrees, but also a sibling folder such as trekky-host: use --dir to narrow).
function defaultDirs(): string[] {
  const gitDir = execFileSync('git', ['rev-parse', '--path-format=absolute', '--git-common-dir'], { encoding: 'utf8' }).trim()
  const prefix = dirname(gitDir).replace(/[^A-Za-z0-9]/g, '-').toLowerCase()
  if (!existsSync(projectsRoot)) return []
  return readdirSync(projectsRoot)
    .filter((name) => name.toLowerCase() === prefix || name.toLowerCase().startsWith(`${prefix}-`))
    .map((name) => join(projectsRoot, name))
}

const trks = values.trk?.map((key) => key.toUpperCase())
const window: Window = (timestamp, branch) => {
  const date = timestamp.slice(0, 10)
  const inPeriod = (!values.since || date >= values.since) && (!values.until || date <= values.until)
  return inPeriod && (!trks || trks.includes(trkOf(branch)))
}

const seen = new Set<string>()
const requests: ApiRequest[] = []
const outputs: ToolOutput[] = []
const sessions: Session[] = []

// Oldest first across all folders, so a resumed session's repeated responses count in the session that made them.
const files = (values.dir ?? defaultDirs())
  .flatMap((dir) =>
    readdirSync(dir)
      .filter((name) => name.endsWith('.jsonl'))
      .map((name) => ({ dir, name, text: readFileSync(join(dir, name), 'utf8') })),
  )
  .sort((a, b) => firstTimestamp(a.text).localeCompare(firstTimestamp(b.text)))

for (const file of files) {
  const dir = file.dir
  const sessionId = basename(file.name, '.jsonl')
  const main = parseTranscript(file.text, { sessionId, run: sessionId, agent: 'main' }, { window, seen })
  let counted = main.requests.length
  requests.push(...main.requests)
  outputs.push(...main.toolOutputs)

  const subagents = join(dir, sessionId, 'subagents')
  if (existsSync(subagents)) {
    for (const agentFile of readdirSync(subagents).filter((name) => name.endsWith('.jsonl'))) {
      const run = basename(agentFile, '.jsonl')
      const metaFile = join(subagents, `${run}.meta.json`)
      const meta = existsSync(metaFile) ? (JSON.parse(readFileSync(metaFile, 'utf8')) as { agentType?: string }) : {}
      const text = readFileSync(join(subagents, agentFile), 'utf8')
      const agent = parseTranscript(text, { sessionId, run, agent: meta.agentType ?? 'subagent' }, { window, seen })
      counted += agent.requests.length
      requests.push(...agent.requests)
      outputs.push(...agent.toolOutputs)
    }
  }

  if (counted === 0) continue
  const start = main.start
  sessions.push({
    sessionId,
    startContext: start && window(start.timestamp, start.branch) ? start.context : 0,
    compactions: main.compactions,
  })
}

function firstTimestamp(text: string): string {
  return /"timestamp":"([^"]+)"/.exec(text)?.[1] ?? ''
}

const report = buildReport(requests, outputs, sessions, prices)
console.log(values.json ? JSON.stringify(report, null, 2) : formatReport(report, top))
