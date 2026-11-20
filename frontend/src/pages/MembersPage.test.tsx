import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { fakeApi, jsonResponse, me, problemResponse, renderApp, requestsTo, validTokens } from '../test/renderApp.tsx'

const pending = {
  id: '0190a000-0000-7000-8000-0000000000c1',
  email: 'grace@example.com',
  role: 'member',
  status: 'pending',
  invitedBy: me.user,
  createdAt: '2026-11-20T10:00:00Z',
  sentAt: '2026-11-20T10:00:00Z',
  expiresAt: '2099-11-27T10:00:00Z',
}
const expiredAdmin = { ...pending, id: '0190a000-0000-7000-8000-0000000000c2', email: 'linus@example.com', role: 'admin', status: 'expired' }

function sent(invite: typeof pending, emailSent: boolean) {
  return { invite, acceptUrl: 'http://localhost:5173/invite#tok', emailSent }
}

function renderMembers(handlers: Parameters<typeof fakeApi>[0] = {}, role = 'owner') {
  const fetchFn = fakeApi({
    'GET /api/v1/me': () => jsonResponse({ ...me, role }),
    'GET /api/v1/invites': () => jsonResponse({ items: [] }),
    ...handlers,
  })
  renderApp({ path: '/members', tokens: validTokens, fetchFn })
  return fetchFn
}

describe('MembersPage', () => {
  it('lists the members with their email and role, marking the signed-in user', async () => {
    renderMembers({
      'GET /api/v1/members': () =>
        jsonResponse({
          items: [
            { user: me.user, role: 'owner' },
            { user: { id: '0190a000-0000-7000-8000-0000000000aa', displayName: 'Grace', email: 'grace@example.com' }, role: 'viewer' },
          ],
        }),
    })

    const list = await screen.findByRole('list', { name: 'Members of your organization' })
    expect(within(list).getAllByRole('listitem').map((row) => row.textContent)).toEqual([
      'ada@example.com(you)ada@example.comOwner',
      'Gracegrace@example.comViewer',
    ])
  })

  it('offers an Owner every invitable role, and an Admin only Member and Viewer', async () => {
    renderMembers()
    const ownerRoles = within(await screen.findByLabelText('Role')).getAllByRole('option').map((o) => o.textContent)
    expect(ownerRoles).toEqual(['Admin', 'Member', 'Viewer'])
  })

  it('does not offer Admin to an Admin', async () => {
    renderMembers({}, 'admin')
    const adminRoles = within(await screen.findByLabelText('Role')).getAllByRole('option').map((o) => o.textContent)
    expect(adminRoles).toEqual(['Member', 'Viewer'])
  })

  it.each(['member', 'viewer'])('shows a %s the members but no invites', async (role) => {
    const fetchFn = renderMembers({}, role)

    expect(await screen.findByRole('list', { name: 'Members of your organization' })).toBeInTheDocument()
    expect(screen.queryByRole('form', { name: 'Invite people' })).not.toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Pending invites' })).not.toBeInTheDocument()
    expect(requestsTo(fetchFn, 'GET /api/v1/invites')).toHaveLength(0)
  })

  it('sends an invite and confirms it was emailed', async () => {
    let created = false
    const fetchFn = renderMembers({
      'POST /api/v1/invites': () => {
        created = true
        return jsonResponse(sent(pending, true), 201)
      },
      'GET /api/v1/invites': () => jsonResponse({ items: created ? [pending] : [] }),
    })
    const form = await screen.findByRole('form', { name: 'Invite people' })

    await userEvent.type(within(form).getByLabelText('Email'), 'grace@example.com')
    await userEvent.selectOptions(within(form).getByLabelText('Role'), 'viewer')
    await userEvent.click(within(form).getByRole('button', { name: 'Send invite' }))

    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('Invite sent to grace@example.com.'))
    expect(within(form).getByLabelText('Email')).toHaveValue('')
    expect(screen.queryByLabelText('Invite link')).not.toBeInTheDocument()
    expect(await within(await screen.findByRole('list', { name: 'Pending invites' })).findByText('grace@example.com')).toBeInTheDocument()
    expect(await requestsTo(fetchFn, 'POST /api/v1/invites')[0]?.json()).toEqual({ email: 'grace@example.com', role: 'viewer' })
  })

  it('gives the link to copy when no email went out', async () => {
    const user = userEvent.setup()
    renderMembers({ 'POST /api/v1/invites': () => jsonResponse(sent(pending, false), 201) })
    const form = await screen.findByRole('form', { name: 'Invite people' })

    await user.type(within(form).getByLabelText('Email'), 'grace@example.com')
    await user.click(within(form).getByRole('button', { name: 'Send invite' }))

    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent(/no email went out/))
    expect(screen.getByLabelText('Invite link')).toHaveValue('http://localhost:5173/invite#tok')
    expect(screen.getByLabelText('Invite link')).toHaveAccessibleDescription('Works once, for 7 days.')

    await user.click(screen.getByRole('button', { name: 'Copy link' }))
    expect(await navigator.clipboard.readText()).toBe('http://localhost:5173/invite#tok')
    expect(screen.getByRole('button', { name: 'Copied' })).toBeInTheDocument()
  })

  it('shows why an invite was refused', async () => {
    renderMembers({
      'POST /api/v1/invites': () =>
        problemResponse(409, { detail: 'This email already belongs to another organization. A person can be in only one organization.' }),
    })
    const form = await screen.findByRole('form', { name: 'Invite people' })

    await userEvent.type(within(form).getByLabelText('Email'), 'taken@example.com')
    await userEvent.click(within(form).getByRole('button', { name: 'Send invite' }))

    await waitFor(() => expect(within(form).getByRole('alert')).toHaveTextContent('This email already belongs to another organization.'))
  })

  it('resends and withdraws pending invites, marking expired ones', async () => {
    let withdrawn = false
    const fetchFn = renderMembers({
      'GET /api/v1/invites': () => jsonResponse({ items: withdrawn ? [expiredAdmin] : [pending, expiredAdmin] }),
      [`POST /api/v1/invites/${pending.id}/resend`]: () => jsonResponse(sent(pending, true)),
      [`DELETE /api/v1/invites/${pending.id}`]: () => {
        withdrawn = true
        return new Response(null, { status: 204 })
      },
    })
    const list = await screen.findByRole('list', { name: 'Pending invites' })
    expect(within(list).getByText('Expired')).toBeInTheDocument()

    await userEvent.click(within(list).getByRole('button', { name: 'Resend the invite to grace@example.com' }))
    await waitFor(() =>
      expect(screen.getByRole('status')).toHaveTextContent('Invite sent again to grace@example.com. The earlier link no longer works.'),
    )

    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true)
    await userEvent.click(within(list).getByRole('button', { name: 'Withdraw the invite to grace@example.com' }))

    expect(confirm).toHaveBeenCalledWith('Withdraw the invite to grace@example.com? The link will stop working.')
    await waitFor(() => expect(within(list).queryByText('grace@example.com')).not.toBeInTheDocument())
    expect(screen.getByRole('status')).toHaveTextContent('Invite to grace@example.com withdrawn.')
    expect(requestsTo(fetchFn, `DELETE /api/v1/invites/${pending.id}`)).toHaveLength(1)
    confirm.mockRestore()
  })

  it('keeps an invite when withdrawing is cancelled', async () => {
    const fetchFn = renderMembers({ 'GET /api/v1/invites': () => jsonResponse({ items: [pending] }) })
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false)

    await userEvent.click(await screen.findByRole('button', { name: 'Withdraw the invite to grace@example.com' }))

    expect(requestsTo(fetchFn, `DELETE /api/v1/invites/${pending.id}`)).toHaveLength(0)
    confirm.mockRestore()
  })

  it('does not let an Admin change an invite for an Admin', async () => {
    renderMembers({ 'GET /api/v1/invites': () => jsonResponse({ items: [pending, expiredAdmin] }) }, 'admin')
    const list = await screen.findByRole('list', { name: 'Pending invites' })

    expect(within(list).getByRole('button', { name: 'Resend the invite to grace@example.com' })).toBeInTheDocument()
    expect(within(list).queryByRole('button', { name: 'Resend the invite to linus@example.com' })).not.toBeInTheDocument()
    expect(within(list).queryByRole('button', { name: 'Withdraw the invite to linus@example.com' })).not.toBeInTheDocument()
  })

  it('is linked from the app bar', async () => {
    renderMembers()

    expect(await screen.findByRole('link', { name: 'Members' })).toHaveAttribute('href', '/members')
  })
})
