import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { fakeApi, jsonResponse, problemResponse, renderApp, requestsTo, validTokens } from '../test/renderApp.tsx'

const project = { id: '0190a000-0000-7000-8000-0000000000b1', key: 'TRK', name: 'Trekky', itemCount: 3, createdAt: '2026-10-26T10:00:00Z' }
const card = (number: number, status: string, type = 'task', title = `Item ${number}`) => ({
  id: `0190a000-0000-7000-8000-${String(number).padStart(12, '0')}`,
  key: `TRK-${number}`,
  type,
  title,
  status,
  parentKey: null,
  updatedAt: '2026-10-26T10:00:00Z',
  etag: `"${number}"`,
})
const column = (status: string, items: ReturnType<typeof card>[], count = items.length) => ({ status, count, items })
const board = (done = column('done', [])) => ({
  columns: [
    column('todo', [card(1, 'todo', 'bug', 'Key not shown'), card(2, 'todo', 'tech_debt', 'Clean up')]),
    column('in_progress', [card(3, 'in_progress', 'story', 'Comments on items')]),
    column('in_review', []),
    done,
  ],
})

const boardPath = `GET /api/v1/projects/${project.id}/board`

function renderBoard(handlers: Parameters<typeof fakeApi>[0] = {}) {
  const fetchFn = fakeApi({
    'GET /api/v1/projects/by-key/TRK': () => jsonResponse(project),
    [boardPath]: () => jsonResponse(board()),
    ...handlers,
  })
  return renderApp({ path: '/TRK/board', tokens: validTokens, fetchFn })
}

describe('BoardPage', () => {
  it('shows one column per status in workflow order, with counts', async () => {
    renderBoard()

    await screen.findByRole('heading', { name: 'To Do (2)' })
    expect(screen.getAllByRole('heading', { level: 2 }).map((heading) => heading.textContent)).toEqual([
      'To Do (2)',
      'In Progress (1)',
      'In Review (0)',
      'Done (0)',
    ])
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('TRK · Trekky')
  })

  it('shows key, type and title on each card, and the card opens the item', async () => {
    renderBoard()

    const toDo = await screen.findByRole('list', { name: 'To Do' })
    const cards = within(toDo).getAllByRole('listitem')
    expect(cards).toHaveLength(2)
    expect(cards[0]).toHaveTextContent('TRK-1')
    expect(cards[0]).toHaveTextContent('Bug')
    expect(cards[0]).toHaveTextContent('Key not shown')
    expect(cards[1]).toHaveTextContent('TechDebt')
    expect(within(toDo).getByRole('link', { name: 'TRK-1' })).toHaveAttribute('href', '/TRK/items/TRK-1')
  })

  it('tells keyboard and screen-reader users how to move a card', async () => {
    renderBoard()

    const link = await screen.findByRole('link', { name: 'TRK-1' })
    expect(link).toHaveAttribute('aria-roledescription', 'movable card')
    expect(link).toHaveAccessibleDescription(
      'Press Enter to open this card. To move it, press Space, use the left and right arrow keys to change column, then press Space to drop it or Escape to cancel.',
    )
  })

  it('says when a column is empty', async () => {
    renderBoard()

    const inReview = await screen.findByRole('region', { name: 'In Review (0)' })
    expect(within(inReview).getByText('No items')).toBeInTheDocument()
  })

  it('asks for every Done card on "Show all" and keeps focus in the column', async () => {
    const latest = column('done', Array.from({ length: 20 }, (_, i) => card(30 - i, 'done')), 21)
    const all = column('done', [...latest.items, card(10, 'done')])
    const { fetchFn } = renderBoard({
      [boardPath]: (request) => jsonResponse(board(new URL(request.url).searchParams.get('allDone') === 'true' ? all : latest)),
    })

    const doneList = await screen.findByRole('list', { name: 'Done' })
    expect(screen.getByRole('heading', { name: 'Done (21)' })).toBeInTheDocument()
    expect(within(doneList).getAllByRole('listitem')).toHaveLength(20)

    await userEvent.click(screen.getByRole('button', { name: 'Show all' }))

    await waitFor(() => expect(within(screen.getByRole('list', { name: 'Done' })).getAllByRole('listitem')).toHaveLength(21))
    expect(screen.queryByRole('button', { name: 'Show all' })).not.toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Done (21)' })).toHaveFocus()
    expect(requestsTo(fetchFn, boardPath).map((request) => new URL(request.url).search)).toEqual(['', '?allDone=true'])
  })

  it('keeps the board and can try again when "Show all" fails', async () => {
    const latest = column('done', Array.from({ length: 20 }, (_, i) => card(30 - i, 'done')), 21)
    const all = column('done', [...latest.items, card(10, 'done')])
    let failing = true
    renderBoard({
      [boardPath]: (request) => {
        if (new URL(request.url).searchParams.get('allDone') !== 'true') return jsonResponse(board(latest))
        return failing ? problemResponse(500, { title: 'Server error' }) : jsonResponse(board(all))
      },
    })

    await screen.findByRole('list', { name: 'Done' })
    await userEvent.click(screen.getByRole('button', { name: 'Show all' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not load every card. Please try again.')
    expect(within(screen.getByRole('list', { name: 'Done' })).getAllByRole('listitem')).toHaveLength(20)

    failing = false
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }))

    await waitFor(() => expect(within(screen.getByRole('list', { name: 'Done' })).getAllByRole('listitem')).toHaveLength(21))
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('does not repeat a failed "Show all" on the next visit to the board', async () => {
    const latest = column('done', Array.from({ length: 20 }, (_, i) => card(30 - i, 'done')), 21)
    renderBoard({
      [boardPath]: (request) =>
        new URL(request.url).searchParams.get('allDone') === 'true'
          ? problemResponse(500, { title: 'Server error' })
          : jsonResponse(board(latest)),
      [`GET /api/v1/projects/${project.id}/work-items`]: () => jsonResponse({ items: [], nextCursor: null }),
    })

    await userEvent.click(await screen.findByRole('button', { name: 'Show all' }))
    await screen.findByRole('alert')

    await userEvent.click(screen.getByRole('link', { name: 'List' }))
    await screen.findByText('No items yet. Create one to start tracking work.')
    await userEvent.click(screen.getByRole('link', { name: 'Board' }))

    await screen.findByRole('list', { name: 'Done' })
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('keeps "Show all" in the URL, and the breadcrumb of a card returns to that board', async () => {
    const latest = column('done', Array.from({ length: 20 }, (_, i) => card(30 - i, 'done')), 21)
    const all = column('done', [...latest.items, card(10, 'done')])
    const detail = { ...card(1, 'todo', 'bug', 'Key not shown'), projectId: project.id, descriptionMd: null, parent: null, children: [], allowedTransitions: ['in_progress'], createdAt: '2026-10-26T10:00:00Z', createdBy: null }
    const { router } = renderBoard({
      [boardPath]: (request) => jsonResponse(board(new URL(request.url).searchParams.get('allDone') === 'true' ? all : latest)),
      'GET /api/v1/work-items/by-key/TRK-1': () => jsonResponse(detail),
      [`GET /api/v1/work-items/${detail.id}/history`]: () => jsonResponse({ items: [], nextCursor: null }),
    })

    await userEvent.click(await screen.findByRole('button', { name: 'Show all' }))
    await waitFor(() => expect(router.state.location.search).toBe('?done=all'))
    await waitFor(() => expect(within(screen.getByRole('list', { name: 'Done' })).getAllByRole('listitem')).toHaveLength(21))

    await userEvent.click(screen.getByRole('link', { name: 'TRK-1' }))
    await screen.findByRole('navigation', { name: 'Breadcrumb' })
    await userEvent.click(within(screen.getByRole('navigation', { name: 'Breadcrumb' })).getByRole('link', { name: 'TRK' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/TRK/board'))
    expect(router.state.location.search).toBe('?done=all')
    await waitFor(() => expect(within(screen.getByRole('list', { name: 'Done' })).getAllByRole('listitem')).toHaveLength(21))
  })

  async function createItemFromBoard() {
    await userEvent.click(await screen.findByRole('button', { name: 'New item' }))
    await userEvent.type(screen.getByLabelText('Title'), 'Made on the board')
    await userEvent.click(screen.getByRole('button', { name: 'Create item' }))
    expect(await screen.findByText('Created TRK-4 · Made on the board.')).toBeInTheDocument()
  }
  const createdOnBoard = card(4, 'todo', 'task', 'Made on the board')
  const createdDetail = { ...createdOnBoard, projectId: project.id, descriptionMd: null, parent: null, children: [], allowedTransitions: ['in_progress'], createdAt: '2026-10-26T10:00:00Z', createdBy: null }

  it('creates an item from the board and shows its card', async () => {
    let created = false
    const before = board()
    const after = { columns: before.columns.map((c) => (c.status === 'todo' ? column('todo', [createdOnBoard, ...c.items]) : c)) }
    renderBoard({
      [boardPath]: () => jsonResponse(created ? after : before),
      [`POST /api/v1/projects/${project.id}/work-items`]: () => {
        created = true
        return jsonResponse(createdDetail, 201)
      },
    })

    await createItemFromBoard()

    expect(await screen.findByRole('heading', { name: 'To Do (3)' })).toBeInTheDocument()
    expect(within(screen.getByRole('list', { name: 'To Do' })).getByRole('link', { name: 'TRK-4' })).toBeInTheDocument()
  })

  it('keeps the cards and says so when a refresh fails', async () => {
    let created = false
    renderBoard({
      [boardPath]: () => (created ? problemResponse(500, { title: 'Server error' }) : jsonResponse(board())),
      [`POST /api/v1/projects/${project.id}/work-items`]: () => {
        created = true
        return jsonResponse(createdDetail, 201)
      },
    })

    await createItemFromBoard()

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not refresh the board. The cards may be out of date.')
    expect(within(screen.getByRole('list', { name: 'To Do' })).getAllByRole('listitem')).toHaveLength(2)
  })

  it('keeps every Done card and says so when refreshing the full board fails', async () => {
    const latest = column('done', Array.from({ length: 20 }, (_, i) => card(30 - i, 'done')), 21)
    const all = column('done', [...latest.items, card(10, 'done')])
    let created = false
    renderBoard({
      [boardPath]: (request) => {
        if (created) return problemResponse(500, { title: 'Server error' })
        return jsonResponse(board(new URL(request.url).searchParams.get('allDone') === 'true' ? all : latest))
      },
      [`POST /api/v1/projects/${project.id}/work-items`]: () => {
        created = true
        return jsonResponse(createdDetail, 201)
      },
    })

    await userEvent.click(await screen.findByRole('button', { name: 'Show all' }))
    await waitFor(() => expect(within(screen.getByRole('list', { name: 'Done' })).getAllByRole('listitem')).toHaveLength(21))

    await createItemFromBoard()

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not refresh the board. The cards may be out of date.')
    expect(within(screen.getByRole('list', { name: 'Done' })).getAllByRole('listitem')).toHaveLength(21)
  })

  it('offers "Show all" only when cards are held back', async () => {
    renderBoard({ [boardPath]: () => jsonResponse(board(column('done', [card(9, 'done')]))) })

    await screen.findByRole('list', { name: 'Done' })
    expect(screen.queryByRole('button', { name: 'Show all' })).not.toBeInTheDocument()
  })

  it('switches between the Board and List tabs and marks the current one', async () => {
    const { router } = renderBoard({
      [`GET /api/v1/projects/${project.id}/work-items`]: () => jsonResponse({ items: [], nextCursor: null }),
    })

    const views = await screen.findByRole('navigation', { name: 'Project views' })
    expect(within(views).getByRole('link', { name: 'Board' })).toHaveAttribute('aria-current', 'page')

    await userEvent.click(within(views).getByRole('link', { name: 'List' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/TRK'))
    const listViews = await screen.findByRole('navigation', { name: 'Project views' })
    expect(within(listViews).getByRole('link', { name: 'List' })).toHaveAttribute('aria-current', 'page')
    expect(within(listViews).getByRole('link', { name: 'Board' })).not.toHaveAttribute('aria-current')
  })

  it('shows an error when the board cannot be loaded', async () => {
    renderBoard({ [boardPath]: () => problemResponse(500, { title: 'Server error' }) })

    expect(await screen.findByRole('alert')).toHaveTextContent('Something went wrong. Please try again.')
  })

  it('says so when the project does not exist', async () => {
    renderBoard({ 'GET /api/v1/projects/by-key/TRK': () => problemResponse(404, { title: 'Project not found' }) })

    expect(await screen.findByRole('alert')).toHaveTextContent('This project does not exist.')
  })
})
