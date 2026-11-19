import { act, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createDesktopPlatform } from './platform/desktop/desktopPlatform.ts'
import { fakeDesktopShell, type FakeDesktopShell } from './test/fakeDesktopShell.ts'
import { fakeApi, jsonResponse, renderApp, requestsTo } from './test/renderApp.tsx'

/** The desktop app's sign-in end to end in the web app (ADR-0019), with the shell and the server faked. */
const SERVER = 'https://trekky.example.com'

function desktop(shell: FakeDesktopShell, storedRefreshToken: string | null = null) {
  return createDesktopPlatform({ url: SERVER, change: vi.fn() }, shell, storedRefreshToken)
}

function tokenResponse(access: string, refresh: string): Response {
  return jsonResponse({ access_token: access, refresh_token: refresh, expires_in: 900, token_type: 'Bearer' })
}

async function formOf(request: Request): Promise<URLSearchParams> {
  return new URLSearchParams(await request.clone().text())
}

describe('desktop sign-in', () => {
  beforeEach(() => window.sessionStorage.clear())

  it('logs in through the system browser and finishes when the trekky:// link comes back', async () => {
    const shell = fakeDesktopShell()
    const fetchFn = fakeApi({ 'POST /connect/token': () => tokenResponse('access-d', 'refresh-d') })
    renderApp({ path: '/', platform: desktop(shell), fetchFn })

    await userEvent.click(await screen.findByRole('button', { name: 'Log in with your browser' }))

    await waitFor(() => expect(shell.opened).toHaveLength(1))
    const authorize = new URL(shell.opened[0] ?? '')
    expect(authorize.origin + authorize.pathname).toBe(`${SERVER}/connect/authorize`)
    expect(authorize.searchParams.get('client_id')).toBe('trekky-desktop')
    expect(authorize.searchParams.get('redirect_uri')).toBe('trekky://auth/callback')
    expect(authorize.searchParams.get('code_challenge_method')).toBe('S256')
    expect(authorize.searchParams.has('prompt')).toBe(false)
    expect(screen.getByRole('status')).toHaveTextContent('Finish in your browser. Trekky continues here automatically.')

    act(() => shell.sendCallback(`trekky://auth/callback?code=the-code&state=${authorize.searchParams.get('state') ?? ''}`))

    expect(await screen.findByText('You are the Owner of Acme.')).toBeInTheDocument()
    const [token] = requestsTo(fetchFn, 'POST /connect/token')
    expect(token?.url).toBe(`${SERVER}/connect/token`)
    const form = await formOf(token as Request)
    expect(form.get('client_id')).toBe('trekky-desktop')
    expect(form.get('code')).toBe('the-code')
    expect(form.get('redirect_uri')).toBe('trekky://auth/callback')
    expect(shell.credentials.get(SERVER)).toBe('refresh-d')
  })

  it('signs up on the server’s sign-up page, which continues to the authorization request', async () => {
    const shell = fakeDesktopShell()
    renderApp({ path: '/sign-up', platform: desktop(shell) })

    await userEvent.click(screen.getByRole('button', { name: 'Sign up in your browser' }))

    await waitFor(() => expect(shell.opened).toHaveLength(1))
    const signUp = new URL(shell.opened[0] ?? '')
    expect(signUp.origin + signUp.pathname).toBe(`${SERVER}/sign-up`)
    const returnTo = signUp.searchParams.get('returnTo') ?? ''
    expect(returnTo.startsWith('/connect/authorize?')).toBe(true)
    expect(new URLSearchParams(returnTo.split('?')[1]).get('client_id')).toBe('trekky-desktop')
  })

  it('ignores a callback it did not ask for, and still finishes with the real one', async () => {
    const shell = fakeDesktopShell()
    const fetchFn = fakeApi({ 'POST /connect/token': () => tokenResponse('access-d', 'refresh-d') })
    const { router } = renderApp({ path: '/login', platform: desktop(shell), fetchFn })
    await userEvent.click(screen.getByRole('button', { name: 'Log in with your browser' }))
    await waitFor(() => expect(shell.opened).toHaveLength(1))
    const state = new URL(shell.opened[0] ?? '').searchParams.get('state') ?? ''

    act(() => shell.sendCallback('trekky://auth/callback?code=stolen&state=not-ours'))

    expect(router.state.location.pathname).toBe('/login')
    expect(screen.getByRole('status')).toHaveTextContent('Finish in your browser.')
    expect(requestsTo(fetchFn, 'POST /connect/token')).toHaveLength(0)

    act(() => shell.sendCallback(`trekky://auth/callback?code=the-code&state=${state}`))

    expect(await screen.findByText('You are the Owner of Acme.')).toBeInTheDocument()
    expect((await formOf(requestsTo(fetchFn, 'POST /connect/token')[0] as Request)).get('code')).toBe('the-code')
  })

  it('“Open the browser again” opens the same request, so its answer still matches', async () => {
    const shell = fakeDesktopShell()
    const fetchFn = fakeApi({ 'POST /connect/token': () => tokenResponse('access-d', 'refresh-d') })
    renderApp({ path: '/login', platform: desktop(shell), fetchFn })

    await userEvent.click(screen.getByRole('button', { name: 'Log in with your browser' }))
    await userEvent.click(await screen.findByRole('button', { name: 'Open the browser again' }))

    await waitFor(() => expect(shell.opened).toHaveLength(2))
    expect(shell.opened[1]).toBe(shell.opened[0])
    const state = new URL(shell.opened[0] ?? '').searchParams.get('state') ?? ''
    act(() => shell.sendCallback(`trekky://auth/callback?code=the-code&state=${state}`))
    expect(await screen.findByText('You are the Owner of Acme.')).toBeInTheDocument()
  })

  it('says so when the browser cannot be opened', async () => {
    const shell = fakeDesktopShell({ openInBrowser: vi.fn().mockRejectedValue(new Error('no browser')) })
    renderApp({ path: '/login', platform: desktop(shell) })

    await userEvent.click(screen.getByRole('button', { name: 'Log in with your browser' }))

    // FormError's alert region is always on the page: wait for its text, not for the region.
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('Something went wrong. Please try again.'))
    expect(screen.queryByText('Finish in your browser. Trekky continues here automatically.')).not.toBeInTheDocument()
  })

  it('a stray callback while signed in leaves the page alone', async () => {
    const shell = fakeDesktopShell()
    const fetchFn = fakeApi({ 'POST /connect/token': () => tokenResponse('access-2', 'refresh-2') })
    const { router } = renderApp({ path: '/', platform: desktop(shell, 'stored-refresh'), fetchFn })
    await screen.findByText('You are the Owner of Acme.')

    act(() => shell.sendCallback('trekky://auth/callback?code=x&state=whatever'))

    expect(router.state.location.pathname).toBe('/')
    expect(screen.getByText('You are the Owner of Acme.')).toBeInTheDocument()
  })

  it('starts signed in with the refresh token kept from last time, and keeps the rotated one', async () => {
    const shell = fakeDesktopShell()
    shell.credentials.set(SERVER, 'stored-refresh')
    const fetchFn = fakeApi({ 'POST /connect/token': () => tokenResponse('access-2', 'refresh-2') })
    renderApp({ path: '/', platform: desktop(shell, 'stored-refresh'), fetchFn })

    expect(screen.getByRole('status')).toHaveTextContent('Signing you in…')
    expect(await screen.findByText('You are the Owner of Acme.')).toBeInTheDocument()
    const form = await formOf(requestsTo(fetchFn, 'POST /connect/token')[0] as Request)
    expect(form.get('grant_type')).toBe('refresh_token')
    expect(form.get('refresh_token')).toBe('stored-refresh')
    expect(form.get('client_id')).toBe('trekky-desktop')
    expect(shell.credentials.get(SERVER)).toBe('refresh-2')
    expect(shell.opened).toEqual([])
  })

  it('forgets a rejected stored token and shows the login page', async () => {
    const shell = fakeDesktopShell()
    shell.credentials.set(SERVER, 'revoked-refresh')
    const fetchFn = fakeApi({ 'POST /connect/token': () => jsonResponse({ error: 'invalid_grant' }, 400) })
    renderApp({ path: '/', platform: desktop(shell, 'revoked-refresh'), fetchFn })

    expect(await screen.findByRole('button', { name: 'Log in with your browser' })).toBeInTheDocument()
    await waitFor(() => expect(shell.credentials.has(SERVER)).toBe(false))
  })

  it('logs out on the server with the bearer token and removes the stored token', async () => {
    const shell = fakeDesktopShell()
    shell.credentials.set(SERVER, 'stored-refresh')
    const fetchFn = fakeApi({
      'POST /connect/token': () => tokenResponse('access-2', 'refresh-2'),
      'POST /api/v1/auth/logout': () => new Response(null, { status: 204 }),
    })
    renderApp({ path: '/', platform: desktop(shell, 'stored-refresh'), fetchFn })
    await screen.findByText('You are the Owner of Acme.')

    await userEvent.click(screen.getByRole('button', { name: 'Log out' }))

    expect(await screen.findByRole('button', { name: 'Log in with your browser' })).toBeInTheDocument()
    expect(requestsTo(fetchFn, 'POST /api/v1/auth/logout')[0]?.headers.get('Authorization')).toBe('Bearer access-2')
    expect(shell.credentials.has(SERVER)).toBe(false)
  })
})
