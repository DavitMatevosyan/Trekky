import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { fakeApi, jsonResponse, problemResponse, renderApp, requestsTo, validTokens } from '../test/renderApp.tsx'

const trekky = { id: '0190a000-0000-7000-8000-0000000000b1', key: 'TRK', name: 'Trekky', itemCount: 0, createdAt: '2026-10-26T10:00:00Z' }
const website = { id: '0190a000-0000-7000-8000-0000000000b2', key: 'WEB', name: 'Website', itemCount: 1, createdAt: '2026-10-26T10:00:00Z' }

function renderProjects(handlers: Parameters<typeof fakeApi>[0] = {}) {
  const fetchFn = fakeApi(handlers)
  renderApp({ tokens: validTokens, fetchFn })
  return fetchFn
}

async function openForm() {
  await userEvent.click(await screen.findByRole('button', { name: 'New project' }))
  return screen.getByRole('form', { name: 'New project' })
}

describe('ProjectsPage', () => {
  it('shows an empty state when the organization has no projects', async () => {
    renderProjects()

    expect(await screen.findByText('No projects yet. Create one to start tracking work.')).toBeInTheDocument()
  })

  it('lists projects with their key and item count', async () => {
    renderProjects({ 'GET /api/v1/projects': () => jsonResponse({ items: [trekky, website], nextCursor: null }) })

    const list = await screen.findByRole('list', { name: 'Projects' })
    const rows = within(list).getAllByRole('listitem')
    expect(rows.map((row) => row.textContent)).toEqual(['TRKTrekky0 items', 'WEBWebsite1 item'])
    expect(screen.queryByRole('button', { name: 'Load more' })).not.toBeInTheDocument()
  })

  it('loads the next page with the cursor', async () => {
    const fetchFn = renderProjects({
      'GET /api/v1/projects': (request) =>
        new URL(request.url).searchParams.get('cursor') === 'page-2'
          ? jsonResponse({ items: [website], nextCursor: null })
          : jsonResponse({ items: [trekky], nextCursor: 'page-2' }),
    })

    await userEvent.click(await screen.findByRole('button', { name: 'Load more' }))

    expect(await screen.findByText('Website')).toBeInTheDocument()
    expect(requestsTo(fetchFn, 'GET /api/v1/projects')).toHaveLength(2)
  })

  it('suggests the key from the name until the user edits it', async () => {
    renderProjects()
    const form = await openForm()
    const name = within(form).getByLabelText('Name')
    const key = within(form).getByLabelText('Key')

    expect(name).toHaveFocus()
    await userEvent.type(name, 'Trekky')
    expect(key).toHaveValue('TRK')
    expect(key).toHaveAccessibleDescription('Suggested from the name. It can’t be changed later.')

    await userEvent.clear(key)
    await userEvent.type(key, 'tk2')
    expect(key).toHaveValue('TK2')
    await userEvent.type(name, ' Docs')
    expect(key).toHaveValue('TK2')
  })

  it('creates a project, refreshes the list and returns focus to the button', async () => {
    let created = false
    const fetchFn = renderProjects({
      'POST /api/v1/projects': () => {
        created = true
        return jsonResponse(trekky, 201)
      },
      'GET /api/v1/projects': () => jsonResponse({ items: created ? [trekky] : [], nextCursor: null }),
    })
    const form = await openForm()

    await userEvent.type(within(form).getByLabelText('Name'), 'Trekky')
    await userEvent.click(within(form).getByRole('button', { name: 'Create project' }))

    expect(await screen.findByRole('list', { name: 'Projects' })).toHaveTextContent('TRKTrekky0 items')
    expect(screen.queryByRole('form', { name: 'New project' })).not.toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent('Created project TRK · Trekky.')
    await waitFor(() => expect(screen.getByRole('button', { name: 'New project' })).toHaveFocus())
    expect(await requestsTo(fetchFn, 'POST /api/v1/projects')[0]?.json()).toEqual({ name: 'Trekky', key: 'TRK' })
  })

  it('shows a taken key on the key field', async () => {
    renderProjects({
      'POST /api/v1/projects': () =>
        problemResponse(409, { title: 'Key already used', detail: 'A project with this key already exists in your organization.' }),
    })
    const form = await openForm()

    await userEvent.type(within(form).getByLabelText('Name'), 'Trekky')
    await userEvent.click(within(form).getByRole('button', { name: 'Create project' }))

    const key = within(form).getByLabelText('Key')
    await waitFor(() => expect(key).toHaveAttribute('aria-invalid', 'true'))
    expect(key).toHaveAccessibleDescription(
      'Suggested from the name. It can’t be changed later. A project with this key already exists in your organization.',
    )
  })

  it('shows validation errors next to their fields', async () => {
    renderProjects({
      'POST /api/v1/projects': () =>
        problemResponse(400, { title: 'Validation failed', errors: { name: ['Name is required.'], key: ['Key is required.'] } }),
    })
    const form = await openForm()

    await userEvent.click(within(form).getByRole('button', { name: 'Create project' }))

    expect(await within(form).findByText('Name is required.')).toBeInTheDocument()
    expect(within(form).getByText('Key is required.')).toBeInTheDocument()
  })

  it('cancel closes the form without creating anything', async () => {
    const fetchFn = renderProjects()
    const form = await openForm()

    await userEvent.click(within(form).getByRole('button', { name: 'Cancel' }))

    expect(screen.queryByRole('form', { name: 'New project' })).not.toBeInTheDocument()
    expect(requestsTo(fetchFn, 'POST /api/v1/projects')).toHaveLength(0)
  })
})
