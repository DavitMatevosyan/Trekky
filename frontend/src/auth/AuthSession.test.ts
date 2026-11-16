import { jsonResponse, requestAt } from '../test/renderApp.tsx'
import { createWebPlatform } from '../platform/webPlatform.ts'
import { AuthSession } from './AuthSession.ts'

const NOW = 1_000_000

function tokenResponse(access: string, refresh: string): Response {
  return jsonResponse({ access_token: access, refresh_token: refresh, expires_in: 900, token_type: 'Bearer' })
}

function sessionWith(fetchFn: typeof fetch, expiresAt = NOW + 900_000) {
  const session = new AuthSession({ fetchFn, now: () => NOW })
  session.setTokens({ accessToken: 'access-1', refreshToken: 'refresh-1', expiresAt })
  return session
}

describe('AuthSession', () => {
  it('starts anonymous and becomes authenticated when tokens are set', () => {
    const session = new AuthSession({ fetchFn: vi.fn() })
    const listener = vi.fn()
    session.subscribe(listener)

    expect(session.status).toBe('anonymous')
    session.setTokens({ accessToken: 'a', refreshToken: 'r', expiresAt: NOW })

    expect(session.status).toBe('authenticated')
    expect(listener).toHaveBeenCalledOnce()
  })

  it('reports loggedOut only when cleared by a logout', () => {
    const session = sessionWith(vi.fn())

    session.clear({ loggedOut: true })
    expect(session.status).toBe('loggedOut')

    session.setTokens({ accessToken: 'a', refreshToken: 'r', expiresAt: NOW })
    session.clear()
    expect(session.status).toBe('anonymous')
  })

  it('sends API requests with the access token as a bearer token', async () => {
    const fetchFn = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse({}))
    const session = sessionWith(fetchFn)

    await session.fetch(new Request('http://localhost/api/v1/me'))

    expect(requestAt(fetchFn, 0).headers.get('Authorization')).toBe('Bearer access-1')
  })

  it('sends requests without a token when anonymous', async () => {
    const fetchFn = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse({}))
    const session = new AuthSession({ fetchFn, now: () => NOW })

    await session.fetch(new Request('http://localhost/api/v1/auth/login', { method: 'POST', body: '{}' }))

    expect(requestAt(fetchFn, 0).headers.has('Authorization')).toBe(false)
  })

  it('refreshes the token and retries the request once after a 401', async () => {
    const fetchFn = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
      .mockResolvedValueOnce(tokenResponse('access-2', 'refresh-2'))
      .mockResolvedValueOnce(jsonResponse({ ok: true }))
    const session = sessionWith(fetchFn)

    const response = await session.fetch(new Request('http://localhost/api/v1/me'))

    expect(response.status).toBe(200)
    const refresh = requestAt(fetchFn, 1)
    expect(refresh.url).toContain('/connect/token')
    expect(await refresh.text()).toContain('grant_type=refresh_token&refresh_token=refresh-1')
    expect(requestAt(fetchFn, 2).headers.get('Authorization')).toBe('Bearer access-2')
  })

  it('refreshes before sending when the access token is about to expire', async () => {
    const fetchFn = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(tokenResponse('access-2', 'refresh-2'))
      .mockResolvedValueOnce(jsonResponse({}))
    const session = sessionWith(fetchFn, NOW + 10_000)

    await session.fetch(new Request('http://localhost/api/v1/me'))

    expect(fetchFn).toHaveBeenCalledTimes(2)
    expect(requestAt(fetchFn, 1).headers.get('Authorization')).toBe('Bearer access-2')
  })

  it('shares one refresh between concurrent requests, because refresh tokens are single-use', async () => {
    const fetchFn = vi.fn<typeof fetch>(async (input) => {
      const url = input instanceof Request ? input.url : String(input)
      return url.includes('/connect/token') ? tokenResponse('access-2', 'refresh-2') : jsonResponse({})
    })
    const session = sessionWith(fetchFn, NOW + 10_000)

    await Promise.all([
      session.fetch(new Request('http://localhost/api/v1/me')),
      session.fetch(new Request('http://localhost/api/v1/me')),
    ])

    const tokenCalls = fetchFn.mock.calls.filter(([input]) => String(input).includes('/connect/token'))
    expect(tokenCalls).toHaveLength(1)
  })

  it('ends the session when the refresh token is rejected', async () => {
    const fetchFn = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
      .mockResolvedValueOnce(jsonResponse({ error: 'invalid_grant' }, 400))
    const session = sessionWith(fetchFn)

    const response = await session.fetch(new Request('http://localhost/api/v1/me'))

    expect(response.status).toBe(401)
    expect(session.status).toBe('anonymous')
  })

  it('keeps the tokens when the refresh fails for a network reason, so the next request can try again', async () => {
    const fetchFn = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
      .mockRejectedValueOnce(new TypeError('Failed to fetch'))
    const session = sessionWith(fetchFn)

    const response = await session.fetch(new Request('http://localhost/api/v1/me'))

    expect(response.status).toBe(401)
    expect(session.status).toBe('authenticated')
  })

  it('stays logged out when a refresh finishes after logout', async () => {
    let finishRefresh: (response: Response) => void = () => {}
    const fetchFn = vi.fn<typeof fetch>((input) =>
      String(input).includes('/connect/token')
        ? new Promise<Response>((resolve) => (finishRefresh = resolve))
        : Promise.resolve(new Response(null, { status: 204 })),
    )
    const session = sessionWith(fetchFn, NOW + 10_000)

    const pending = session.fetch(new Request('http://localhost/api/v1/me'))
    await session.logOut(async () => {})
    finishRefresh(tokenResponse('access-2', 'refresh-2'))
    await pending

    expect(session.status).toBe('loggedOut')
  })

  it('reports loggedOut, not anonymous, when the session lapses during logout', async () => {
    const session = sessionWith(vi.fn<typeof fetch>())
    const statuses: string[] = []
    session.subscribe(() => statuses.push(session.status))

    await session.logOut(async () => session.clear())

    expect(statuses).not.toContain('anonymous')
    expect(session.status).toBe('loggedOut')
  })

  describe('restore (a refresh token kept by the platform, ADR-0019)', () => {
    function storing(initial: string | null) {
      const saved: (string | null)[] = []
      const web = createWebPlatform()
      let current = initial
      const platform = createWebPlatform({
        tokens: {
          load: () => current,
          save: (token) => {
            current = token
            saved.push(token)
          },
        },
        login: { ...web.login, silent: false },
      })
      return { platform, saved }
    }

    it('is restoring until the stored token is redeemed, then authenticated with the rotated one', async () => {
      const { platform, saved } = storing('stored')
      const fetchFn = vi.fn<typeof fetch>().mockResolvedValue(tokenResponse('access-2', 'refresh-2'))
      const session = new AuthSession({ fetchFn, now: () => NOW, platform })

      expect(session.status).toBe('restoring')
      await session.restore()

      expect(session.status).toBe('authenticated')
      expect(saved).toEqual(['refresh-2'])
      expect(new URLSearchParams(await requestAt(fetchFn, 0).text()).get('refresh_token')).toBe('stored')
    })

    it('redeems the single-use token once even when asked twice', async () => {
      const { platform } = storing('stored')
      const fetchFn = vi.fn<typeof fetch>().mockResolvedValue(tokenResponse('access-2', 'refresh-2'))
      const session = new AuthSession({ fetchFn, now: () => NOW, platform })

      await Promise.all([session.restore(), session.restore()])
      await session.restore()

      expect(fetchFn).toHaveBeenCalledOnce()
    })

    it('forgets a rejected token and becomes anonymous', async () => {
      const { platform, saved } = storing('revoked')
      const session = new AuthSession({ fetchFn: vi.fn<typeof fetch>().mockResolvedValue(jsonResponse({ error: 'invalid_grant' }, 400)), now: () => NOW, platform })

      await session.restore()

      expect(session.status).toBe('anonymous')
      expect(saved).toEqual([null])
    })

    it('keeps the token for next time when the server cannot be reached', async () => {
      const { platform, saved } = storing('stored')
      const session = new AuthSession({ fetchFn: vi.fn<typeof fetch>().mockRejectedValue(new TypeError('Failed to fetch')), now: () => NOW, platform })

      await session.restore()

      expect(session.status).toBe('anonymous')
      expect(saved).toEqual([])
    })

    it('does nothing without a stored token', async () => {
      const fetchFn = vi.fn<typeof fetch>()
      const session = new AuthSession({ fetchFn })

      await session.restore()

      expect(session.status).toBe('anonymous')
      expect(fetchFn).not.toHaveBeenCalled()
    })
  })
})

