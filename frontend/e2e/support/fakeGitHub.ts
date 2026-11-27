import { createHash } from 'node:crypto'
import { createServer, type IncomingMessage, type Server, type ServerResponse } from 'node:http'

/**
 * A stand-in for github.com and api.github.com in end-to-end tests (TREK-75), on the port the API is configured with
 * (playwright.config.ts). It answers only what Trekky asks: the app install page and code exchange, the installation
 * and repository checks of TREK-74, and the commits, trees, blobs and contents the docs viewer reads. Every installation
 * reaches one repository, `acme/handbook`, with the files below on branch `dev`.
 */

export const FAKE_GITHUB_PORT = Number(process.env.E2E_FAKE_GITHUB_PORT ?? 5181)
export const FAKE_GITHUB_CLIENT_ID = 'Iv23.e2e-fake'
export const FAKE_GITHUB_CLIENT_SECRET = 'e2e-fake-secret'
export const FAKE_GITHUB_SLUG = 'trekky-e2e'
export const REPOSITORY = 'acme/handbook'

/** A 1×1 PNG. */
const PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==',
  'base64',
)

export const FILES: Record<string, Buffer> = {
  'README.md': Buffer.from('# Repository readme\n\nOutside the docs folder.\n'),
  'docs/README.md': Buffer.from(
    [
      '# Handbook',
      '',
      'Start with [setting up](guide/setup.md#install).',
      '',
      '| Service | Port |',
      '| --- | --- |',
      '| API | 5080 |',
      '',
      '- [x] Read the handbook',
      '',
      '```mermaid',
      'graph LR; Reader-->Docs',
      '```',
      '',
      '![Flow chart](images/flow.png)',
      '',
    ].join('\n'),
  ),
  'docs/guide/setup.md': Buffer.from('# Setting up\n\n## Install\n\nRun Docker Compose to start Postgres and Mailpit.\n'),
  'docs/images/flow.png': PNG,
}

const COMMIT_SHA = 'c0ffee0000000000000000000000000000000001'
const sha1 = (text: string | Buffer) => createHash('sha1').update(text).digest('hex')
const blobSha = (bytes: Buffer) => sha1(Buffer.concat([Buffer.from(`blob ${bytes.length}\0`), bytes]))

/** The tree SHA of a folder ('' for the root), and the folder each SHA stands for. */
function treeSha(folder: string): string {
  return sha1(`tree ${folder}\n${Object.keys(FILES).filter((p) => p.startsWith(folder ? `${folder}/` : '')).join('\n')}`)
}
const FOLDERS = new Set(['', ...Object.keys(FILES).flatMap((path) => path.split('/').slice(0, -1).map((_, i, s) => s.slice(0, i + 1).join('/')))])
const TREES = new Map([...FOLDERS].map((folder) => [treeSha(folder), folder]))
const BLOBS = new Map(Object.values(FILES).map((bytes) => [blobSha(bytes), bytes]))

const repository = {
  id: 4242,
  full_name: REPOSITORY,
  default_branch: 'dev',
  private: true,
  html_url: `https://github.com/${REPOSITORY}`,
}

const commit = {
  sha: COMMIT_SHA,
  commit: { message: 'Write the handbook', author: { name: 'Ada Lovelace', date: '2026-11-20T10:00:00Z' }, tree: { sha: treeSha('') } },
}

function json(response: ServerResponse, body: unknown, status = 200) {
  response.writeHead(status, { 'Content-Type': 'application/json' })
  response.end(JSON.stringify(body))
}

function notFound(response: ServerResponse) {
  json(response, { message: 'Not Found' }, 404)
}

function bearer(request: IncomingMessage): string {
  return /^Bearer (.+)$/.exec(request.headers.authorization ?? '')?.[1] ?? ''
}

/** The installation id behind a token this fake issued (`inst-<id>`, `user-<id>`). */
function idFrom(token: string, prefix: string): number | null {
  return token.startsWith(prefix) ? Number(token.slice(prefix.length)) : null
}

function contents(request: IncomingMessage, response: ServerResponse, path: string, ref: string | null) {
  if (ref !== 'dev' && ref !== COMMIT_SHA) return notFound(response)
  const file = FILES[path]
  if (file) {
    if ((request.headers.accept ?? '').includes('raw')) {
      response.writeHead(200, { 'Content-Type': 'application/vnd.github.raw' })
      return response.end(file)
    }
    return json(response, { type: 'file', name: path.split('/').at(-1), path, sha: blobSha(file) })
  }
  if (!FOLDERS.has(path)) return notFound(response)
  const prefix = path ? `${path}/` : ''
  const children = [...new Set(Object.keys(FILES).filter((p) => p.startsWith(prefix)).map((p) => p.slice(prefix.length).split('/')[0] ?? ''))]
  json(
    response,
    children.map((name) => {
      const childPath = prefix + name
      const child = FILES[childPath]
      return child
        ? { type: 'file', name, path: childPath, sha: blobSha(child) }
        : { type: 'dir', name, path: childPath, sha: treeSha(childPath) }
    }),
  )
}

async function handle(request: IncomingMessage, response: ServerResponse, webOrigin: string) {
  const url = new URL(request.url ?? '/', 'http://fake')
  const path = decodeURIComponent(url.pathname)
  const route = `${request.method} ${path}`
  let match: RegExpExecArray | null

  // github.com: the install page sends the browser straight back, as after installing on one repository.
  if (route === `GET /apps/${FAKE_GITHUB_SLUG}/installations/new`) {
    const installationId = 100_000 + Math.floor(Math.random() * 800_000_000)
    const back = new URL('/settings/github/callback', webOrigin)
    back.searchParams.set('code', `code-${installationId}`)
    back.searchParams.set('installation_id', String(installationId))
    back.searchParams.set('setup_action', 'install')
    const state = url.searchParams.get('state')
    if (state) back.searchParams.set('state', state)
    response.writeHead(302, { Location: back.toString() })
    return response.end()
  }
  if (route === 'POST /login/oauth/access_token') {
    let body = ''
    for await (const chunk of request) body += String(chunk)
    const form = new URLSearchParams(body)
    const id = idFrom(form.get('code') ?? '', 'code-')
    if (form.get('client_id') !== FAKE_GITHUB_CLIENT_ID || form.get('client_secret') !== FAKE_GITHUB_CLIENT_SECRET || !id) {
      return json(response, { error: 'bad_verification_code' })
    }
    return json(response, { access_token: `user-${id}`, token_type: 'bearer' })
  }
  if (request.method === 'DELETE' && path === `/applications/${FAKE_GITHUB_CLIENT_ID}/token`) {
    response.writeHead(204)
    return response.end()
  }

  // As the user who installed the app.
  const userId = idFrom(bearer(request), 'user-')
  if (route === 'GET /user/installations' && userId) {
    return json(response, { total_count: 1, installations: [{ id: userId, account: { login: 'acme', type: 'Organization' } }] })
  }
  if ((match = /^GET \/user\/installations\/(\d+)\/repositories$/.exec(route)) && userId === Number(match[1])) {
    return json(response, { total_count: 1, repositories: [{ ...repository, permissions: { admin: true } }] })
  }

  // As the app (any JWT: the API's signing is tested against the .NET fake).
  if ((match = /^GET \/app\/installations\/(\d+)$/.exec(route))) {
    return json(response, { id: Number(match[1]), account: { login: 'acme', type: 'Organization' } })
  }
  if ((match = /^POST \/app\/installations\/(\d+)\/access_tokens$/.exec(route))) {
    return json(response, { token: `inst-${match[1]}`, expires_at: new Date(Date.now() + 3600_000).toISOString() }, 201)
  }

  // As an installation.
  if (!idFrom(bearer(request), 'inst-')) return json(response, { message: 'Bad credentials' }, 401)
  if (route === 'GET /installation/repositories') return json(response, { total_count: 1, repositories: [repository] })

  const prefix = `/repos/${REPOSITORY}`
  if (!path.startsWith(prefix) || request.method !== 'GET') return notFound(response)
  const rest = path.slice(prefix.length)
  if (rest === '/branches/dev') return json(response, { name: 'dev', commit })
  if (rest === '/commits') return json(response, url.searchParams.get('sha') === 'dev' ? [commit] : [])
  if (rest === '/contents') return contents(request, response, '', url.searchParams.get('ref'))
  if (rest.startsWith('/contents/')) return contents(request, response, rest.slice('/contents/'.length).replace(/\/$/, ''), url.searchParams.get('ref'))
  if ((match = /^\/git\/trees\/([0-9a-f]+)$/.exec(rest))) {
    const folder = TREES.get(match[1] ?? '')
    if (folder === undefined) return notFound(response)
    const prefixed = folder ? `${folder}/` : ''
    const entries = Object.entries(FILES)
      .filter(([p]) => p.startsWith(prefixed))
      .map(([p, bytes]) => ({ path: p.slice(prefixed.length), type: 'blob', sha: blobSha(bytes), size: bytes.length }))
    return json(response, { sha: match[1], tree: entries, truncated: false })
  }
  if ((match = /^\/git\/blobs\/([0-9a-f]+)$/.exec(rest))) {
    const bytes = BLOBS.get(match[1] ?? '')
    if (!bytes) return notFound(response)
    response.writeHead(200, { 'Content-Type': 'application/vnd.github.raw' })
    return response.end(bytes)
  }
  notFound(response)
}

/** Starts the fake on FAKE_GITHUB_PORT; the install page redirects back to `webOrigin`. */
export async function startFakeGitHub(webOrigin: string): Promise<Server> {
  const server = createServer((request, response) => {
    handle(request, response, webOrigin).catch(() => {
      response.writeHead(500)
      response.end()
    })
  })
  await new Promise<void>((resolve, reject) => {
    server.once('error', reject)
    server.listen(FAKE_GITHUB_PORT, '127.0.0.1', resolve)
  })
  return server
}
