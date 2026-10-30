import { QueryClient } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import { createMemoryRouter } from 'react-router'
import { App } from '../App.tsx'
import { AuthSession } from '../auth/AuthSession.ts'
import type { Tokens } from '../auth/Tokens.ts'
import { routes } from '../routes.tsx'

export const validTokens: Tokens = { accessToken: 'access-1', refreshToken: 'refresh-1', expiresAt: Date.now() + 15 * 60_000 }

export function jsonResponse(body: unknown, status = 200, contentType = 'application/json'): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': contentType } })
}

export function problemResponse(status: number, problem: Record<string, unknown>): Response {
  return jsonResponse({ status, ...problem }, status, 'application/problem+json')
}

export const me = {
  user: { id: '0190a000-0000-7000-8000-000000000001', displayName: 'ada@example.com', email: 'ada@example.com' },
  organization: { id: '0190a000-0000-7000-8000-000000000002', name: 'Acme' },
  role: 'owner',
}

export const emptyProjectPage = { items: [], nextCursor: null }

type Handler = (request: Request) => Response | Promise<Response>

/**
 * A fake API for a logged-in page: `"METHOD /path"` → response. GET /api/v1/me and GET /api/v1/projects answer by
 * default; anything else is a 404 so a missing handler shows up in the test.
 */
export function fakeApi(handlers: Record<string, Handler> = {}) {
  const all: Record<string, Handler> = {
    'GET /api/v1/me': () => jsonResponse(me),
    'GET /api/v1/projects': () => jsonResponse(emptyProjectPage),
    ...handlers,
  }
  return vi.fn<typeof fetch>(async (input, init) => {
    const request = input instanceof Request ? input : new Request(new URL(String(input), 'http://localhost'), init)
    const handler = all[`${request.method} ${new URL(request.url).pathname}`]
    return handler ? handler(request) : new Response(null, { status: 404 })
  })
}

/** The requests a fetch mock received for `"METHOD /path"`, in order. */
export function requestsTo(fetchFn: typeof fetch, route: string): Request[] {
  return vi
    .mocked(fetchFn)
    .mock.calls.map(([input, init]) => (input instanceof Request ? input : new Request(new URL(String(input), 'http://localhost'), init)))
    .filter((request) => `${request.method} ${new URL(request.url).pathname}` === route)
}

/** Renders the real routes in a memory router, with a session whose network calls go to `fetchFn`. */
export function renderApp({
  path = '/',
  fetchFn = vi.fn<typeof fetch>(),
  tokens = null,
}: { path?: string; fetchFn?: typeof fetch; tokens?: Tokens | null } = {}) {
  const session = new AuthSession({ fetchFn })
  if (tokens) session.setTokens(tokens)
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  const router = createMemoryRouter(routes, { initialEntries: [path] })
  const view = render(<App session={session} queryClient={queryClient} router={router} />)
  return { ...view, session, router, fetchFn }
}

/** The request a fetch mock received in call `index`. */
export function requestAt(fetchFn: typeof fetch, index: number): Request {
  const call = vi.mocked(fetchFn).mock.calls[index]
  if (!call) throw new Error(`fetch was not called ${index + 1} times`)
  const [input, init] = call
  return input instanceof Request ? input : new Request(new URL(String(input), 'http://localhost'), init)
}
