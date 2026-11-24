import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { fakeApi, jsonResponse, problemResponse, renderApp, requestsTo, validTokens } from '../test/renderApp.tsx'

const connection = {
  id: '0190a000-0000-7000-8000-0000000000e1',
  installationId: 4242,
  account: 'acme',
  accountType: 'Organization',
  manageUrl: 'https://github.com/organizations/acme/settings/installations/4242',
  connectedAt: '2026-11-20T10:00:00Z',
}

function renderCallback(query: string, handlers: Parameters<typeof fakeApi>[0] = {}) {
  const fetchFn = fakeApi({
    'POST /api/v1/github/installations': () => jsonResponse(connection, 201),
    'GET /api/v1/github': () => jsonResponse({ configured: true, installUrl: 'https://github.com/apps/t/installations/new', installations: [connection] }),
    ...handlers,
  })
  const view = renderApp({ path: `/settings/github/callback?${query}`, tokens: validTokens, fetchFn })
  return { ...view, fetchFn }
}

describe('GitHubCallbackPage', () => {
  afterEach(() => sessionStorage.clear())

  it('connects at once when GitHub returns the state this tab sent, then announces it in settings', async () => {
    sessionStorage.setItem('trekky.github.installState', 'abc123')
    const { fetchFn, router } = renderCallback('code=c0de&installation_id=4242&setup_action=install&state=abc123')

    expect(await screen.findByText('Connected acme.')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/settings')
    const [request] = requestsTo(fetchFn, 'POST /api/v1/github/installations')
    expect(await request?.json()).toEqual({ code: 'c0de', installationId: 4242 })
    expect(requestsTo(fetchFn, 'POST /api/v1/github/installations')).toHaveLength(1)
  })

  it('asks before connecting when the state is missing or not ours', async () => {
    const { fetchFn } = renderCallback('code=c0de&installation_id=4242&setup_action=install')

    expect(await screen.findByRole('heading', { name: 'Finish connecting GitHub?' })).toBeInTheDocument()
    expect(requestsTo(fetchFn, 'POST /api/v1/github/installations')).toHaveLength(0)

    await userEvent.click(screen.getByRole('button', { name: 'Connect' }))

    expect(await screen.findByText('Connected acme.')).toBeInTheDocument()
    expect(requestsTo(fetchFn, 'POST /api/v1/github/installations')).toHaveLength(1)
  })

  it('shows why the API refused the installation', async () => {
    sessionStorage.setItem('trekky.github.installState', 's')
    renderCallback('code=c0de&installation_id=4242&state=s', {
      'POST /api/v1/github/installations': () =>
        problemResponse(422, { title: 'Installation not accessible', detail: 'Your GitHub account cannot access this installation of the Trekky app.' }),
    })

    expect(await screen.findByText('Your GitHub account cannot access this installation of the Trekky app.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Back to Settings' })).toHaveAttribute('href', '/settings')
  })

  it('explains an install that waits for a GitHub organization owner', async () => {
    const { fetchFn } = renderCallback('setup_action=request&state=x')

    expect(await screen.findByText(/sent the install request to an owner of the GitHub organization/)).toBeInTheDocument()
    expect(requestsTo(fetchFn, 'POST /api/v1/github/installations')).toHaveLength(0)
  })

  it('says what is missing when GitHub sent no code', async () => {
    renderCallback('installation_id=4242')

    expect(await screen.findByText(/GitHub didn’t send back what Trekky needs/)).toBeInTheDocument()
  })
})
