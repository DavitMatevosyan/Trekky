import { AuthSession } from '../auth/AuthSession.ts'
import { beginAuthorization, completeAuthorization } from '../auth/oauthClient.ts'
import { jsonResponse } from '../test/renderApp.tsx'
import type { Platform } from './Platform.ts'
import { apiBaseUrlFrom, CALLBACK_PATH, createWebPlatform } from './webPlatform.ts'

describe('apiBaseUrlFrom', () => {
  it('uses the page origin when nothing is configured', () => {
    expect(apiBaseUrlFrom(undefined, 'https://trekky.example.ts.net')).toBe('https://trekky.example.ts.net')
    expect(apiBaseUrlFrom({}, 'https://trekky.example.ts.net')).toBe('https://trekky.example.ts.net')
  })

  it('uses a configured http(s) origin, without path or trailing slash', () => {
    expect(apiBaseUrlFrom({ apiBaseUrl: 'https://api.example.com/some/path/' }, 'http://tauri.localhost')).toBe('https://api.example.com')
    expect(apiBaseUrlFrom({ apiBaseUrl: 'http://localhost:5080' }, 'http://tauri.localhost')).toBe('http://localhost:5080')
  })

  it('ignores a configured value that is not an http(s) URL', () => {
    expect(apiBaseUrlFrom({ apiBaseUrl: 'not a url' }, 'https://page.example')).toBe('https://page.example')
    expect(apiBaseUrlFrom({ apiBaseUrl: 'javascript:alert(1)' }, 'https://page.example')).toBe('https://page.example')
  })
})

describe('createWebPlatform', () => {
  afterEach(() => {
    delete window.__TREKKY_CONFIG__
  })

  it('keeps the browser behaviour: same origin, callback on this site, state in sessionStorage, nothing stored, links left alone', () => {
    const web = createWebPlatform()

    expect(web.apiBaseUrl).toBe(window.location.origin)
    expect(web.login.redirectUri()).toBe(`${window.location.origin}${CALLBACK_PATH}`)
    expect(web.pendingAuthorization).toBe(window.sessionStorage)
    web.tokens.save('a-refresh-token')
    expect(web.tokens.load()).toBeNull()
    expect(web.links.open('https://example.com')).toBe(false)
  })

  it('reads the API origin from runtime config', () => {
    window.__TREKKY_CONFIG__ = { apiBaseUrl: 'https://trekky.example.ts.net' }

    expect(createWebPlatform().apiBaseUrl).toBe('https://trekky.example.ts.net')
  })
})

describe('the auth flow on another platform', () => {
  function testPlatform(): Platform & { opened: string[]; saved: (string | null)[] } {
    const store = new Map<string, string>()
    const opened: string[] = []
    const saved: (string | null)[] = []
    return {
      apiBaseUrl: 'https://api.example.com',
      clientId: 'trekky-desktop',
      login: { redirectUri: () => 'trekky://auth/callback', open: (url) => opened.push(url) },
      pendingAuthorization: {
        getItem: (key) => store.get(key) ?? null,
        setItem: (key, value) => store.set(key, value),
        removeItem: (key) => store.delete(key),
      },
      tokens: { load: () => saved.at(-1) ?? null, save: (token) => saved.push(token) },
      links: { open: () => true },
      opened,
      saved,
    }
  }

  it('sends the user to the platform\'s API and redirect URI, and redeems the code there', async () => {
    const platform = testPlatform()
    await beginAuthorization({ platform })
    const authorize = new URL(platform.opened[0] ?? '')
    const fetchFn = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse({ access_token: 'a', refresh_token: 'r', expires_in: 900 }))

    await completeAuthorization(new URLSearchParams({ code: 'c', state: authorize.searchParams.get('state') ?? '' }), { platform, fetchFn })

    expect(authorize.origin + authorize.pathname).toBe('https://api.example.com/connect/authorize')
    expect(authorize.searchParams.get('redirect_uri')).toBe('trekky://auth/callback')
    expect(authorize.searchParams.get('client_id')).toBe('trekky-desktop')
    const [input, init] = fetchFn.mock.calls[0] ?? []
    expect(input).toBe('https://api.example.com/connect/token')
    expect(new URLSearchParams(String(init?.body)).get('redirect_uri')).toBe('trekky://auth/callback')
    expect(new URLSearchParams(String(init?.body)).get('client_id')).toBe('trekky-desktop')
  })

  it('stores the refresh token when the session gets tokens, and forgets it when they are dropped', () => {
    const platform = testPlatform()
    const session = new AuthSession({ platform })

    session.setTokens({ accessToken: 'a', refreshToken: 'r', expiresAt: Date.now() + 60_000 })
    session.clear({ loggedOut: true })

    expect(platform.saved).toEqual(['r', null])
  })
})
