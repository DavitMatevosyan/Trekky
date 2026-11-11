import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { fakeApi, jsonResponse, problemResponse, renderApp, requestsTo, validTokens } from '../test/renderApp.tsx'

const summary = (key: string, type: string, title: string, status = 'todo', parentKey: string | null = null) => ({
  id: `0190a000-0000-7000-8000-00000000${key.replace('TRK-', '').padStart(4, '0')}`,
  key,
  type,
  title,
  status,
  parentKey,
  updatedAt: '2026-10-26T10:00:00Z',
})

const epic = summary('TRK-2', 'epic', 'v1.0 Dogfood', 'in_progress')
const item = {
  ...summary('TRK-14', 'story', 'Comments on items', 'in_progress', 'TRK-2'),
  projectId: '0190a000-0000-7000-8000-0000000000b1',
  descriptionMd: 'Members can **discuss** an item.\n\n[bad](javascript:alert(1)) ![tracker](https://example.com/pixel.png)',
  parent: epic,
  children: [summary('TRK-15', 'task', 'Edit own comments', 'todo', 'TRK-14'), summary('TRK-16', 'task', 'Delete own comments', 'done', 'TRK-14')],
  allowedTransitions: ['todo', 'in_review'],
  createdAt: '2026-10-20T10:00:00Z',
  createdBy: { id: '0190a000-0000-7000-8000-0000000000aa', displayName: 'Davit', email: 'davit@example.com' },
}

const itemPath = 'GET /api/v1/work-items/by-key/TRK-14'
const patchPath = `PATCH /api/v1/work-items/${item.id}`

function withETag(body: unknown, etag: string, status = 200) {
  const response = jsonResponse(body, status)
  response.headers.set('ETag', etag)
  return response
}

const historyPath = `GET /api/v1/work-items/${item.id}/history`
const commentsPath = `GET /api/v1/work-items/${item.id}/comments`

function historyEvent(n: number, eventType: string, payload: unknown, occurredAt = item.updatedAt, displayName: string | null = 'Davit') {
  return {
    eventId: `0190a000-0000-7000-8000-00000000e${String(n).padStart(3, '0')}`,
    eventType,
    schemaVersion: 1,
    occurredAt,
    actor: { type: 'user', id: '0190a000-0000-7000-8000-0000000000aa', displayName },
    entity: { type: 'work_item', id: item.id, key: item.key },
    payload,
  }
}

const createdEvent = historyEvent(1, 'WorkItemCreated', { type: 'story', title: 'Comments on items', status: 'todo' })

function renderItem(handlers: Parameters<typeof fakeApi>[0] = {}, path = '/TRK/items/TRK-14') {
  const fetchFn = fakeApi({
    [itemPath]: () => withETag(item, '"7"'),
    [historyPath]: () => jsonResponse({ items: [createdEvent], nextCursor: null }),
    [commentsPath]: () => jsonResponse({ items: [], nextCursor: null }),
    ...handlers,
  })
  return { ...renderApp({ path, tokens: validTokens, fetchFn }), fetchFn }
}

describe('WorkItemPage', () => {
  it('shows the key, type, title, status, rendered description, parent and children', async () => {
    renderItem()

    const heading = await screen.findByRole('heading', { level: 1 })
    expect(heading).toHaveTextContent('StoryComments on items')
    expect(within(screen.getByRole('navigation', { name: 'Breadcrumb' })).getByText('TRK-14')).toHaveAttribute('aria-current', 'page')
    // The Markdown renderer loads lazily; give it time when the whole suite runs in parallel.
    expect((await screen.findByText('discuss', {}, { timeout: 5000 })).tagName).toBe('STRONG')
    expect(screen.getByText('bad')).not.toHaveAttribute('href', expect.stringContaining('javascript'))
    // Images are links, never loaded.
    expect(screen.queryByRole('img')).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'tracker' })).toHaveAttribute('href', 'https://example.com/pixel.png')
    expect(screen.getByRole('link', { name: 'TRK-2 Epic' })).toHaveAttribute('href', '/TRK/items/TRK-2')
    const children = within(screen.getByRole('list', { name: 'Children' })).getAllByRole('listitem')
    expect(children.map((li) => li.textContent)).toEqual(['TRK-15TaskEdit own commentsTo Do', 'TRK-16TaskDelete own commentsDone'])
    expect(screen.getByText('In Progress')).toBeInTheDocument()
  })

  it('says so when the item does not exist, or belongs to another project', async () => {
    const missing = renderItem({ [itemPath]: () => problemResponse(404, { title: 'Work item not found' }) })
    expect(await screen.findByRole('alert')).toHaveTextContent('This item does not exist.')
    missing.unmount()

    renderItem({}, '/WEB/items/TRK-14')
    expect(await screen.findByRole('alert')).toHaveTextContent('This item does not exist.')
  })

  it('saves only the changed fields as a merge patch with If-Match', async () => {
    const { fetchFn } = renderItem({ [patchPath]: () => withETag({ ...item, title: 'Comments' }, '"8"') })
    await userEvent.click(await screen.findByRole('button', { name: 'Edit' }))
    const form = screen.getByRole('form', { name: 'Edit item' })

    expect(within(form).getByRole('combobox', { name: 'Parent' })).toHaveValue('TRK-2 · v1.0 Dogfood')
    const title = within(form).getByLabelText('Title')
    await userEvent.clear(title)
    await userEvent.type(title, 'Comments')
    await userEvent.click(within(form).getByRole('button', { name: 'Save' }))

    expect(await screen.findByRole('heading', { level: 1 })).toHaveTextContent('Comments')
    await waitFor(() => expect(screen.queryByRole('form', { name: 'Edit item' })).not.toBeInTheDocument())
    expect(screen.getByRole('status')).toHaveTextContent('Saved TRK-14.')
    const [patch] = requestsTo(fetchFn, patchPath)
    expect(patch?.headers.get('If-Match')).toBe('"7"')
    expect(patch?.headers.get('Content-Type')).toBe('application/merge-patch+json')
    expect(await patch?.json()).toEqual({ title: 'Comments' })
  })

  it('never offers the item as its own parent', async () => {
    const listPath = `GET /api/v1/projects/${item.projectId}/work-items`
    renderItem({ [listPath]: () => jsonResponse({ items: [summary('TRK-14', 'story', 'Comments on items'), epic], nextCursor: null }) })
    await userEvent.click(await screen.findByRole('button', { name: 'Edit' }))
    const form = screen.getByRole('form', { name: 'Edit item' })

    await userEvent.click(within(form).getByRole('button', { name: 'Clear parent' }))
    await userEvent.click(within(form).getByRole('combobox', { name: 'Parent' }))

    const options = await within(await within(form).findByRole('listbox')).findAllByRole('option')
    expect(options.map((option) => option.textContent)).toEqual(['TRK-2Epicv1.0 Dogfood'])
  })

  it('clears the parent with the clear button', async () => {
    const { fetchFn } = renderItem({ [patchPath]: () => withETag({ ...item, parentKey: null, parent: null }, '"8"') })
    await userEvent.click(await screen.findByRole('button', { name: 'Edit' }))
    const form = screen.getByRole('form', { name: 'Edit item' })

    await userEvent.click(within(form).getByRole('button', { name: 'Clear parent' }))
    await userEvent.click(within(form).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(requestsTo(fetchFn, patchPath)).toHaveLength(1))
    expect(await requestsTo(fetchFn, patchPath)[0]?.json()).toEqual({ parentId: null })
  })

  it('shows the latest version when someone else changed the item', async () => {
    let version = 7
    const { fetchFn } = renderItem({
      [itemPath]: () => withETag(version === 7 ? item : { ...item, title: 'Renamed elsewhere' }, `"${version}"`),
      [patchPath]: () => {
        version = 9
        return problemResponse(412, { title: 'Changed by someone else', detail: 'This item was changed by someone else.' })
      },
    })
    await userEvent.click(await screen.findByRole('button', { name: 'Edit' }))
    const form = screen.getByRole('form', { name: 'Edit item' })

    await userEvent.type(within(form).getByLabelText('Title'), ' (mine)')
    await userEvent.click(within(form).getByRole('button', { name: 'Save' }))

    expect(await screen.findByText('This item was changed by someone else. The latest version is shown; make your change again.')).toBeInTheDocument()
    await waitFor(() => expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Renamed elsewhere'))
    await waitFor(() => expect(screen.getByLabelText('Title')).toHaveValue('Renamed elsewhere'))
    expect(requestsTo(fetchFn, itemPath).length).toBeGreaterThanOrEqual(2)
  })

  it('shows children that no longer fit a new type on the type field', async () => {
    renderItem({
      [patchPath]: () =>
        problemResponse(422, {
          title: 'Children do not fit the new type',
          detail: 'A Bug cannot be the parent of TRK-15, TRK-16. Move them first.',
          allowedParentTypes: ['epic', 'story'],
          conflictingChildKeys: ['TRK-15', 'TRK-16'],
        }),
    })
    await userEvent.click(await screen.findByRole('button', { name: 'Edit' }))
    const form = screen.getByRole('form', { name: 'Edit item' })

    await userEvent.selectOptions(within(form).getByLabelText('Type'), 'bug')
    await userEvent.click(within(form).getByRole('button', { name: 'Save' }))

    expect(await within(form).findByText('A Bug cannot be the parent of TRK-15, TRK-16. Move them first.')).toBeInTheDocument()
    expect(within(form).getByLabelText('Type')).toHaveAttribute('aria-invalid', 'true')
  })

  describe('status', () => {
    const statusPath = `POST /api/v1/work-items/${item.id}/status`
    const statusSelect = () => screen.findByRole('combobox', { name: 'Status' })

    it('offers only the current status and the allowed moves, in workflow order', async () => {
      renderItem()

      const options = within(await statusSelect()).getAllByRole('option')
      expect(options.map((option) => option.textContent)).toEqual(['To Do', 'In Progress', 'In Review'])
      expect(await statusSelect()).toHaveValue('in_progress')
    })

    it('moves the item with If-Match and announces it', async () => {
      const { fetchFn } = renderItem({
        [statusPath]: () => withETag({ ...item, status: 'in_review', allowedTransitions: ['todo', 'in_progress', 'done'] }, '"8"'),
      })

      await userEvent.selectOptions(await statusSelect(), 'in_review')

      await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('TRK-14 moved to In Review.'))
      const select = await statusSelect()
      expect(select).toHaveValue('in_review')
      expect(within(select).getAllByRole('option').map((option) => option.textContent)).toEqual(['To Do', 'In Progress', 'In Review', 'Done'])
      const [move] = requestsTo(fetchFn, statusPath)
      expect(move?.headers.get('If-Match')).toBe('"7"')
      expect(await move?.json()).toEqual({ to: 'in_review' })
    })

    it('shows the latest status when someone else changed the item', async () => {
      let version = 7
      const { fetchFn } = renderItem({
        [itemPath]: () => withETag(version === 7 ? item : { ...item, status: 'todo', allowedTransitions: ['in_progress'] }, `"${version}"`),
        [statusPath]: () => {
          version = 9
          return problemResponse(412, { title: 'Changed by someone else' })
        },
      })

      await userEvent.selectOptions(await statusSelect(), 'in_review')

      expect(await screen.findByRole('alert')).toHaveTextContent(
        'This item was changed by someone else. Its latest status is shown; change it again.',
      )
      await waitFor(async () => expect(await statusSelect()).toHaveValue('todo'))
      expect(requestsTo(fetchFn, itemPath).length).toBeGreaterThanOrEqual(2)
    })

    it('says where the item can go when the move is not allowed', async () => {
      renderItem({
        [statusPath]: () =>
          problemResponse(422, { title: 'Status change not allowed', allowedTransitions: ['todo', 'in_review'] }),
      })

      await userEvent.selectOptions(await statusSelect(), 'todo')

      expect(await screen.findByRole('alert')).toHaveTextContent('This item can only move to To Do or In Review.')
      expect(await statusSelect()).toHaveValue('in_progress')
      expect(await statusSelect()).toHaveAttribute('aria-invalid', 'true')

      // Editing the item is other work: the failed move's message goes.
      await userEvent.click(screen.getByRole('button', { name: 'Edit' }))
      expect(screen.queryByText('This item can only move to To Do or In Review.')).not.toBeInTheDocument()
      expect(await statusSelect()).not.toHaveAttribute('aria-invalid')
    })

    it('cannot be changed while the item is being edited', async () => {
      renderItem()
      await userEvent.click(await screen.findByRole('button', { name: 'Edit' }))

      expect(await statusSelect()).toBeDisabled()
      await userEvent.click(within(screen.getByRole('form', { name: 'Edit item' })).getByRole('button', { name: 'Cancel' }))
      expect(await statusSelect()).toBeEnabled()
    })
  })

  describe('history', () => {
    const historyRows = async () => within(await screen.findByRole('list', { name: 'History' })).getAllByRole('listitem')

    it('shows who changed what and when, newest first', async () => {
      renderItem({
        [historyPath]: () =>
          jsonResponse({
            items: [historyEvent(2, 'WorkItemStatusChanged', { from: 'todo', to: 'in_progress' }, item.updatedAt, null), createdEvent],
            nextCursor: null,
          }),
      })

      const rows = await historyRows()

      expect(rows).toHaveLength(2)
      expect(rows[0]).toHaveTextContent('A former member changed status To Do → In Progress')
      expect(rows[1]).toHaveTextContent('Davit created this Story')
      expect(rows[1]?.querySelector('time')).toHaveAttribute('datetime', item.updatedAt)
    })

    it('loads older events with the cursor', async () => {
      const older = historyEvent(0, 'WorkItemUpdated', { changes: { title: { from: 'A', to: 'B' } } })
      const { fetchFn } = renderItem({
        [historyPath]: (request) =>
          new URL(request.url).searchParams.get('cursor') === 'page-2'
            ? jsonResponse({ items: [older], nextCursor: null })
            : jsonResponse({ items: [createdEvent], nextCursor: 'page-2' }),
      })

      await userEvent.click(await screen.findByRole('button', { name: 'Load more' }))

      await waitFor(async () => expect(await historyRows()).toHaveLength(2))
      expect((await historyRows())[1]).toHaveTextContent('Davit changed title “A” → “B”')
      expect(screen.queryByRole('button', { name: 'Load more' })).not.toBeInTheDocument()
      expect(requestsTo(fetchFn, historyPath)).toHaveLength(2)
    })

    it('shows a new change once its event has arrived', async () => {
      const movedAt = '2026-10-26T11:00:00Z'
      const moved = { ...item, status: 'in_review', updatedAt: movedAt, allowedTransitions: ['todo', 'in_progress', 'done'] }
      const statusChanged = historyEvent(2, 'WorkItemStatusChanged', { from: 'in_progress', to: 'in_review' }, movedAt)
      let moves = 0
      let readsAfterMove = 0
      renderItem({
        [`POST /api/v1/work-items/${item.id}/status`]: () => {
          moves += 1
          return withETag(moved, '"8"')
        },
        [historyPath]: () => {
          if (moves > 0) readsAfterMove += 1
          // The first read after the move is too early: the event is still in the outbox.
          return jsonResponse({ items: readsAfterMove > 1 ? [statusChanged, createdEvent] : [createdEvent], nextCursor: null })
        },
      })

      await userEvent.selectOptions(await screen.findByRole('combobox', { name: 'Status' }), 'in_review')

      await waitFor(async () => expect((await historyRows())[0]).toHaveTextContent('Davit changed status In Progress → In Review'), { timeout: 3000 })
      expect(await historyRows()).toHaveLength(2)
    })

    it('goes back to the newest events after a change', async () => {
      const movedAt = '2026-10-26T11:00:00Z'
      const older = historyEvent(0, 'WorkItemUpdated', { changes: { title: { from: 'A', to: 'B' } } })
      const statusChanged = historyEvent(2, 'WorkItemStatusChanged', { from: 'in_progress', to: 'in_review' }, movedAt)
      let moved = false
      renderItem({
        [`POST /api/v1/work-items/${item.id}/status`]: () => {
          moved = true
          return withETag({ ...item, status: 'in_review', updatedAt: movedAt, allowedTransitions: ['todo', 'in_progress', 'done'] }, '"8"')
        },
        [historyPath]: (request) => {
          if (moved) return jsonResponse({ items: [statusChanged, createdEvent], nextCursor: 'page-2' })
          return new URL(request.url).searchParams.get('cursor') === 'page-2'
            ? jsonResponse({ items: [older], nextCursor: null })
            : jsonResponse({ items: [createdEvent], nextCursor: 'page-2' })
        },
      })
      await userEvent.click(await screen.findByRole('button', { name: 'Load more' }))
      await waitFor(async () => expect((await historyRows())[1]).toHaveTextContent('changed title'))

      await userEvent.selectOptions(await screen.findByRole('combobox', { name: 'Status' }), 'in_review')

      await waitFor(async () => expect((await historyRows())[0]).toHaveTextContent('changed status In Progress → In Review'))
      expect((await historyRows()).map((row) => row.textContent)).not.toContainEqual(expect.stringContaining('changed title'))
    })

    it('recovers by itself when a read fails', async () => {
      let reads = 0
      renderItem({
        [historyPath]: () => {
          reads += 1
          return reads === 1 ? problemResponse(500, { title: 'Server error' }) : jsonResponse({ items: [createdEvent], nextCursor: null })
        },
      })

      expect(await screen.findByRole('alert')).toHaveTextContent('Could not load the history. Please try again.')

      await waitFor(async () => expect((await historyRows())[0]).toHaveTextContent('Davit created this Story'), { timeout: 3000 })
      expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    })

    it('keeps one steady alert while every read fails, then stops', async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true })
      try {
        const { fetchFn } = renderItem({ [historyPath]: () => problemResponse(500, { title: 'Server error' }) })
        const alert = await screen.findByRole('alert')

        for (let tick = 0; tick < 40; tick += 1) {
          await vi.advanceTimersByTimeAsync(250)
          expect(screen.getByRole('alert')).toBe(alert)
          expect(screen.queryByText('Loading history…')).not.toBeInTheDocument()
          expect(screen.queryByRole('list', { name: 'History' })).not.toBeInTheDocument()
        }

        expect(requestsTo(fetchFn, historyPath)).toHaveLength(10)
      } finally {
        vi.useRealTimers()
      }
    })

    it('stops checking when a change never gets an event', async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true })
      try {
        const { fetchFn } = renderItem({ [historyPath]: () => jsonResponse({ items: [], nextCursor: null }) })
        expect(await screen.findByText('No history yet.')).toBeInTheDocument()

        await vi.advanceTimersByTimeAsync(20_000)

        expect(requestsTo(fetchFn, historyPath)).toHaveLength(10)
      } finally {
        vi.useRealTimers()
      }
    })

    it('keeps the rest of the page when the history cannot load', async () => {
      renderItem({ [historyPath]: () => problemResponse(500, { title: 'Server error' }) })

      expect(await screen.findByRole('alert')).toHaveTextContent('Could not load the history. Please try again.')
      expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Comments on items')
    })
  })

  describe('comments', () => {
    const postPath = `POST /api/v1/work-items/${item.id}/comments`
    const comment = (n: number, bodyMd: string, author: { id: string; displayName: string; email: string | null } | null = item.createdBy) => ({
      id: `0190a000-0000-7000-8000-00000000c${String(n).padStart(3, '0')}`,
      workItemId: item.id,
      authorId: '0190a000-0000-7000-8000-0000000000aa',
      author,
      bodyMd,
      createdAt: `2026-10-2${n}T10:00:00Z`,
      edited: false,
    })
    const commentRows = async () => within(await screen.findByRole('list', { name: 'Comments' })).getAllByRole('listitem')

    it('shows the comments oldest first with author, time and Markdown', async () => {
      renderItem({
        [commentsPath]: () =>
          jsonResponse({ items: [comment(1, 'Keep it **flat**.'), comment(2, 'Agreed.', null)], nextCursor: null }),
      })

      const rows = await commentRows()
      expect(rows).toHaveLength(2)
      expect(rows[0]).toHaveTextContent('Davit')
      expect(within(rows[0]!).getByText('flat').tagName).toBe('STRONG')
      expect(within(rows[0]!).getByRole('time')).toBeInTheDocument()
      expect(rows[1]).toHaveTextContent('A former member')
      expect(rows[1]).toHaveTextContent('Agreed.')
    })

    it('says when there are no comments yet', async () => {
      renderItem()
      expect(await screen.findByText('No comments yet.')).toBeInTheDocument()
    })

    it('loads more comments after the first page', async () => {
      renderItem({
        [commentsPath]: (request) =>
          new URL(request.url).searchParams.get('cursor') === 'next'
            ? jsonResponse({ items: [comment(3, 'Third')], nextCursor: null })
            : jsonResponse({ items: [comment(1, 'First'), comment(2, 'Second')], nextCursor: 'next' }),
      })
      expect(await commentRows()).toHaveLength(2)

      await userEvent.click(screen.getByRole('button', { name: 'Show more comments' }))

      await waitFor(async () => expect(await commentRows()).toHaveLength(3))
      expect((await commentRows())[2]).toHaveTextContent('Third')
      expect(screen.queryByRole('button', { name: 'Show more comments' })).not.toBeInTheDocument()
    })

    it('posts a comment, clears the box, shows it and announces it', async () => {
      let posted: ReturnType<typeof comment> | null = null
      const { fetchFn } = renderItem({
        [commentsPath]: () => jsonResponse({ items: posted ? [posted] : [], nextCursor: null }),
        [postPath]: () => {
          posted = comment(5, 'Ship it.')
          return jsonResponse(posted, 201)
        },
      })
      const box = await screen.findByLabelText('Add a comment')
      expect(box).toHaveAccessibleDescription('Markdown is supported.')

      await userEvent.type(box, 'Ship it.')
      await userEvent.click(screen.getByRole('button', { name: 'Post comment' }))

      expect(await commentRows()).toHaveLength(1)
      expect(box).toHaveValue('')
      await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('Comment posted.'))
      expect(await requestsTo(fetchFn, postPath)[0]?.json()).toEqual({ bodyMd: 'Ship it.' })
    })

    it('asks for some text without sending an empty comment', async () => {
      const { fetchFn } = renderItem()
      const box = await screen.findByLabelText('Add a comment')

      await userEvent.type(box, '   ')
      await userEvent.click(screen.getByRole('button', { name: 'Post comment' }))

      expect(box).toHaveAccessibleDescription('Markdown is supported. Write something to comment.')
      expect(box).toHaveFocus()
      expect(requestsTo(fetchFn, postPath)).toHaveLength(0)
    })

    it('shows the field error the API sends, and keeps the text', async () => {
      renderItem({ [postPath]: () => problemResponse(400, { title: 'Invalid', errors: { bodyMd: ['A comment can be at most 20,000 characters.'] } }) })
      const box = await screen.findByLabelText('Add a comment')

      await userEvent.type(box, 'Too long, says the server')
      await userEvent.click(screen.getByRole('button', { name: 'Post comment' }))

      await waitFor(() => expect(box).toHaveAccessibleDescription('Markdown is supported. A comment can be at most 20,000 characters.'))
      expect(box).toHaveValue('Too long, says the server')
    })

    it('says so when posting fails, and keeps the text', async () => {
      renderItem({ [postPath]: () => problemResponse(500, { title: 'Server error' }) })
      const box = await screen.findByLabelText('Add a comment')

      await userEvent.type(box, 'Hello')
      await userEvent.click(screen.getByRole('button', { name: 'Post comment' }))

      expect(await screen.findByRole('alert')).toHaveTextContent('Could not post the comment. Please try again.')
      expect(box).toHaveValue('Hello')
    })

    it('keeps the rest of the page when the comments cannot load', async () => {
      renderItem({ [commentsPath]: () => problemResponse(500, { title: 'Server error' }) })

      expect(await screen.findByRole('alert')).toHaveTextContent('Could not load the comments. Please try again.')
      expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Comments on items')
    })
  })

  it('closes without a request when nothing changed', async () => {
    const { fetchFn } = renderItem()
    await userEvent.click(await screen.findByRole('button', { name: 'Edit' }))

    await userEvent.click(within(screen.getByRole('form', { name: 'Edit item' })).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(screen.getByRole('button', { name: 'Edit' })).toHaveFocus())
    expect(requestsTo(fetchFn, patchPath)).toHaveLength(0)
  })
})
