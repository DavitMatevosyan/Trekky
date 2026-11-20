import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createWebPlatform } from '../platform/webPlatform.ts'
import { fakeApi, jsonResponse, me, renderApp, requestsTo, validTokens } from '../test/renderApp.tsx'

const acme = {
  id: '0190a000-0000-7000-8000-0000000000e1',
  installationId: 4242,
  account: 'acme',
  accountType: 'Organization',
  manageUrl: 'https://github.com/organizations/acme/settings/installations/4242',
  connectedAt: '2026-11-20T10:00:00Z',
}
const installUrl = 'https://github.com/apps/trekky/installations/new'

function renderSettings(handlers: Parameters<typeof fakeApi>[0] = {}, open = vi.fn<(url: string) => boolean>(() => true)) {
  const fetchFn = fakeApi(handlers)
  renderApp({ path: '/settings', tokens: validTokens, fetchFn, platform: createWebPlatform({ links: { open } }) })
  return { fetchFn, open }
}

describe('SettingsPage', () => {
  afterEach(() => sessionStorage.clear())

  it('lists connected GitHub accounts with a link to choose repositories on GitHub', async () => {
    renderSettings({ 'GET /api/v1/github': () => jsonResponse({ configured: true, installUrl, installations: [acme] }) })

    const list = await screen.findByRole('list')
    expect(within(list).getByText('acme')).toBeInTheDocument()
    expect(within(list).getByRole('link', { name: 'Choose repositories for acme on GitHub' })).toHaveAttribute('href', acme.manageUrl)
    expect(screen.getByRole('button', { name: 'Connect another account' })).toBeInTheDocument()
  })

  it('starts the install on GitHub with a state it remembers', async () => {
    const { open } = renderSettings({ 'GET /api/v1/github': () => jsonResponse({ configured: true, installUrl, installations: [] }) })

    expect(await screen.findByText('No GitHub account connected yet.')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Connect GitHub' }))

    const url = new URL(String(open.mock.calls[0]?.[0]))
    expect(url.origin + url.pathname).toBe(installUrl)
    expect(url.searchParams.get('state')).toBe(sessionStorage.getItem('trekky.github.installState'))
  })

  it('says when the server has no GitHub App and offers no connect button', async () => {
    renderSettings({ 'GET /api/v1/github': () => jsonResponse({ configured: false, installUrl: null, installations: [] }) })

    expect(await screen.findByText(/GitHub isn’t set up on this Trekky server yet/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Connect/ })).not.toBeInTheDocument()
  })

  it('disconnects an account after confirming', async () => {
    let installations = [acme]
    const { fetchFn } = renderSettings({
      'GET /api/v1/github': () => jsonResponse({ configured: true, installUrl, installations }),
      'DELETE /api/v1/github/installations/0190a000-0000-7000-8000-0000000000e1': () => {
        installations = []
        return new Response(null, { status: 204 })
      },
    })

    await userEvent.click(await screen.findByRole('button', { name: 'Disconnect acme' }))
    const confirm = screen.getByRole('group', { name: /Disconnect acme\?/ })
    expect(within(confirm).getByRole('button', { name: 'Cancel' })).toHaveFocus()
    await userEvent.click(within(confirm).getByRole('button', { name: 'Disconnect acme' }))

    expect(await screen.findByText('Disconnected acme.')).toBeInTheDocument()
    expect(await screen.findByText('No GitHub account connected yet.')).toBeInTheDocument()
    expect(requestsTo(fetchFn, 'DELETE /api/v1/github/installations/0190a000-0000-7000-8000-0000000000e1')).toHaveLength(1)
  })

  it('shows Members neither the settings link nor the GitHub section', async () => {
    const { fetchFn } = renderSettings({ 'GET /api/v1/me': () => jsonResponse({ ...me, role: 'member' }) })

    expect(await screen.findByText('Only Admins and Owners can change organization settings.')).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Settings' })).not.toBeInTheDocument()
    expect(requestsTo(fetchFn, 'GET /api/v1/github')).toHaveLength(0)
  })

  it('leaves /SETTINGS to a project with that key', async () => {
    const settingsProject = { id: '0190a000-0000-7000-8000-0000000000b9', key: 'SETTINGS', name: 'Settings app', itemCount: 0, createdAt: '2026-10-26T10:00:00Z' }
    const fetchFn = fakeApi({
      'GET /api/v1/projects/by-key/SETTINGS': () => jsonResponse(settingsProject),
      'GET /api/v1/projects/0190a000-0000-7000-8000-0000000000b9/work-items': () => jsonResponse({ items: [], nextCursor: null }),
    })
    renderApp({ path: '/SETTINGS', tokens: validTokens, fetchFn })

    expect(await screen.findByRole('heading', { name: /Settings app/ })).toBeInTheDocument()
  })

  it('shows Admins the settings link in the app bar', async () => {
    renderSettings({
      'GET /api/v1/me': () => jsonResponse({ ...me, role: 'admin' }),
      'GET /api/v1/github': () => jsonResponse({ configured: true, installUrl, installations: [] }),
    })

    expect(await screen.findByRole('link', { name: 'Settings' })).toHaveAttribute('href', '/settings')
  })
})
