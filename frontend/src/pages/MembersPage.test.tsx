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

const ada = { user: me.user, role: 'owner', kind: 'person', etag: '"10"' }
const grace = {
  user: { id: '0190a000-0000-7000-8000-0000000000aa', displayName: 'Grace', email: 'grace@example.com' },
  role: 'viewer',
  kind: 'person',
  etag: '"11"',
}
const linus = {
  user: { id: '0190a000-0000-7000-8000-0000000000ab', displayName: 'Linus', email: 'linus@example.com' },
  role: 'admin',
  kind: 'person',
  etag: '"12"',
}
const robot = {
  user: { id: '0190a000-0000-7000-8000-0000000000ac', displayName: 'Claude', email: null },
  role: 'member',
  kind: 'bot',
  etag: '"13"',
}

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
  it('shows everyone the members with name, email, person or bot, and role, marking the signed-in user', async () => {
    renderMembers({ 'GET /api/v1/members': () => jsonResponse({ items: [ada, robot, grace] }) }, 'viewer')

    const list = await screen.findByRole('list', { name: 'Members of your organization' })
    expect(within(list).getAllByRole('listitem').map((row) => row.textContent)).toEqual([
      'ada@example.com(you)ada@example.comPersonOwner',
      'ClaudeBotMember',
      'Gracegrace@example.comPersonViewer',
    ])
    expect(within(list).queryByRole('combobox')).not.toBeInTheDocument()
    expect(within(list).queryByRole('button')).not.toBeInTheDocument()
  })

  it('lets an Owner change a role, sending the member’s etag, and says so', async () => {
    let changed = false
    const fetchFn = renderMembers({
      'GET /api/v1/members': () => jsonResponse({ items: [ada, changed ? { ...grace, role: 'admin', etag: '"14"' } : grace] }),
      [`PATCH /api/v1/members/${grace.user.id}`]: () => {
        changed = true
        return jsonResponse({ ...grace, role: 'admin', etag: '"14"' })
      },
    })
    const role = await screen.findByRole('combobox', { name: 'Role of Grace' })
    expect(within(role).getAllByRole('option').map((o) => o.textContent)).toEqual(['Owner', 'Admin', 'Member', 'Viewer'])

    await userEvent.selectOptions(role, 'admin')
    // Choosing only picks the role: nothing is sent until it is saved (arrow keys on a select change it at once).
    expect(requestsTo(fetchFn, `PATCH /api/v1/members/${grace.user.id}`)).toHaveLength(0)
    await userEvent.click(screen.getByRole('button', { name: 'Save role of Grace' }))

    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('Grace is now Admin.'))
    const request = requestsTo(fetchFn, `PATCH /api/v1/members/${grace.user.id}`)[0]
    expect(request?.headers.get('If-Match')).toBe('"11"')
    expect(await request?.json()).toEqual({ role: 'admin' })
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'Role of Grace' })).toHaveValue('admin'))
    expect(screen.getByRole('combobox', { name: 'Role of Grace' })).toHaveFocus()
    expect(screen.queryByRole('button', { name: 'Save role of Grace' })).not.toBeInTheDocument()
  })

  it('offers no save until a different role is chosen', async () => {
    renderMembers({ 'GET /api/v1/members': () => jsonResponse({ items: [ada, grace] }) })
    const role = await screen.findByRole('combobox', { name: 'Role of Grace' })

    expect(screen.queryByRole('button', { name: 'Save role of Grace' })).not.toBeInTheDocument()
    await userEvent.selectOptions(role, 'member')
    expect(screen.getByRole('button', { name: 'Save role of Grace' })).toBeInTheDocument()
    await userEvent.selectOptions(role, 'viewer')
    expect(screen.queryByRole('button', { name: 'Save role of Grace' })).not.toBeInTheDocument()
  })

  it('offers an Admin only Member and Viewer, and nothing for Admins and Owners', async () => {
    renderMembers({ 'GET /api/v1/members': () => jsonResponse({ items: [ada, grace, linus] }) }, 'admin')

    const role = await screen.findByRole('combobox', { name: 'Role of Grace' })
    expect(within(role).getAllByRole('option').map((o) => o.textContent)).toEqual(['Member', 'Viewer'])
    expect(screen.getByRole('button', { name: 'Remove Grace' })).toBeInTheDocument()
    expect(screen.queryByRole('combobox', { name: 'Role of Linus' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Remove Linus' })).not.toBeInTheDocument()
    expect(screen.queryByRole('combobox', { name: 'Role of ada@example.com' })).not.toBeInTheDocument()
  })

  it('offers no change for yourself, and none for the last Owner', async () => {
    const otherOwner = { ...linus, role: 'owner' }
    renderMembers({ 'GET /api/v1/members': () => jsonResponse({ items: [ada, otherOwner] }) })

    // With two Owners, either can change the other, but nobody changes themselves here.
    expect(await screen.findByRole('combobox', { name: 'Role of Linus' })).toHaveValue('owner')
    expect(screen.queryByRole('combobox', { name: 'Role of ada@example.com' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Remove ada@example.com' })).not.toBeInTheDocument()
  })

  it('leaves the only Owner as they are, even for another Owner’s view', async () => {
    renderMembers({ 'GET /api/v1/members': () => jsonResponse({ items: [{ ...ada, role: 'admin' }, { ...linus, role: 'owner' }] }) })

    expect(await screen.findByText('Linus')).toBeInTheDocument()
    expect(screen.queryByRole('combobox', { name: 'Role of Linus' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Remove Linus' })).not.toBeInTheDocument()
  })

  it('removes a member after confirming, and says so', async () => {
    let removed = false
    const fetchFn = renderMembers({
      'GET /api/v1/members': () => jsonResponse({ items: removed ? [ada] : [ada, grace] }),
      [`DELETE /api/v1/members/${grace.user.id}`]: () => {
        removed = true
        return new Response(null, { status: 204 })
      },
    })

    await userEvent.click(await screen.findByRole('button', { name: 'Remove Grace' }))
    const confirm = screen.getByRole('group', { name: /^Remove Grace from your organization\?/ })
    expect(within(confirm).getByRole('button', { name: 'Cancel' })).toHaveFocus()
    await userEvent.click(within(confirm).getByRole('button', { name: 'Remove' }))

    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('Grace was removed from your organization.'))
    await waitFor(() => expect(screen.queryByText('grace@example.com')).not.toBeInTheDocument())
    expect(screen.getByRole('heading', { name: 'Members of your organization' })).toHaveFocus()
    expect(requestsTo(fetchFn, `DELETE /api/v1/members/${grace.user.id}`)).toHaveLength(1)
  })

  it('keeps the member when removing is cancelled', async () => {
    const fetchFn = renderMembers({ 'GET /api/v1/members': () => jsonResponse({ items: [ada, grace] }) })

    await userEvent.click(await screen.findByRole('button', { name: 'Remove Grace' }))
    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(screen.queryByRole('group', { name: /^Remove Grace/ })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Remove Grace' })).toBeInTheDocument()
    expect(requestsTo(fetchFn, `DELETE /api/v1/members/${grace.user.id}`)).toHaveLength(0)
  })

  it('shows why a role change was refused', async () => {
    renderMembers({
      'GET /api/v1/members': () => jsonResponse({ items: [ada, grace] }),
      [`PATCH /api/v1/members/${grace.user.id}`]: () =>
        problemResponse(412, { detail: 'This member was changed since you loaded the page. Their latest role is shown.' }),
    })

    await userEvent.selectOptions(await screen.findByRole('combobox', { name: 'Role of Grace' }), 'member')
    await userEvent.click(screen.getByRole('button', { name: 'Save role of Grace' }))

    const refused = () => screen.getAllByRole('alert').some((alert) => alert.textContent?.includes('This member was changed since you loaded the page.'))
    await waitFor(() => expect(refused()).toBe(true))

    // Starting another action clears the old error.
    await userEvent.click(screen.getByRole('button', { name: 'Remove Grace' }))
    expect(refused()).toBe(false)
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
