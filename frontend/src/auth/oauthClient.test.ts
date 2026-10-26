import { jsonResponse } from '../test/renderApp.tsx'
import { AuthorizationError, beginAuthorization, completeAuthorization, safeReturnTo } from './oauthClient.ts'

async function startFlow(options: Parameters<typeof beginAuthorization>[0] = {}) {
  const navigate = vi.fn<(url: string) => void>()
  await beginAuthorization({ storage: sessionStorage, navigate, ...options })
  const url = new URL(navigate.mock.calls[0]?.[0] ?? '', window.location.origin)
  return { url, state: url.searchParams.get('state') ?? '' }
}

describe('oauthClient', () => {
  beforeEach(() => sessionStorage.clear())

  it('redirects to /connect/authorize with code + PKCE for the trekky-web client', async () => {
    const { url } = await startFlow({ prompt: 'none' })

    expect(url.pathname).toBe('/connect/authorize')
    expect(Object.fromEntries(url.searchParams)).toMatchObject({
      client_id: 'trekky-web',
      response_type: 'code',
      redirect_uri: `${window.location.origin}/auth/callback`,
      scope: 'openid email profile offline_access',
      code_challenge_method: 'S256',
      prompt: 'none',
    })
    expect(url.searchParams.get('code_challenge')).toMatch(/^[A-Za-z0-9_-]{43}$/)
  })

  it('sends a code challenge that is the SHA-256 of the verifier it later redeems', async () => {
    const { url, state } = await startFlow()
    const fetchFn = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse({ access_token: 'a', refresh_token: 'r', expires_in: 900 }))

    await completeAuthorization(new URLSearchParams({ code: 'the-code', state }), { fetchFn })

    const body = new URLSearchParams(String(fetchFn.mock.calls[0]?.[1]?.body))
    const verifier = body.get('code_verifier') ?? ''
    const digest = new Uint8Array(await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier)))
    const expected = btoa(String.fromCharCode(...digest)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
    expect(url.searchParams.get('code_challenge')).toBe(expected)
  })

  it('exchanges the code for tokens and returns to the saved path', async () => {
    const { state } = await startFlow({ returnTo: '/projects?x=1' })
    const fetchFn = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse({ access_token: 'a', refresh_token: 'r', expires_in: 900 }))

    const result = await completeAuthorization(new URLSearchParams({ code: 'the-code', state }), { fetchFn, now: () => 1000 })

    expect(result).toEqual({ tokens: { accessToken: 'a', refreshToken: 'r', expiresAt: 901_000 }, returnTo: '/projects?x=1' })
    const [input, init] = fetchFn.mock.calls[0] ?? []
    expect(input).toBe('/connect/token')
    expect(Object.fromEntries(new URLSearchParams(String(init?.body)))).toMatchObject({
      client_id: 'trekky-web',
      grant_type: 'authorization_code',
      code: 'the-code',
    })
  })

  it('rejects a response whose state does not match, without calling the token endpoint', async () => {
    await startFlow()
    const fetchFn = vi.fn<typeof fetch>()

    await expect(completeAuthorization(new URLSearchParams({ code: 'c', state: 'forged' }), { fetchFn })).rejects.toMatchObject({
      loginRequired: true,
    })
    expect(fetchFn).not.toHaveBeenCalled()
  })

  it('reports login_required with the path the user was headed to', async () => {
    const { state } = await startFlow({ prompt: 'none', returnTo: '/somewhere' })

    const error = await completeAuthorization(new URLSearchParams({ error: 'login_required', state })).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(AuthorizationError)
    expect(error).toMatchObject({ loginRequired: true, returnTo: '/somewhere' })
  })

  it('only keeps same-origin return paths', () => {
    expect(safeReturnTo('/projects')).toBe('/projects')
    expect(safeReturnTo('//evil.example')).toBe('/')
    expect(safeReturnTo('/\\evil.example')).toBe('/')
    expect(safeReturnTo('https://evil.example')).toBe('/')
    expect(safeReturnTo(null)).toBe('/')
  })

  it('ignores an error response whose state does not match', async () => {
    await startFlow({ returnTo: '/somewhere' })

    const error = await completeAuthorization(new URLSearchParams({ error: 'access_denied', state: 'forged' })).catch((e: unknown) => e)

    expect(error).toMatchObject({ loginRequired: true, message: 'The sign-in response does not match the request.' })
  })

  it('treats a corrupted pending request as no request', async () => {
    sessionStorage.setItem('trekky.auth.pending', '{not json')

    await expect(completeAuthorization(new URLSearchParams({ code: 'c', state: 's' }))).rejects.toMatchObject({ loginRequired: true })
  })
})
