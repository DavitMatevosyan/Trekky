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
    await userEvent.click(screen.getByRole('button', { name: 'Show all' }))

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
