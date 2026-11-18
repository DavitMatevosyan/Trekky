import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { fakeApi, jsonResponse, me, problemResponse, renderApp, requestsTo, validTokens } from '../test/renderApp.tsx'

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
  assignee: number === 3 ? { id: '0190a000-0000-7000-8000-0000000000aa', displayName: 'Davit', email: 'davit@example.com' } : null,
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

  it('shows who a card is assigned to, and nothing when nobody is', async () => {
    renderBoard()

    const inProgress = await screen.findByRole('list', { name: 'In Progress' })
    expect(within(inProgress).getByRole('listitem')).toHaveTextContent('Assignee: Davit')
    const toDo = within(await screen.findByRole('list', { name: 'To Do' })).getAllByRole('listitem')
    expect(toDo[0]).not.toHaveTextContent('Assignee')
  })

  it('tells keyboard and screen-reader users how to move a card', async () => {
    renderBoard()

    const link = await screen.findByRole('link', { name: 'TRK-1' })
    expect(link).toHaveAttribute('aria-roledescription', 'movable card')
    expect(link).toHaveAccessibleDescription(
      'Press Enter to open this card. To move it, press Space, use the left and right arrow keys to change column and the up and down arrow keys to change its place, then press Space to drop it or Escape to cancel.',
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
    await screen.findByText('0 items match')
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

describe('BoardPage filters', () => {
  const boardRequests = (fetchFn: typeof fetch) => requestsTo(fetchFn, boardPath).map((request) => new URL(request.url).searchParams.toString())

  function renderFilteredBoard(path: string, handlers: Parameters<typeof fakeApi>[0] = {}) {
    const fetchFn = fakeApi({
      'GET /api/v1/projects/by-key/TRK': () => jsonResponse(project),
      // Filtered: only the bug; unfiltered: the whole board.
      [boardPath]: (request) => {
        const params = new URL(request.url).searchParams
        if (!params.has('type') && !params.has('open') && !params.has('status')) return jsonResponse(board())
        return jsonResponse({
          columns: [
            column('todo', params.getAll('type').includes('bug') ? [card(1, 'todo', 'bug', 'Key not shown')] : []),
            column('in_progress', []),
            column('in_review', []),
            column('done', []),
          ],
        })
      },
      ...handlers,
    })
    return renderApp({ path, tokens: validTokens, fetchFn })
  }

  it('asks for the cards that match the URL filters and counts them against the unfiltered board', async () => {
    const { fetchFn } = renderFilteredBoard('/TRK/board?type=bug')

    await screen.findByRole('heading', { name: 'To Do (1 of 2)' })
    expect(screen.getAllByRole('heading', { level: 2 }).map((heading) => heading.textContent)).toEqual([
      'To Do (1 of 2)',
      'In Progress (0 of 1)',
      'In Review (0 of 0)',
      'Done (0 of 0)',
    ])
    expect(boardRequests(fetchFn)).toEqual(expect.arrayContaining(['type=bug', '']))
    expect(screen.getByRole('search', { name: 'Filters' })).toBeInTheDocument()
    // Read out after the change, like the list's count.
    expect(screen.getByText('1 item matches')).toHaveAttribute('aria-live', 'polite')
    expect(screen.getByRole('button', { name: 'Remove filter Type: Bug' })).toBeInTheDocument()
  })

  it('shows plain counts and asks for one board when nothing is filtered', async () => {
    const { fetchFn } = renderFilteredBoard('/TRK/board')

    await screen.findByRole('heading', { name: 'To Do (2)' })
    expect(boardRequests(fetchFn)).toEqual([''])
  })

  it('hides the Done column with Open, and keeps every column for a status filter', async () => {
    const { router } = renderFilteredBoard('/TRK/board')
    const quick = within(await screen.findByRole('group', { name: 'Quick filters' }))

    await userEvent.click(quick.getByRole('button', { name: 'Open' }))

    expect(router.state.location.search).toBe('?open=true')
    await waitFor(() =>
      expect(screen.getAllByRole('heading', { level: 2 }).map((heading) => heading.textContent)).toEqual([
        'To Do (0 of 2)',
        'In Progress (0 of 1)',
        'In Review (0 of 0)',
      ]),
    )

    await userEvent.click(quick.getByRole('button', { name: 'Open' }))
    await userEvent.click(within(screen.getByRole('search')).getByRole('button', { name: /^Status/ }))
    await userEvent.click(within(screen.getByRole('group', { name: 'Status' })).getByRole('checkbox', { name: 'Done' }))
    await waitFor(() => expect(screen.getAllByRole('heading', { level: 2 })).toHaveLength(4))
  })

  it('keeps the filters when "Show all" is asked for, and sends them with it', async () => {
    const latest = column('done', Array.from({ length: 20 }, (_, i) => card(30 - i, 'done')), 21)
    const { fetchFn, router } = renderFilteredBoard('/TRK/board?type=task', {
      [boardPath]: () => jsonResponse(board(latest)),
    })

    await userEvent.click(await screen.findByRole('button', { name: 'Show all' }))

    await waitFor(() => expect(router.state.location.search).toBe('?type=task&done=all'))
    await waitFor(() => expect(boardRequests(fetchFn)).toContain('type=task&allDone=true'))
    expect(screen.getByRole('button', { name: 'Remove filter Type: Task' })).toBeInTheDocument()
  })

  it('says when nothing matches, clears the filters from there and keeps the focus in the bar', async () => {
    const { router } = renderFilteredBoard('/TRK/board?type=epic')

    // Shown, and read out from the board's polite live region.
    const shown = await screen.findAllByText('No items match these filters.')
    expect(shown.map((element) => element.getAttribute('aria-live'))).toEqual(['polite', null])
    await userEvent.click(screen.getByRole('button', { name: 'Clear filters' }))

    expect(router.state.location.search).toBe('')
    expect(screen.getByRole('searchbox', { name: 'Search' })).toHaveFocus()
    expect(await screen.findByRole('heading', { name: 'To Do (2)' })).toBeInTheDocument()
    expect(screen.queryByText('No items match these filters.')).not.toBeInTheDocument()
  })

  it('marks the board busy while the cards for new filters load', async () => {
    let answer: (response: Response) => void = () => {}
    renderFilteredBoard('/TRK/board', {
      [boardPath]: (request) =>
        new URL(request.url).searchParams.has('type') ? new Promise<Response>((resolve) => (answer = resolve)) : jsonResponse(board()),
    })
    await screen.findByRole('heading', { name: 'To Do (2)' })

    await userEvent.click(within(screen.getByRole('search')).getByRole('button', { name: /^Type/ }))
    await userEvent.click(within(screen.getByRole('group', { name: 'Type' })).getByRole('checkbox', { name: 'Bug' }))

    await waitFor(() => expect(screen.getByRole('group', { name: 'Board columns' })).toHaveAttribute('aria-busy', 'true'))
    answer(jsonResponse(board()))
    await waitFor(() => expect(screen.getByRole('group', { name: 'Board columns' })).not.toHaveAttribute('aria-busy'))
  })

  it('keeps the filters when switching between Board and List, but not "Show all"', async () => {
    const { router } = renderFilteredBoard('/TRK/board?type=bug&done=all', {
      [`GET /api/v1/projects/${project.id}/work-items`]: () => jsonResponse({ items: [], nextCursor: null, totalCount: 0 }),
    })
    const views = await screen.findByRole('navigation', { name: 'Project views' })

    expect(within(views).getByRole('link', { name: 'List' })).toHaveAttribute('href', '/TRK?type=bug')
    await userEvent.click(within(views).getByRole('link', { name: 'List' }))

    expect(router.state.location.search).toBe('?type=bug')
    expect(await screen.findByRole('button', { name: 'Remove filter Type: Bug' })).toBeInTheDocument()
    expect(within(screen.getByRole('navigation', { name: 'Project views' })).getByRole('link', { name: 'Board' })).toHaveAttribute(
      'href',
      '/TRK/board?type=bug',
    )
  })
})

describe('BoardPage roles (TREK-46)', () => {
  it('shows a viewer the cards as plain links: no "New item", nothing to pick up', async () => {
    renderBoard({ 'GET /api/v1/me': () => jsonResponse({ ...me, role: 'viewer' }) })

    // The signed-in user's email in the header: the role is known.
    await screen.findByText(me.user.email)
    const link = await screen.findByRole('link', { name: 'TRK-1' })
    expect(link).not.toHaveAttribute('aria-roledescription')
    expect(link).not.toHaveAccessibleDescription()
    expect(link.closest('li')).not.toHaveClass('cursor-grab')
    expect(screen.queryByRole('button', { name: 'New item' })).not.toBeInTheDocument()
  })

  it('lets a member create items and move cards', async () => {
    renderBoard({ 'GET /api/v1/me': () => jsonResponse({ ...me, role: 'member' }) })

    expect(await screen.findByRole('button', { name: 'New item' })).toBeInTheDocument()
    expect(await screen.findByRole('link', { name: 'TRK-1' })).toHaveAttribute('aria-roledescription', 'movable card')
  })
})
