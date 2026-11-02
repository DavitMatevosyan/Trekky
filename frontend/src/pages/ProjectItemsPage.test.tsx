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
const story = { ...epic, id: '0190a000-0000-7000-8000-0000000000c2', key: 'TRK-2', type: 'story', title: 'Comments on items', status: 'todo', parentKey: 'TRK-1' }
const detail = (summary: typeof epic) => ({ ...summary, projectId: project.id, descriptionMd: null, parent: null, children: [], createdAt: summary.updatedAt })

const itemsPath = `GET /api/v1/projects/${project.id}/work-items`
const createPath = `POST /api/v1/projects/${project.id}/work-items`

function renderItems(handlers: Parameters<typeof fakeApi>[0] = {}) {
  const fetchFn = fakeApi({
    'GET /api/v1/projects/by-key/TRK': () => jsonResponse(project),
    [itemsPath]: () => jsonResponse({ items: [story, epic], nextCursor: null }),
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
  it('lists the items with key, type, title, status, parent and updated', async () => {
    renderItems()

    const table = await screen.findByRole('table', { name: 'Work items' })
    expect(within(table).getAllByRole('columnheader').map((th) => th.textContent)).toEqual([
      'Key',
      'Type',
      'Title',
      'Status',
      'Parent',
      'Updated',
    ])
    const [, first, second] = within(table).getAllByRole('row') as [HTMLElement, HTMLElement, HTMLElement]
    expect(within(first).getAllByRole('cell').map((td) => td.textContent).slice(0, 5)).toEqual([
      'TRK-2',
      'Story',
      'Comments on items',
      'To Do',
      'TRK-1',
    ])
    expect(within(second).getAllByRole('cell')[4]).toHaveTextContent('—')
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

  it('finds the parent from the key typed and names the allowed parent types', async () => {
    const { fetchFn } = renderItems({
      'GET /api/v1/work-items/by-key/TRK-1': () => jsonResponse(detail(epic)),
      [createPath]: () => jsonResponse(detail({ ...story, key: 'TRK-3' }), 201),
    })
    const form = await openForm()

    const parent = within(form).getByLabelText('Parent key')
    expect(parent).toHaveAccessibleDescription('Optional: Epic or Story.')
    await userEvent.selectOptions(within(form).getByLabelText('Type'), 'subtask')
    expect(parent).toHaveAccessibleDescription('Required: Story or Task.')
    await userEvent.selectOptions(within(form).getByLabelText('Type'), 'story')
    await userEvent.type(within(form).getByLabelText('Title'), 'Child')
    await userEvent.type(within(form).getByLabelText('Description'), '**Why** it matters')
    await userEvent.type(parent, 'trk-1')
    expect(parent).toHaveValue('TRK-1')
    await userEvent.click(within(form).getByRole('button', { name: 'Create item' }))

    await waitFor(() => expect(requestsTo(fetchFn, createPath)).toHaveLength(1))
    expect(await requestsTo(fetchFn, createPath)[0]?.json()).toEqual({
      type: 'story',
      title: 'Child',
      descriptionMd: '**Why** it matters',
      parentId: epic.id,
    })
  })

  it('shows an unknown parent key on the parent field without creating anything', async () => {
    const { fetchFn } = renderItems({ 'GET /api/v1/work-items/by-key/TRK-9': () => problemResponse(404, { title: 'Work item not found' }) })
    const form = await openForm()

    await userEvent.type(within(form).getByLabelText('Title'), 'Child')
    await userEvent.type(within(form).getByLabelText('Parent key'), 'TRK-9')
    await userEvent.click(within(form).getByRole('button', { name: 'Create item' }))

    await waitFor(() => expect(within(form).getByLabelText('Parent key')).toHaveAttribute('aria-invalid', 'true'))
    expect(within(form).getByText('There is no item TRK-9 in this project.')).toBeInTheDocument()
    expect(requestsTo(fetchFn, createPath)).toHaveLength(0)
  })

  it('shows a parent rule violation on the parent field', async () => {
    renderItems({
      'GET /api/v1/work-items/by-key/TRK-2': () => jsonResponse(detail(story)),
      [createPath]: () =>
        problemResponse(422, {
          title: 'Parent not allowed',
          detail: 'A Story can only have a parent of type Epic, not Story.',
          allowedParentTypes: ['epic'],
          conflictingChildKeys: [],
        }),
    })
    const form = await openForm()

    await userEvent.selectOptions(within(form).getByLabelText('Type'), 'story')
    await userEvent.type(within(form).getByLabelText('Title'), 'Child')
    await userEvent.type(within(form).getByLabelText('Parent key'), 'TRK-2')
    await userEvent.click(within(form).getByRole('button', { name: 'Create item' }))

    expect(await within(form).findByText('A Story can only have a parent of type Epic, not Story.')).toBeInTheDocument()
    expect(within(form).getByLabelText('Parent key')).toHaveAttribute('aria-invalid', 'true')
  })

  it('shows validation errors next to their fields', async () => {
    renderItems({ [createPath]: () => problemResponse(400, { title: 'Validation failed', errors: { title: ['Title is required.'] } }) })
    const form = await openForm()

    await userEvent.click(within(form).getByRole('button', { name: 'Create item' }))

    expect(await within(form).findByText('Title is required.')).toBeInTheDocument()
  })
})
