import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { fakeApi, jsonResponse, me, problemResponse, renderApp, requestsTo, validTokens } from '../test/renderApp.tsx'

vi.mock('mermaid', () => ({
  default: {
    initialize: vi.fn(),
    render: vi.fn(async (_id: string, source: string) =>
      source.includes('broken')
        ? Promise.reject(new Error('Parse error'))
        : { svg: `<svg data-testid="mermaid-svg"><text>${source.includes('A-->B') ? 'A to B' : ''}</text></svg>` },
    ),
  },
}))

const project = { id: '0190a000-0000-7000-8000-0000000000b1', key: 'TRK', name: 'Trekky', itemCount: 0, createdAt: '2026-10-26T10:00:00Z' }
const docsPath = `/api/v1/projects/${project.id}/docs`
const commit = {
  sha: 'abc123',
  message: 'Write the handbook',
  author: 'Ada Lovelace',
  committedAt: new Date(Date.now() - 2 * 3600_000).toISOString(),
  htmlUrl: 'https://github.com/acme/handbook/commit/abc123',
}
const tree = {
  projectId: project.id,
  repository: 'acme/handbook',
  branch: 'dev',
  docsPath: 'docs/',
  htmlUrl: 'https://github.com/acme/handbook/tree/dev/docs',
  lastCommit: commit,
  checkedAt: '2026-11-24T10:00:00Z',
  stale: false,
  folderMissing: false,
  truncated: false,
  files: [
    { path: 'README.md', title: 'Handbook', size: 100, tooLarge: false },
    { path: 'guide/setup.md', title: 'Setting up', size: 100, tooLarge: false },
  ],
}

const readme = [
  '# Handbook',
  '',
  'Read [setup](guide/setup.md#install), the [code](../src/app.ts) or [elsewhere](https://example.com/).',
  '',
  '| Name | Value |',
  '| --- | --- |',
  '| a | 1 |',
  '',
  '- [x] done',
  '- [ ] todo',
  '',
  '<script>window.hacked = true</script>',
  '<img src="x" onerror="window.hacked = true">',
  '<a href="javascript:alert(1)">bad link</a>',
  '',
  '```ts',
  'const answer = 42',
  '```',
  '',
  '```mermaid',
  'graph TD; A-->B',
  '```',
  '',
  '![Flow](images/flow.png) ![Remote](https://example.com/tracker.png)',
  '',
  '<picture><source srcset="https://tracker.example/pixel.png"><img src="images/flow.png" alt="Pictured"></picture>',
  '',
  'A claim.[^1]',
  '',
  '[^1]: The source.',
].join('\n')

function doc(path: string, content: string | null, extra: Record<string, unknown> = {}) {
  return {
    path,
    title: path === 'README.md' ? 'Handbook' : 'Setting up',
    content,
    blobSha: 'sha',
    size: content?.length ?? 0,
    tooLarge: false,
    lastCommit: commit,
    htmlUrl: `https://github.com/acme/handbook/blob/dev/docs/${path}`,
    stale: false,
    ...extra,
  }
}

const docs: Record<string, string> = {
  'README.md': readme,
  'guide/setup.md': '# Setting up\n\n## Install\n\nRun it. Back to [the handbook](../README.md).\n',
}

function renderDocs(path = '/TRK/docs', handlers: Parameters<typeof fakeApi>[0] = {}, role = me.role) {
  const fetchFn = fakeApi({
    'GET /api/v1/me': () => jsonResponse({ ...me, role }),
    'GET /api/v1/projects/by-key/TRK': () => jsonResponse(project),
    [`GET ${docsPath}`]: () => jsonResponse(tree),
    [`GET ${docsPath}/file`]: (request) => {
      const file = new URL(request.url).searchParams.get('path') ?? ''
      const content = docs[file]
      return content === undefined ? problemResponse(404, { title: 'Doc not found' }) : jsonResponse(doc(file, content))
    },
    [`GET ${docsPath}/image`]: () => new Response(new Uint8Array([137, 80, 78, 71]), { status: 200, headers: { 'Content-Type': 'image/png' } }),
    ...handlers,
  })
  const view = renderApp({ path, tokens: validTokens, fetchFn })
  return { ...view, fetchFn }
}

describe('DocsPage', () => {
  // The route loads the page lazily; load it once up front so the first test doesn't spend findBy's wait compiling it.
  beforeAll(async () => {
    await import('./DocsPage.tsx')
  }, 30_000)

  it('shows the folder tree and opens the README by default, with its last change and a GitHub link', async () => {
    renderDocs()

    const article = await screen.findByRole('article')
    expect(within(article).getByRole('heading', { level: 1, name: 'Handbook' })).toBeInTheDocument()
    const files = screen.getByRole('navigation', { name: 'Docs' })
    expect(within(files).getByRole('link', { name: 'Handbook' })).toHaveAttribute('aria-current', 'page')
    expect(within(files).getByRole('link', { name: 'Setting up' })).toHaveAttribute('href', '/TRK/docs/guide/setup.md')
    expect(within(article).getByText(/Last changed by Ada Lovelace,/)).toBeInTheDocument()
    expect(within(article).getByRole('link', { name: '2 hours ago' })).toHaveAttribute('href', commit.htmlUrl)
    expect(within(article).getByRole('link', { name: 'Open on GitHub' })).toHaveAttribute('href', 'https://github.com/acme/handbook/blob/dev/docs/README.md')
    expect(screen.getByText('acme/handbook · dev · docs/')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Docs', current: 'page' })).toBeInTheDocument()
  })

  it('renders GitHub-flavoured Markdown, highlighted code and Mermaid, and drops unsafe HTML', async () => {
    renderDocs()

    const article = await screen.findByRole('article')
    expect(within(article).getByRole('table')).toBeInTheDocument()
    expect(within(article).getAllByRole('checkbox')).toHaveLength(2)
    expect(within(article).getAllByRole('checkbox')[0]).toBeChecked()
    expect(article.querySelector('code.hljs .hljs-keyword')?.textContent).toBe('const')
    expect(await within(article).findByTestId('mermaid-svg')).toHaveTextContent('A to B')

    expect(article.querySelector('script')).toBeNull()
    expect(article.querySelector('[onerror]')).toBeNull()
    expect(within(article).queryByRole('link', { name: 'bad link' })).toBeNull()
    expect((window as unknown as { hacked?: boolean }).hacked).toBeUndefined()
  })

  it('opens relative doc links inside Trekky and other files on GitHub', async () => {
    renderDocs()

    const article = await screen.findByRole('article')
    expect(within(article).getByRole('link', { name: 'setup' })).toHaveAttribute('href', '/TRK/docs/guide/setup.md#install')
    expect(within(article).getByRole('link', { name: 'code' })).toHaveAttribute('href', 'https://github.com/acme/handbook/blob/dev/src/app.ts')
    expect(within(article).getByRole('link', { name: 'elsewhere' })).toHaveAttribute('href', 'https://example.com/')
  })

  it('loads images from the docs folder through the API and never loads remote ones', async () => {
    const { fetchFn } = renderDocs()

    const article = await screen.findByRole('article')
    const image = await within(article).findByRole('img', { name: 'Flow' })
    expect(image.getAttribute('src')).toMatch(/^data:image\/png;base64,/)
    const [request] = requestsTo(fetchFn, `GET ${docsPath}/image`)
    expect(new URL(request?.url ?? 'http://x').searchParams.get('path')).toBe('images/flow.png')
    expect(request?.headers.get('Authorization')).toBe(`Bearer ${validTokens.accessToken}`)
    expect(within(article).queryByRole('img', { name: 'Remote' })).toBeNull()
    expect(within(article).getByRole('link', { name: 'Remote' })).toHaveAttribute('href', 'https://example.com/tracker.png')

    // A <picture> keeps its <img> (loaded through the API), but its <source srcset> is dropped, so nothing remote loads.
    expect(await within(article).findByRole('img', { name: 'Pictured' })).toBeInTheDocument()
    expect(article.querySelector('source')).toBeNull()
    expect(article.innerHTML).not.toContain('tracker.example')
  })

  it('jumps to footnotes, whose ids carry the sanitiser prefix', async () => {
    renderDocs()

    const article = await screen.findByRole('article')
    const footnoteLink = within(article).getByRole('link', { name: '1' })
    const target = document.getElementById(`user-content-${footnoteLink.getAttribute('href')?.slice(1) ?? ''}`)
    expect(target).not.toBeNull()
    const scrolled = vi.fn()
    if (target) target.scrollIntoView = scrolled
    await userEvent.click(footnoteLink)

    expect(scrolled).toHaveBeenCalled()
  })

  it('survives a link hash that cannot be decoded', async () => {
    renderDocs('/TRK/docs/README.md#%E0%A4%A')

    expect(await screen.findByRole('heading', { level: 1, name: 'Handbook' })).toBeInTheDocument()
  })

  it('opens a doc from the tree, with its breadcrumb', async () => {
    renderDocs()

    await userEvent.click(await screen.findByRole('link', { name: 'Setting up' }))

    const article = await screen.findByRole('article')
    expect(await within(article).findByRole('heading', { level: 1, name: 'Setting up' })).toBeInTheDocument()
    const crumbs = within(article).getByRole('navigation', { name: 'Breadcrumb' })
    expect(within(crumbs).getByRole('link', { name: 'Docs' })).toHaveAttribute('href', '/TRK/docs')
    expect(within(crumbs).getByText('setup.md')).toHaveAttribute('aria-current', 'page')
    expect(within(article).getByRole('heading', { level: 2, name: 'Install' })).toHaveAttribute('id', 'install')
    expect(within(article).getByRole('link', { name: 'the handbook' })).toHaveAttribute('href', '/TRK/docs/README.md')
  })

  it('searches the docs and marks the matched words', async () => {
    const { fetchFn } = renderDocs('/TRK/docs', {
      [`GET ${docsPath}/search`]: () =>
        jsonResponse({
          query: 'dock',
          incomplete: false,
          results: [{ path: 'guide/setup.md', title: 'Setting up', snippet: [{ text: 'Run ', match: false }, { text: 'Docker', match: true }] }],
        }),
    })

    await userEvent.type(await screen.findByRole('searchbox', { name: 'Search docs' }), 'dock')

    expect(await screen.findByText('1 doc found')).toBeInTheDocument()
    const results = screen.getByRole('region', { name: 'Search docs' })
    expect(within(results).getByRole('link', { name: 'Setting up' })).toHaveAttribute('href', '/TRK/docs/guide/setup.md')
    expect(within(results).getByText('Docker').tagName).toBe('MARK')
    await waitFor(() => expect(requestsTo(fetchFn, `GET ${docsPath}/search`).length).toBeGreaterThan(0))
    const lastSearch = requestsTo(fetchFn, `GET ${docsPath}/search`).at(-1)
    expect(new URL(lastSearch?.url ?? 'http://x').searchParams.get('q')).toBe('dock')
  })

  it('refreshes from GitHub on request and announces it', async () => {
    const { fetchFn } = renderDocs()

    await userEvent.click(await screen.findByRole('button', { name: 'Refresh' }))

    expect(await screen.findByText('Docs refreshed from GitHub.')).toBeInTheDocument()
    const refreshes = requestsTo(fetchFn, `GET ${docsPath}`).filter((r) => new URL(r.url).searchParams.get('refresh') === 'true')
    expect(refreshes).toHaveLength(1)
  })

  it('says when the copy is stale, a doc is too large or missing, or a diagram cannot be drawn', async () => {
    renderDocs('/TRK/docs/README.md', {
      [`GET ${docsPath}`]: () => jsonResponse({ ...tree, stale: true }),
      [`GET ${docsPath}/file`]: (request) => {
        const file = new URL(request.url).searchParams.get('path')
        if (file === 'README.md') return jsonResponse(doc('README.md', '```mermaid\nbroken\n```'))
        return jsonResponse(doc('guide/setup.md', null, { tooLarge: true }))
      },
    })

    expect(await screen.findAllByText('GitHub didn’t answer just now, so this is the last copy Trekky has.')).not.toHaveLength(0)
    expect(await screen.findByText('This Mermaid diagram couldn’t be drawn.')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('link', { name: 'Setting up' }))
    expect(await screen.findByText('This doc is larger than 1 MB, so Trekky doesn’t show it.')).toBeInTheDocument()
  })

  it('says a doc does not exist', async () => {
    renderDocs('/TRK/docs/nope.md')

    expect(await screen.findByText('This doc doesn’t exist on the branch (any more).')).toBeInTheDocument()
  })

  it('points an admin to project settings when no repository is linked', async () => {
    renderDocs('/TRK/docs', { [`GET ${docsPath}`]: () => problemResponse(404, { title: 'No GitHub repository linked' }) })

    expect(await screen.findByText('This project has no docs repository yet.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Link one in project settings' })).toHaveAttribute('href', '/TRK/settings')
    expect(screen.queryByRole('button', { name: 'Refresh' })).toBeNull()
  })

  it('tells a viewer to ask an admin when no repository is linked', async () => {
    renderDocs('/TRK/docs', { [`GET ${docsPath}`]: () => problemResponse(404, { title: 'No GitHub repository linked' }) }, 'viewer')

    expect(await screen.findByText('An Admin or Owner can link one in the project’s settings.')).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Link one in project settings' })).toBeNull()
  })

  it.each([
    [503, 'GitHub isn’t set up on this Trekky server yet.'],
    [409, 'The Trekky app was uninstalled or suspended on GitHub. An Admin can reconnect it in Settings.'],
    [502, 'GitHub didn’t answer, and nothing is cached yet. Try again in a moment.'],
  ])('explains a %i from the API', async (status, message) => {
    renderDocs('/TRK/docs', { [`GET ${docsPath}`]: () => problemResponse(status, { title: 'Problem' }) })

    expect(await screen.findByRole('alert')).toHaveTextContent(message)
  })

  it('says when the docs folder is gone from the branch', async () => {
    renderDocs('/TRK/docs', { [`GET ${docsPath}`]: () => jsonResponse({ ...tree, folderMissing: true, files: [] }) })

    expect(await screen.findByRole('alert')).toHaveTextContent('The docs folder no longer exists on this branch on GitHub.')
  })
})
