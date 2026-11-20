import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beginAuthorization } from '../auth/oauthClient.ts'
import { fakeApi, jsonResponse, me, problemResponse, renderApp, requestsTo, validTokens } from '../test/renderApp.tsx'

vi.mock('../auth/oauthClient.ts', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../auth/oauthClient.ts')>()),
  beginAuthorization: vi.fn().mockResolvedValue(undefined),
}))

const TOKEN = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQ'

const preview = {
  email: 'grace@example.com',
  role: 'viewer',
  organization: { id: me.organization.id, name: 'Acme' },
  invitedBy: 'Ada',
  expiresAt: '2026-11-27T12:00:00Z',
}

describe('InvitePage', () => {
  beforeEach(() => vi.mocked(beginAuthorization).mockClear())

  it('shows who invited whom to which organization and role, with the email fixed', async () => {
    const fetchFn = fakeApi({ 'POST /api/v1/auth/invites/preview': () => jsonResponse(preview) })
    renderApp({ path: `/invite#${TOKEN}`, fetchFn })

    expect(await screen.findByText('Ada invited you to join Acme as a Viewer.')).toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toHaveValue('grace@example.com')
    expect(screen.getByLabelText('Email')).toHaveAttribute('readonly')
    expect(await requestsTo(fetchFn, 'POST /api/v1/auth/invites/preview')[0]?.json()).toEqual({ token: TOKEN })
  })

  it('accepts with a password and name, then signs the new member in', async () => {
    const fetchFn = fakeApi({
      'POST /api/v1/auth/invites/preview': () => jsonResponse(preview),
      'POST /api/v1/auth/invites/accept': () => jsonResponse({ ...me, role: 'viewer' }, 201),
    })
    renderApp({ path: `/invite#${TOKEN}`, fetchFn })

    await userEvent.type(await screen.findByLabelText('Your name'), 'Grace')
    await userEvent.type(screen.getByLabelText('Password'), 'correct-horse-battery')
    await userEvent.click(screen.getByRole('button', { name: 'Join' }))

    await waitFor(() => expect(beginAuthorization).toHaveBeenCalledWith(expect.objectContaining({ returnTo: '/' })))
    expect(await requestsTo(fetchFn, 'POST /api/v1/auth/invites/accept')[0]?.json()).toEqual({
      token: TOKEN,
      password: 'correct-horse-battery',
      displayName: 'Grace',
    })
  })

  it('shows field errors from the API next to their fields', async () => {
    renderApp({
      path: `/invite#${TOKEN}`,
      fetchFn: fakeApi({
        'POST /api/v1/auth/invites/preview': () => jsonResponse(preview),
        'POST /api/v1/auth/invites/accept': () =>
          problemResponse(400, { title: 'Validation', errors: { password: ['Password must be at least 12 characters.'] } }),
      }),
    })

    await userEvent.type(await screen.findByLabelText('Password'), 'short')
    await userEvent.click(screen.getByRole('button', { name: 'Join' }))

    expect(await screen.findByText('Password must be at least 12 characters.')).toBeInTheDocument()
    expect(beginAuthorization).not.toHaveBeenCalled()
  })

  it.each([
    [410, 'This invite has expired. Ask the person who invited you to send a new one.'],
    [410, 'This invite has already been used. Log in instead.'],
    [404, 'This invite link is not valid. If you got a newer invite email, use the link in that one.'],
  ])('explains a link that cannot be used (%i)', async (status, detail) => {
    renderApp({ path: `/invite#${TOKEN}`, fetchFn: fakeApi({ 'POST /api/v1/auth/invites/preview': () => problemResponse(status, { detail }) }) })

    expect(await screen.findByRole('heading', { name: 'This invite can’t be used' })).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent(detail)
    expect(screen.getByRole('link', { name: 'Log in' })).toHaveAttribute('href', '/login')
  })

  it('treats a mangled link as an invalid one instead of crashing', async () => {
    const detail = 'This invite link is not valid. If you got a newer invite email, use the link in that one.'
    renderApp({ path: '/invite#%E0%A4%A', fetchFn: fakeApi({ 'POST /api/v1/auth/invites/preview': () => problemResponse(404, { detail }) }) })

    expect(await screen.findByRole('alert')).toHaveTextContent(detail)
  })

  it('says so when the link has no token', () => {
    const fetchFn = fakeApi()
    renderApp({ path: '/invite', fetchFn })

    expect(screen.getByRole('alert')).toHaveTextContent('This invite link is incomplete.')
    expect(requestsTo(fetchFn, 'POST /api/v1/auth/invites/preview')).toHaveLength(0)
  })

  it('asks someone already signed in to log out first', async () => {
    const fetchFn = fakeApi({
      'POST /api/v1/auth/logout': () => new Response(null, { status: 204 }),
      'POST /api/v1/auth/invites/preview': () => jsonResponse(preview),
    })
    renderApp({ path: `/invite#${TOKEN}`, fetchFn, tokens: validTokens })

    expect(await screen.findByText('You are signed in as ada@example.com. Log out to accept this invite with a new account.')).toBeInTheDocument()
    expect(requestsTo(fetchFn, 'POST /api/v1/auth/invites/preview')).toHaveLength(0)

    await userEvent.click(screen.getByRole('button', { name: 'Log out' }))

    expect(await screen.findByText('Ada invited you to join Acme as a Viewer.')).toBeInTheDocument()
  })
})
