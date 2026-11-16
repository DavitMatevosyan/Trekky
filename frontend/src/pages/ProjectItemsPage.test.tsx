import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { fakeApi, jsonResponse, problemResponse, renderApp, requestsTo, validTokens } from '../test/renderApp.tsx'

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

  it('shows an empty state and loads more with the cursor', async () => {
    const empty = renderItems({ [itemsPath]: () => jsonResponse({ items: [], nextCursor: null }) })
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
