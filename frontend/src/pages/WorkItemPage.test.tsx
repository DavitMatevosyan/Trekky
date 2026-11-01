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
  createdAt: '2026-10-20T10:00:00Z',
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
    expect((await screen.findByText('discuss')).tagName).toBe('STRONG')
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

    expect(within(form).getByLabelText('Parent key')).toHaveValue('TRK-2')
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

  it('clears the parent when its key is removed', async () => {
    const { fetchFn } = renderItem({ [patchPath]: () => withETag({ ...item, parentKey: null, parent: null }, '"8"') })
    await userEvent.click(await screen.findByRole('button', { name: 'Edit' }))
    const form = screen.getByRole('form', { name: 'Edit item' })

    await userEvent.clear(within(form).getByLabelText('Parent key'))
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

  it('closes without a request when nothing changed', async () => {
    const { fetchFn } = renderItem()
    await userEvent.click(await screen.findByRole('button', { name: 'Edit' }))

    await userEvent.click(within(screen.getByRole('form', { name: 'Edit item' })).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(screen.getByRole('button', { name: 'Edit' })).toHaveFocus())
    expect(requestsTo(fetchFn, patchPath)).toHaveLength(0)
  })
})
