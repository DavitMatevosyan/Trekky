import { act, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { fakeApi, jsonResponse, me, problemResponse, renderApp, requestsTo, validTokens } from '../test/renderApp.tsx'

const project = { id: '0190a000-0000-7000-8000-0000000000b1', key: 'TRK', name: 'Trekky', itemCount: 2, createdAt: '2026-10-26T10:00:00Z' }
const epic = {
  id: '0190a000-0000-7000-8000-0000000000c1',
  key: 'TRK-1',
  type: 'epic',
  title: 'v1.0 Dogfood',
  status: 'in_progress',
  parentKey: null as string | null,
  updatedAt: '2026-10-26T10:00:00Z',
}
const epicAssigned = { id: '0190a000-0000-7000-8000-0000000000aa', displayName: 'Davit', email: 'davit@example.com' }
const story = { ...epic, id: '0190a000-0000-7000-8000-0000000000c2', key: 'TRK-2', type: 'story', title: 'Comments on items', status: 'todo', parentKey: 'TRK-1' }
const detail = (summary: typeof epic) => ({ ...summary, projectId: project.id, descriptionMd: null, parent: null, children: [], allowedTransitions: ['in_progress'], createdAt: summary.updatedAt, createdBy: null })

const itemsPath = `GET /api/v1/projects/${project.id}/work-items`
const createPath = `POST /api/v1/projects/${project.id}/work-items`

function renderItems(handlers: Parameters<typeof fakeApi>[0] = {}) {
  const fetchFn = fakeApi({
    'GET /api/v1/projects/by-key/TRK': () => jsonResponse(project),
    [itemsPath]: () => jsonResponse({ items: [story, { ...epic, assignee: epicAssigned }], nextCursor: null }),
    ...handlers,
  })
  const view = renderApp({ path: '/TRK', tokens: validTokens, fetchFn })
  return { ...view, fetchFn }
}

async function openForm() {
  await userEvent.click(await screen.findByRole('button', { name: 'New item' }))
  return screen.getByRole('form', { name: 'New item' })
}

describe('ProjectItemsPage', () => {
  it('lists the items with key, type, title, status, assignee, parent and updated', async () => {
    renderItems()

    const table = await screen.findByRole('table', { name: 'Work items' })
    expect(within(table).getAllByRole('columnheader').map((th) => th.textContent)).toEqual([
      'Key',
      'Type',
      'Title',
      'Status',
      'Assignee',
      'Parent',
      'Updated',
    ])
    const [, first, second] = within(table).getAllByRole('row') as [HTMLElement, HTMLElement, HTMLElement]
    expect(within(first).getAllByRole('cell').map((td) => td.textContent).slice(0, 6)).toEqual([
      'TRK-2',
      'Story',
      'Comments on items',
      'To Do',
      'Nobody',
      'TRK-1',
    ])
    expect(within(second).getAllByRole('cell')[5]).toHaveTextContent('—')
    expect(within(second).getAllByRole('cell')[4]).toHaveTextContent('Davit')
    expect(within(first).getByRole('link', { name: 'TRK-2' })).toHaveAttribute('href', '/TRK/items/TRK-2')
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('TRK · Trekky')
  })

  it('opens the item when its row is clicked', async () => {
    const { router } = renderItems({ 'GET /api/v1/work-items/by-key/TRK-2': () => jsonResponse(detail(story)) })

    await userEvent.click(await screen.findByText('Comments on items'))

    await waitFor(() => expect(router.state.location.pathname).toBe('/TRK/items/TRK-2'))
  })

  it('does not ask a viewer to create an item when the project has none (TREK-46)', async () => {
    renderItems({
      'GET /api/v1/me': () => jsonResponse({ ...me, role: 'viewer' }),
      'GET /api/v1/projects/by-key/TRK': () => jsonResponse({ ...project, itemCount: 0 }),
      [itemsPath]: () => jsonResponse({ items: [], nextCursor: null, totalCount: 0 }),
    })

    // The signed-in user's email in the header: the role is known.
    await screen.findByText(me.user.email)
    expect(await screen.findByText('No items yet.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'New item' })).not.toBeInTheDocument()
  })

  it('shows an empty state and loads more with the cursor', async () => {
    const empty = renderItems({
      'GET /api/v1/projects/by-key/TRK': () => jsonResponse({ ...project, itemCount: 0 }),
      [itemsPath]: () => jsonResponse({ items: [], nextCursor: null, totalCount: 0 }),
    })
    expect(await screen.findByText('No items yet. Create one to start tracking work.')).toBeInTheDocument()
    empty.unmount()

    const { fetchFn } = renderItems({
      [itemsPath]: (request) =>
        new URL(request.url).searchParams.get('cursor') === 'page-2'
          ? jsonResponse({ items: [epic], nextCursor: null })
          : jsonResponse({ items: [story], nextCursor: 'page-2' }),
    })
    await userEvent.click(await screen.findByRole('button', { name: 'Load more' }))

    expect(await screen.findByText('v1.0 Dogfood')).toBeInTheDocument()
    expect(requestsTo(fetchFn, itemsPath)).toHaveLength(2)
  })

  it('says so when the project does not exist', async () => {
    renderItems({ 'GET /api/v1/projects/by-key/TRK': () => problemResponse(404, { title: 'Project not found' }) })

    expect(await screen.findByRole('alert')).toHaveTextContent('This project does not exist.')
  })

  it('creates an item and refreshes the list', async () => {
    let created = false
    const { fetchFn } = renderItems({
      [createPath]: () => {
        created = true
        return jsonResponse(detail({ ...epic, key: 'TRK-3', type: 'task', title: 'Write tests', status: 'todo' }), 201)
      },
      [itemsPath]: () => jsonResponse({ items: created ? [story, epic] : [epic], nextCursor: null }),
    })
    const form = await openForm()

    expect(within(form).getByLabelText('Title')).toHaveFocus()
    expect(within(form).getByLabelText('Type')).toHaveValue('task')
    await userEvent.type(within(form).getByLabelText('Title'), 'Write tests')
    await userEvent.click(within(form).getByRole('button', { name: 'Create item' }))

    expect(await screen.findByText('Comments on items')).toBeInTheDocument()
    expect(screen.queryByRole('form', { name: 'New item' })).not.toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent('Created TRK-3 · Write tests.')
    await waitFor(() => expect(screen.getByRole('button', { name: 'New item' })).toHaveFocus())
    expect(await requestsTo(fetchFn, createPath)[0]?.json()).toEqual({ type: 'task', title: 'Write tests', descriptionMd: null, parentId: null })
  })

  it('offers only the parent types the chosen type allows and creates with the chosen parent', async () => {
    const { fetchFn } = renderItems({
      [createPath]: () => jsonResponse(detail({ ...story, key: 'TRK-3' }), 201),
    })
    const form = await openForm()
    const parent = within(form).getByRole('combobox', { name: 'Parent' })
    expect(parent).toHaveAccessibleDescription('Optional: Epic or Story.')

    await userEvent.type(within(form).getByLabelText('Title'), 'Child')
    await userEvent.type(within(form).getByLabelText('Description'), '**Why** it matters')
    await userEvent.click(parent)
    const options = await within(await within(form).findByRole('listbox')).findAllByRole('option')
    expect(options.map((option) => option.textContent)).toEqual(['TRK-2StoryComments on items', 'TRK-1Epicv1.0 Dogfood'])
    const candidates = requestsTo(fetchFn, itemsPath).map((request) => new URL(request.url).searchParams)
    expect(candidates.at(-1)?.getAll('type')).toEqual(['epic', 'story'])
    // TREK-52: the list hides epics in Done by default; they stay parent candidates.
    expect(candidates.at(-1)?.get('completedEpics')).toBe('show')
    await userEvent.click(options[1] as HTMLElement)
    expect(parent).toHaveValue('TRK-1 · v1.0 Dogfood')
    await userEvent.click(within(form).getByRole('button', { name: 'Create item' }))

    await waitFor(() => expect(requestsTo(fetchFn, createPath)).toHaveLength(1))
    expect(await requestsTo(fetchFn, createPath)[0]?.json()).toEqual({
      type: 'task',
      title: 'Child',
      descriptionMd: '**Why** it matters',
      parentId: epic.id,
    })
  })

  it('searches with the keyboard and chooses with Enter without submitting', async () => {
    const { fetchFn } = renderItems({
      [itemsPath]: (request) => {
        const q = new URL(request.url).searchParams.get('q')
        return jsonResponse({ items: q === 'dog' ? [epic] : [story, epic], nextCursor: null })
      },
    })
    const form = await openForm()
    const parent = within(form).getByRole('combobox', { name: 'Parent' })

    await userEvent.type(parent, 'dog')
    await waitFor(() => expect(within(within(form).getByRole('listbox')).getAllByRole('option')).toHaveLength(1))
    await userEvent.keyboard('{ArrowDown}')
    expect(parent).toHaveAttribute('aria-activedescendant', within(within(form).getByRole('listbox')).getByRole('option').id)
    await userEvent.keyboard('{Enter}')

    expect(parent).toHaveValue('TRK-1 · v1.0 Dogfood')
    expect(parent).toHaveAttribute('aria-expanded', 'false')
    expect(requestsTo(fetchFn, createPath)).toHaveLength(0)
    expect(requestsTo(fetchFn, itemsPath).some((request) => new URL(request.url).searchParams.get('q') === 'dog')).toBe(true)
  })

  it('drops a parent the new type does not allow, and has none for an Epic', async () => {
    renderItems()
    const form = await openForm()
    const parent = within(form).getByRole('combobox', { name: 'Parent' })
    await userEvent.click(parent)
    await userEvent.click(within(form).getByRole('option', { name: /TRK-2/ }))
    expect(parent).toHaveValue('TRK-2 · Comments on items')

    // A SubTask may sit under a Story: the choice stays.
    await userEvent.selectOptions(within(form).getByLabelText('Type'), 'subtask')
    expect(parent).toHaveValue('TRK-2 · Comments on items')
    expect(parent).toHaveAccessibleDescription('Required: Story or Task.')
    // A Story may not: it is dropped.
    await userEvent.selectOptions(within(form).getByLabelText('Type'), 'story')
    expect(parent).toHaveValue('')
    await userEvent.selectOptions(within(form).getByLabelText('Type'), 'epic')
    expect(parent).toBeDisabled()
    expect(parent).toHaveAccessibleDescription('Epic items have no parent.')
  })

  it('takes an exact key typed without choosing, and keeps anything else with why', async () => {
    // Leaving the field looks the typed key up; any other key is not found (404).
    const { fetchFn } = renderItems({ 'GET /api/v1/work-items/by-key/TRK-1': () => jsonResponse(detail(epic)) })
    const form = await openForm()
    const parent = within(form).getByRole('combobox', { name: 'Parent' })

    await userEvent.type(parent, 'trk-1')
    await waitFor(() => expect(within(within(form).getByRole('listbox')).getAllByRole('option')).toHaveLength(2))
    await userEvent.click(within(form).getByLabelText('Title'))
    await waitFor(() => expect(parent).toHaveValue('TRK-1 · v1.0 Dogfood'))

    await userEvent.click(within(form).getByRole('button', { name: 'Clear parent' }))
    await userEvent.type(parent, 'nothing like it')
    await userEvent.click(within(form).getByLabelText('Title'))
    expect(await within(form).findByText('Choose a parent from the list, or clear the field.')).toBeInTheDocument()
    expect(parent).toHaveValue('nothing like it')
    expect(parent).toHaveAttribute('aria-invalid', 'true')
    // Text that is not a key is never looked up.
    expect(requestsTo(fetchFn, 'GET /api/v1/work-items/by-key/NOTHING%20LIKE%20IT')).toHaveLength(0)
  })

  it('creates with a key typed but not chosen, whether submitted by click or by Enter', async () => {
    const byKey = { 'GET /api/v1/work-items/by-key/TRK-1': () => jsonResponse(detail(epic)) }
    for (const submit of ['click', 'escape then enter'] as const) {
      const { fetchFn, unmount } = renderItems({ ...byKey, [createPath]: () => jsonResponse(detail({ ...story, key: 'TRK-3' }), 201) })
      const form = await openForm()
      await userEvent.type(within(form).getByLabelText('Title'), 'Child')
      await userEvent.type(within(form).getByRole('combobox', { name: 'Parent' }), 'trk-1')

      if (submit === 'click') await userEvent.click(within(form).getByRole('button', { name: 'Create item' }))
      else await userEvent.keyboard('{Escape}{Enter}')

      await waitFor(() => expect(requestsTo(fetchFn, createPath)).toHaveLength(1))
      expect((await requestsTo(fetchFn, createPath)[0]?.json()) as unknown).toMatchObject({ parentId: epic.id })
      unmount()
    }
  })

  it('does not create when the typed key is not an item of this project, and says why', async () => {
    const { fetchFn } = renderItems()
    const form = await openForm()
    await userEvent.type(within(form).getByLabelText('Title'), 'Child')
    const parent = within(form).getByRole('combobox', { name: 'Parent' })
    await userEvent.type(parent, 'trk-9')

    await userEvent.click(within(form).getByRole('button', { name: 'Create item' }))

    expect(await within(form).findByText('There is no item TRK-9 in this project.')).toBeInTheDocument()
    expect(parent).toHaveValue('trk-9')
    expect(parent).toHaveAttribute('aria-invalid', 'true')
    expect(requestsTo(fetchFn, createPath)).toHaveLength(0)
  })

  it('ignores a key lookup that finishes after the type changed to one without a parent', async () => {
    let answer: (response: Response) => void = () => {}
    const { fetchFn } = renderItems({
      'GET /api/v1/work-items/by-key/TRK-1': () => new Promise<Response>((resolve) => (answer = resolve)),
      [createPath]: () => jsonResponse(detail(epic), 201),
    })
    const form = await openForm()
    await userEvent.selectOptions(within(form).getByLabelText('Type'), 'story')
    await userEvent.type(within(form).getByRole('combobox', { name: 'Parent' }), 'trk-1')
    await userEvent.type(within(form).getByLabelText('Title'), 'Big one')
    await userEvent.selectOptions(within(form).getByLabelText('Type'), 'epic')

    answer(jsonResponse(detail(epic)))
    await waitFor(() => expect(requestsTo(fetchFn, 'GET /api/v1/work-items/by-key/TRK-1')).toHaveLength(1))
    expect(within(form).getByRole('combobox', { name: 'Parent' })).toHaveValue('')
    await userEvent.click(within(form).getByRole('button', { name: 'Create item' }))

    await waitFor(() => expect(requestsTo(fetchFn, createPath)).toHaveLength(1))
    expect((await requestsTo(fetchFn, createPath)[0]?.json()) as unknown).toMatchObject({ type: 'epic', parentId: null })
  })

  it('says when no item of the allowed types matches', async () => {
    renderItems({ [itemsPath]: (request) => jsonResponse({ items: new URL(request.url).searchParams.has('type') ? [] : [story, epic], nextCursor: null }) })
    const form = await openForm()

    await userEvent.click(within(form).getByRole('combobox', { name: 'Parent' }))

    expect(await within(form).findByRole('status')).toHaveTextContent('No Epic or Story items match.')
  })

  it('shows a parent rule violation on the parent field', async () => {
    renderItems({
      [createPath]: () =>
        problemResponse(422, {
          title: 'Parent not allowed',
          detail: 'The parent must be a work item in the same project.',
          allowedParentTypes: ['epic', 'story'],
          conflictingChildKeys: [],
        }),
    })
    const form = await openForm()

    await userEvent.type(within(form).getByLabelText('Title'), 'Child')
    await userEvent.click(within(form).getByRole('combobox', { name: 'Parent' }))
    await userEvent.click(await within(form).findByRole('option', { name: /TRK-1/ }))
    await userEvent.click(within(form).getByRole('button', { name: 'Create item' }))

    expect(await within(form).findByText('The parent must be a work item in the same project.')).toBeInTheDocument()
    expect(within(form).getByRole('combobox', { name: 'Parent' })).toHaveAttribute('aria-invalid', 'true')
  })

  it('shows validation errors next to their fields', async () => {
    renderItems({ [createPath]: () => problemResponse(400, { title: 'Validation failed', errors: { title: ['Title is required.'] } }) })
    const form = await openForm()

    await userEvent.click(within(form).getByRole('button', { name: 'Create item' }))

    expect(await within(form).findByText('Title is required.')).toBeInTheDocument()
  })
})

describe('ProjectItemsPage filters', () => {
  const listRequests = (fetchFn: typeof fetch) =>
    requestsTo(fetchFn, itemsPath)
      .map((request) => new URL(request.url).searchParams)
      // Without the epic picker's searches.
      .filter((params) => !params.has('limit'))
  const lastList = (fetchFn: typeof fetch) => listRequests(fetchFn).at(-1)
  const filterBar = () => screen.getByRole('search', { name: 'Filters' })

  function renderFiltered(path = '/TRK', handlers: Parameters<typeof fakeApi>[0] = {}) {
    const fetchFn = fakeApi({
      'GET /api/v1/projects/by-key/TRK': () => jsonResponse(project),
      [itemsPath]: () => jsonResponse({ items: [story, epic], nextCursor: null, totalCount: 2 }),
      ...handlers,
    })
    return renderApp({ path, tokens: validTokens, fetchFn })
  }

  it('asks for the default view first: completed epics hidden, nothing else', async () => {
    const { fetchFn } = renderFiltered()

    expect(await screen.findByText('2 items match')).toBeInTheDocument()
    expect(lastList(fetchFn)?.toString()).toBe('')
    expect(screen.getByRole('checkbox', { name: 'Show completed epics' })).not.toBeChecked()
    expect(screen.queryByRole('list', { name: 'Active filters' })).not.toBeInTheDocument()
  })

  it('filters by the quick filters Open and Mine, keeps them in the URL and shows them as chips', async () => {
    const { fetchFn, router } = renderFiltered('/TRK', {
      [itemsPath]: (request) =>
        new URL(request.url).searchParams.get('open') === 'true'
          ? jsonResponse({ items: [story], nextCursor: null, totalCount: 1 })
          : jsonResponse({ items: [story, epic], nextCursor: null, totalCount: 2 }),
    })
    const quick = within(await screen.findByRole('group', { name: 'Quick filters' }))

    await userEvent.click(quick.getByRole('button', { name: 'Open' }))
    await userEvent.click(quick.getByRole('button', { name: 'Mine' }))

    expect(quick.getByRole('button', { name: 'Open' })).toHaveAttribute('aria-pressed', 'true')
    expect(quick.getByRole('button', { name: 'Mine' })).toHaveAttribute('aria-pressed', 'true')
    expect(quick.getByRole('button', { name: 'Unassigned' })).toHaveAttribute('aria-pressed', 'false')
    expect(router.state.location.search).toBe('?open=true&assignee=me')
    await waitFor(() => expect(lastList(fetchFn)?.toString()).toBe('open=true&assignee=me'))
    expect(await screen.findByText('1 item matches')).toBeInTheDocument()
    expect(screen.queryByText('v1.0 Dogfood')).not.toBeInTheDocument()
    const chips = within(screen.getByRole('list', { name: 'Active filters' })).getAllByRole('button')
    expect(chips.map((chip) => chip.textContent)).toEqual(['Open×', 'Assignee: Me×'])
  })

  it('reads the filters from the URL, so a shared or reloaded link shows the same list', async () => {
    const { fetchFn } = renderFiltered('/TRK?open=true&assignee=none&status=in_review&type=bug&parent=TRK-1&q=log&completedEpics=show', {
      'GET /api/v1/work-items/by-key/TRK-1': () => jsonResponse(detail(epic)),
    })

    await screen.findByText('2 items match')
    expect(lastList(fetchFn)?.toString()).toBe('open=true&status=in_review&type=bug&assignee=none&parent=TRK-1&q=log&completedEpics=show')
    expect(within(filterBar()).getByRole('button', { name: 'Unassigned' })).toHaveAttribute('aria-pressed', 'true')
    expect(within(filterBar()).getByRole('button', { name: /^Status \(1\)/ })).toBeInTheDocument()
    expect(within(filterBar()).getByRole('searchbox', { name: 'Search' })).toHaveValue('log')
    expect(within(filterBar()).getByRole('checkbox', { name: 'Show completed epics' })).toBeChecked()
    await waitFor(() => expect(within(filterBar()).getByRole('combobox', { name: 'Epic' })).toHaveValue('TRK-1 · v1.0 Dogfood'))
  })

  it('picks several statuses and types from their dropdowns, and Escape closes one back on its button', async () => {
    const { fetchFn } = renderFiltered()
    const status = await within(await screen.findByRole('search')).findByRole('button', { name: /^Status/ })

    await userEvent.click(status)
    expect(status).toHaveAttribute('aria-expanded', 'true')
    const options = within(screen.getByRole('group', { name: 'Status' }))
    await userEvent.click(options.getByRole('checkbox', { name: 'In Review' }))
    await userEvent.click(options.getByRole('checkbox', { name: 'To Do' }))
    await userEvent.keyboard('{Escape}')

    expect(status).toHaveAttribute('aria-expanded', 'false')
    expect(status).toHaveFocus()
    expect(status).toHaveTextContent('Status (2)')
    await userEvent.click(within(filterBar()).getByRole('button', { name: /^Type/ }))
    await userEvent.click(within(screen.getByRole('group', { name: 'Type' })).getByRole('checkbox', { name: 'Bug' }))

    await waitFor(() => expect(lastList(fetchFn)?.toString()).toBe('status=in_review&status=todo&type=bug'))
  })

  it('offers Me, Unassigned and the other members as assignees', async () => {
    const other = { id: '0190a000-0000-7000-8000-0000000000bb', displayName: 'Grace', email: 'grace@example.com' }
    const { fetchFn } = renderFiltered('/TRK', {
      'GET /api/v1/members': () => jsonResponse({ items: [{ user: me.user, role: 'owner' }, { user: other, role: 'member' }] }),
    })

    await userEvent.click(await within(await screen.findByRole('search')).findByRole('button', { name: /^Assignee/ }))
    const group = within(screen.getByRole('group', { name: 'Assignee' }))
    await waitFor(() => expect(group.getAllByRole('checkbox').map((box) => box.parentElement?.textContent)).toEqual(['Me', 'Unassigned', 'Grace']))
    await userEvent.click(group.getByRole('checkbox', { name: 'Grace' }))

    await waitFor(() => expect(lastList(fetchFn)?.getAll('assignee')).toEqual([other.id]))
    expect(screen.getByRole('button', { name: 'Remove filter Assignee: Grace' })).toBeInTheDocument()
  })

  it('searches once typing pauses, with one history entry for the whole search', async () => {
    const { fetchFn, router } = renderFiltered('/TRK?open=true')
    const box = await screen.findByRole('searchbox', { name: 'Search' })

    await userEvent.type(box, 'log')
    await waitFor(() => expect(router.state.location.search).toBe('?open=true&q=log'))
    expect(router.state.historyAction).toBe('PUSH')
    await waitFor(() => expect(lastList(fetchFn)?.get('q')).toBe('log'))
    expect(listRequests(fetchFn).filter((params) => params.has('q')).map((params) => params.get('q'))).toEqual(['log'])

    // More keys refine the same entry; Back returns to the list before the search.
    await userEvent.type(box, 'in')
    await waitFor(() => expect(router.state.location.search).toBe('?open=true&q=login'))
    expect(router.state.historyAction).toBe('REPLACE')
    expect(box).toHaveValue('login')
    await act(() => router.navigate(-1))
    expect(router.state.location.search).toBe('?open=true')
    expect(box).toHaveValue('')
  })

  it('drops a search still waiting to be written when Back is pressed', async () => {
    const { router } = renderFiltered('/TRK')
    const quick = within(await screen.findByRole('group', { name: 'Quick filters' }))
    await userEvent.click(quick.getByRole('button', { name: 'Open' }))
    const box = screen.getByRole('searchbox', { name: 'Search' })

    await userEvent.type(box, 'x')
    await act(() => router.navigate(-1))
    await new Promise((resolve) => setTimeout(resolve, 400))

    expect(router.state.location.search).toBe('')
    expect(box).toHaveValue('')
  })

  it('focuses the search box with /, but not while typing elsewhere', async () => {
    renderFiltered()
    const box = await screen.findByRole('searchbox', { name: 'Search' })
    expect(box).toHaveAttribute('aria-keyshortcuts', '/')

    await userEvent.keyboard('/')
    expect(box).toHaveFocus()
    expect(box).toHaveValue('')

    await userEvent.click(await screen.findByRole('button', { name: 'New item' }))
    const title = screen.getByLabelText('Title')
    await userEvent.type(title, 'a/b')
    expect(title).toHaveValue('a/b')
    expect(title).toHaveFocus()
  })

  it('removes one chip at a time and clears all back to the default', async () => {
    const { router } = renderFiltered('/TRK?open=true&type=bug&completedEpics=show')
    const chips = await screen.findByRole('list', { name: 'Active filters' })
    expect(within(chips).getAllByRole('button').map((chip) => chip.textContent)).toEqual(['Open×', 'Type: Bug×', 'Completed epics shown×'])

    await userEvent.click(within(chips).getByRole('button', { name: 'Remove filter Type: Bug' }))
    expect(router.state.location.search).toBe('?open=true&completedEpics=show')
    await waitFor(() => expect(within(chips).getByRole('button', { name: 'Remove filter Completed epics shown' })).toHaveFocus())

    await userEvent.click(screen.getByRole('button', { name: 'Clear all' }))
    expect(router.state.location.search).toBe('')
    expect(screen.queryByRole('list', { name: 'Active filters' })).not.toBeInTheDocument()
    expect(screen.getByRole('searchbox', { name: 'Search' })).toHaveFocus()
  })

  it('says when nothing matches and clears the filters from there', async () => {
    const { router } = renderFiltered('/TRK?type=bug', {
      [itemsPath]: (request) =>
        new URL(request.url).searchParams.has('type')
          ? jsonResponse({ items: [], nextCursor: null, totalCount: 0 })
          : jsonResponse({ items: [story], nextCursor: null, totalCount: 1 }),
    })

    expect(await screen.findByText('No items match these filters.')).toBeInTheDocument()
    expect(screen.getByText('0 items match')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Clear filters' }))

    expect(router.state.location.search).toBe('')
    expect(screen.getByRole('searchbox', { name: 'Search' })).toHaveFocus()
    expect(screen.queryByText('Every item is under a completed epic.')).not.toBeInTheDocument()
    expect(await screen.findByText('Comments on items')).toBeInTheDocument()
  })

  it('says when every item is under a completed epic, and shows them on request', async () => {
    const { fetchFn } = renderFiltered('/TRK', {
      [itemsPath]: (request) =>
        new URL(request.url).searchParams.get('completedEpics') === 'show'
          ? jsonResponse({ items: [story, epic], nextCursor: null, totalCount: 2 })
          : jsonResponse({ items: [], nextCursor: null, totalCount: 0 }),
    })

    expect(await screen.findByText('Every item is under a completed epic.')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Show completed epics' }))

    expect(await screen.findByText('Comments on items')).toBeInTheDocument()
    expect(lastList(fetchFn)?.get('completedEpics')).toBe('show')
    expect(screen.getByRole('checkbox', { name: 'Show completed epics' })).toBeChecked()
  })

  it('filters by an epic chosen from the searchable picker', async () => {
    const { fetchFn, router } = renderFiltered('/TRK', {
      'GET /api/v1/work-items/by-key/TRK-1': () => jsonResponse(detail(epic)),
      [itemsPath]: (request) => {
        const params = new URL(request.url).searchParams
        if (params.getAll('type').includes('epic')) return jsonResponse({ items: [epic], nextCursor: null, totalCount: 1 })
        return jsonResponse({ items: params.has('parent') ? [story] : [story, epic], nextCursor: null, totalCount: params.has('parent') ? 1 : 2 })
      },
    })
    const picker = await screen.findByRole('combobox', { name: 'Epic' })

    await userEvent.click(picker)
    await userEvent.click(await screen.findByRole('option', { name: /TRK-1/ }))

    expect(router.state.location.search).toBe('?parent=TRK-1')
    await waitFor(() => expect(picker).toHaveValue('TRK-1 · v1.0 Dogfood'))
    await waitFor(() => expect(lastList(fetchFn)?.get('parent')).toBe('TRK-1'))
    expect(await screen.findByText('1 item matches')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Clear epic' }))
    expect(router.state.location.search).toBe('')
    expect(picker).toHaveFocus()
  })

  it('shows completed epics too when the chosen epic is in Done', async () => {
    const doneEpic = { ...epic, status: 'done' }
    const { router } = renderFiltered('/TRK', {
      'GET /api/v1/work-items/by-key/TRK-1': () => jsonResponse(detail(doneEpic)),
      [itemsPath]: (request) =>
        new URL(request.url).searchParams.getAll('type').includes('epic')
          ? jsonResponse({ items: [doneEpic], nextCursor: null, totalCount: 1 })
          : jsonResponse({ items: [story], nextCursor: null, totalCount: 1 }),
    })

    await userEvent.click(await screen.findByRole('combobox', { name: 'Epic' }))
    await userEvent.click(await screen.findByRole('option', { name: /TRK-1/ }))

    expect(router.state.location.search).toBe('?parent=TRK-1&completedEpics=show')
    expect(screen.getByRole('checkbox', { name: 'Show completed epics' })).toBeChecked()
  })

  it('names the signed-in user "Me" in a shared link that filters by their id', async () => {
    renderFiltered(`/TRK?assignee=${me.user.id}`)

    expect(await screen.findByRole('button', { name: 'Remove filter Assignee: Me' })).toBeInTheDocument()
  })

  it('moves the focus to the search box after "Show completed epics" in the empty state', async () => {
    renderFiltered('/TRK', {
      [itemsPath]: (request) =>
        new URL(request.url).searchParams.get('completedEpics') === 'show'
          ? jsonResponse({ items: [story], nextCursor: null, totalCount: 1 })
          : jsonResponse({ items: [], nextCursor: null, totalCount: 0 }),
    })

    await userEvent.click(await screen.findByRole('button', { name: 'Show completed epics' }))

    expect(screen.getByRole('searchbox', { name: 'Search' })).toHaveFocus()
  })

  it('steps back and forward through filter changes', async () => {
    const { router } = renderFiltered()
    const quick = within(await screen.findByRole('group', { name: 'Quick filters' }))
    await userEvent.click(quick.getByRole('button', { name: 'Open' }))
    await userEvent.click(quick.getByRole('button', { name: 'Unassigned' }))

    await act(() => router.navigate(-1))
    expect(router.state.location.search).toBe('?open=true')
    expect(quick.getByRole('button', { name: 'Unassigned' })).toHaveAttribute('aria-pressed', 'false')
    await act(() => router.navigate(1))
    expect(quick.getByRole('button', { name: 'Unassigned' })).toHaveAttribute('aria-pressed', 'true')
  })

  it('opens an item with a breadcrumb back to the filtered list', async () => {
    renderFiltered('/TRK?open=true', {
      'GET /api/v1/work-items/by-key/TRK-2': () => jsonResponse(detail(story)),
      [`GET /api/v1/work-items/${story.id}/history`]: () => jsonResponse({ items: [], nextCursor: null }),
    })

    await userEvent.click(await screen.findByRole('link', { name: 'TRK-2' }))

    const breadcrumb = await screen.findByRole('navigation', { name: 'Breadcrumb' })
    expect(within(breadcrumb).getByRole('link', { name: 'TRK' })).toHaveAttribute('href', '/TRK?open=true')
  })
})
