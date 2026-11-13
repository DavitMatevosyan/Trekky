/**
 * Moves Trekky's own task list from Notion into a running Trekky (TRK-21): one Epic per Notion section, one Task (or
 * Bug) per TRK, moved along the workflow to its Notion status. Safe to run again: items are found by title, never
 * created twice (matched by their Notion key, so a rename in Trekky is fine), and never moved back.
 *
 *   TREKKY_EMAIL=… TREKKY_PASSWORD=… node tools/notion-import/import.ts [--dry-run]
 *
 * TREKKY_URL is the API (default http://localhost:5080). It signs in like the web app does (session cookie, then
 * authorization code + PKCE), so TREKKY_REDIRECT_URI must be one the API allows (default the local web app's).
 */
import { createHash, randomBytes } from 'node:crypto'
import { readFileSync } from 'node:fs'
import { existingMatchKey, itemDescription, itemTitle, matchKey, stepsTo, validate, type Snapshot, type Status } from './plan.ts'

const baseUrl = (process.env.TREKKY_URL ?? 'http://localhost:5080').replace(/\/$/, '')
const redirectUri = process.env.TREKKY_REDIRECT_URI ?? 'http://localhost:5173/auth/callback'
const clientId = 'trekky-web'
const dryRun = process.argv.includes('--dry-run')

interface Item {
  id: string
  title: string
  type: string
  status: string
}

class ApiError extends Error {}

async function signIn(email: string, password: string): Promise<string> {
  const login = await fetch(`${baseUrl}/api/v1/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  })
  if (!login.ok) throw new ApiError(`Login failed (${login.status}). Check TREKKY_EMAIL and TREKKY_PASSWORD.`)
  const cookie = login.headers.getSetCookie().map((header) => header.split(';')[0]).join('; ')

  const verifier = randomBytes(32).toString('base64url')
  const challenge = createHash('sha256').update(verifier).digest('base64url')
  const authorize = new URL(`${baseUrl}/connect/authorize`)
  for (const [key, value] of Object.entries({
    client_id: clientId,
    response_type: 'code',
    redirect_uri: redirectUri,
    scope: 'openid email profile offline_access',
    state: randomBytes(8).toString('hex'),
    code_challenge: challenge,
    code_challenge_method: 'S256',
  })) {
    authorize.searchParams.set(key, value)
  }
  const redirect = await fetch(authorize, { headers: { Cookie: cookie }, redirect: 'manual' })
  const code = new URL(redirect.headers.get('location') ?? '', baseUrl).searchParams.get('code')
  if (!code) throw new ApiError(`Authorization gave no code (${redirect.status}). Is ${redirectUri} an allowed redirect URI?`)

  const token = await fetch(`${baseUrl}/connect/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({ grant_type: 'authorization_code', client_id: clientId, code, redirect_uri: redirectUri, code_verifier: verifier }),
  })
  if (!token.ok) throw new ApiError(`Token request failed (${token.status}).`)
  return ((await token.json()) as { access_token: string }).access_token
}

function client(accessToken: string) {
  return async function call<T>(method: string, path: string, body?: unknown, headers: Record<string, string> = {}) {
    const response = await fetch(`${baseUrl}${path}`, {
      method,
      headers: { Authorization: `Bearer ${accessToken}`, ...(body === undefined ? {} : { 'Content-Type': 'application/json' }), ...headers },
      body: body === undefined ? undefined : JSON.stringify(body),
    })
    const text = await response.text()
    let parsed: unknown = null
    try {
      parsed = text ? JSON.parse(text) : null
    } catch {
      // A proxy in front of the API can answer with HTML (a 502 page); say where, not just "Unexpected token".
      throw new ApiError(`${method} ${path} answered ${response.status} with something that is not JSON.`)
    }
    return { status: response.status, etag: response.headers.get('etag') ?? '', body: parsed as T }
  }
}

type Call = ReturnType<typeof client>

async function ensureProject(call: Call, snapshot: Snapshot): Promise<string> {
  const found = await call<{ id: string }>('GET', `/api/v1/projects/by-key/${snapshot.project.key}`)
  if (found.status === 200) return found.body.id
  if (dryRun) return ''
  const created = await call<{ id: string }>('POST', '/api/v1/projects', snapshot.project)
  if (created.status !== 201) throw new ApiError(`Creating project ${snapshot.project.key} failed (${created.status}).`)
  console.log(`created project ${snapshot.project.key}`)
  return created.body.id
}

async function existingItems(call: Call, projectId: string): Promise<Map<string, Item>> {
  const items = new Map<string, Item>()
  if (!projectId) return items
  let cursor: string | null = null
  do {
    const query: string = cursor ? `&cursor=${encodeURIComponent(cursor)}` : ''
    const page = await call<{ items: Item[]; nextCursor: string | null }>('GET', `/api/v1/projects/${projectId}/work-items?limit=100${query}`)
    if (page.status !== 200) throw new ApiError(`Listing items failed (${page.status}).`)
    for (const item of page.body.items) items.set(existingMatchKey(item.title), item)
    cursor = page.body.nextCursor
  } while (cursor)
  return items
}

async function create(call: Call, projectId: string, body: Record<string, unknown>): Promise<Item & { etag: string }> {
  const created = await call<Item>('POST', `/api/v1/projects/${projectId}/work-items`, body)
  if (created.status !== 201) throw new ApiError(`Creating "${String(body.title)}" failed (${created.status}): ${JSON.stringify(created.body)}`)
  return { ...created.body, etag: created.etag }
}

/** Moves the item forward one allowed step at a time; each step needs the ETag of the step before. */
async function advance(call: Call, item: Item, etag: string, to: Status): Promise<number> {
  const steps = stepsTo(item.status, to)
  let current = etag
  if (steps.length > 0 && !current) current = (await call('GET', `/api/v1/work-items/${item.id}`)).etag
  for (const step of steps) {
    const moved = await call('POST', `/api/v1/work-items/${item.id}/status`, { to: step }, { 'If-Match': current })
    if (moved.status !== 200) throw new ApiError(`Moving "${item.title}" to ${step} failed (${moved.status}).`)
    current = moved.etag
  }
  return steps.length
}

async function main() {
  const snapshot = JSON.parse(readFileSync(new URL('./tasks.json', import.meta.url), 'utf8')) as Snapshot
  const problems = validate(snapshot)
  if (problems.length > 0) throw new ApiError(`tasks.json is not valid:\n- ${problems.join('\n- ')}`)

  const email = process.env.TREKKY_EMAIL
  const password = process.env.TREKKY_PASSWORD
  if (!email || !password) throw new ApiError('Set TREKKY_EMAIL and TREKKY_PASSWORD (the Trekky account to import into).')

  const call = client(await signIn(email, password))
  const projectId = await ensureProject(call, snapshot)
  const existing = await existingItems(call, projectId)
  const counts = { created: 0, kept: 0, moves: 0 }

  for (const epic of snapshot.epics) {
    let parent = existing.get(existingMatchKey(epic.title))
    if (parent) counts.kept++
    else if (!dryRun) {
      parent = await create(call, projectId, { type: 'epic', title: epic.title, descriptionMd: itemDescription(epic, snapshot.source) })
      counts.created++
    } else {
      console.log(`would create epic "${epic.title}"`)
      counts.created++
    }

    for (const entry of epic.items) {
      const title = itemTitle(entry)
      const found = existing.get(matchKey(entry))
      if (dryRun) {
        console.log(found ? `would move "${title}" ${stepsTo(found.status, entry.status).join(' → ') || '(no change)'}` : `would create "${title}" in ${entry.status}`)
        counts[found ? 'kept' : 'created']++
        continue
      }

      const item: Item & { etag?: string } =
        found ??
        (await create(call, projectId, {
          type: entry.type ?? 'task',
          title,
          descriptionMd: itemDescription(entry, snapshot.source),
          parentId: parent?.id,
        }))
      counts[found ? 'kept' : 'created']++
      counts.moves += await advance(call, item, item.etag ?? '', entry.status)
    }
  }

  const verb = dryRun ? 'Would create' : 'Created'
  console.log(`${verb} ${counts.created} items, kept ${counts.kept} that exist, ${counts.moves} status moves.`)
}

main().catch((error: unknown) => {
  console.error(error instanceof ApiError ? error.message : error)
  process.exitCode = 1
})
