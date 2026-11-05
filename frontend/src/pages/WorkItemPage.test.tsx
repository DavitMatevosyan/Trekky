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

function renderItem(handlers: Parameters<typeof fakeApi>[0] = {}, path = '/TRK/items/TRK-14') {
  const fetchFn = fakeApi({ [itemPath]: () => withETag(item, '"7"'), ...handlers })
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

  it('closes without a request when nothing changed', async () => {
    const { fetchFn } = renderItem()
    await userEvent.click(await screen.findByRole('button', { name: 'Edit' }))

    await userEvent.click(within(screen.getByRole('form', { name: 'Edit item' })).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(screen.getByRole('button', { name: 'Edit' })).toHaveFocus())
    expect(requestsTo(fetchFn, patchPath)).toHaveLength(0)
  })
})
