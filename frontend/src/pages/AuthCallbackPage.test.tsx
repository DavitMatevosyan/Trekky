import { screen, waitFor } from '@testing-library/react'
import { beginAuthorization } from '../auth/oauthClient.ts'
import { fakeApi, jsonResponse, renderApp } from '../test/renderApp.tsx'

/** Runs a real beginAuthorization to store state + verifier, and returns the state it sent. */
async function pendingState(returnTo = '/'): Promise<string> {
  const navigate = vi.fn<(url: string) => void>()
  await beginAuthorization({ returnTo, navigate })
  return new URL(navigate.mock.calls[0]?.[0] ?? '', window.location.origin).searchParams.get('state') ?? ''
}

describe('AuthCallbackPage', () => {
  beforeEach(() => sessionStorage.clear())

  it('stores the tokens and opens the page the user was headed to', async () => {
    const state = await pendingState('/')
    const fetchFn = fakeApi({
      'POST /connect/token': () => jsonResponse({ access_token: 'access', refresh_token: 'refresh', expires_in: 900 }),
    })

    const { session, router } = renderApp({ path: `/auth/callback?code=abc&state=${state}`, fetchFn })

    expect(await screen.findByText('You are the Owner of Acme.')).toBeInTheDocument()
    expect(session.status).toBe('authenticated')
    expect(router.state.location.pathname).toBe('/')
  })

  it('sends the user to the login page when there is no session, keeping where they were headed', async () => {
    const state = await pendingState('/projects')

    const { router } = renderApp({ path: `/auth/callback?error=login_required&state=${state}` })

    expect(await screen.findByRole('heading', { name: 'Log in to Trekky' })).toBeInTheDocument()
    await waitFor(() => expect(router.state.location.search).toBe('?returnTo=%2Fprojects'))
  })
})
