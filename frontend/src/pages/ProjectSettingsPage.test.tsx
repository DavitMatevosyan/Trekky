import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { fakeApi, jsonResponse, me, problemResponse, renderApp, requestsTo, validTokens } from '../test/renderApp.tsx'

const project = { id: '0190a000-0000-7000-8000-0000000000b1', key: 'TRK', name: 'Trekky', itemCount: 0, createdAt: '2026-10-26T10:00:00Z' }
const acme = {
  id: '0190a000-0000-7000-8000-0000000000e1',
  installationId: 4242,
  account: 'acme',
  accountType: 'Organization',
  manageUrl: 'https://github.com/organizations/acme/settings/installations/4242',
  connectedAt: '2026-11-20T10:00:00Z',
}
const link = {
  projectId: project.id,
  installationId: acme.id,
  account: 'acme',
  repositoryId: 7,
  repository: 'acme/handbook',
  docsPath: 'docs/',
  branch: 'dev',
  htmlUrl: 'https://github.com/acme/handbook/tree/dev/docs',
  linkedAt: '2026-11-20T10:00:00Z',
}
const repositoryPath = `/api/v1/projects/${project.id}/github`
const repositories = {
  items: [
    { id: 7, fullName: 'acme/handbook', defaultBranch: 'main', private: false, htmlUrl: 'https://github.com/acme/handbook' },
    { id: 8, fullName: 'acme/wiki', defaultBranch: 'main', private: true, htmlUrl: 'https://github.com/acme/wiki' },
  ],
  truncated: false,
}

function renderProjectSettings(handlers: Parameters<typeof fakeApi>[0] = {}) {
  const fetchFn = fakeApi({
    'GET /api/v1/projects/by-key/TRK': () => jsonResponse(project),
    'GET /api/v1/github': () => jsonResponse({ configured: true, installUrl: 'https://github.com/apps/t/installations/new', installations: [acme] }),
    [`GET /api/v1/github/installations/${acme.id}/repositories`]: () => jsonResponse(repositories),
    [`GET ${repositoryPath}`]: () => problemResponse(404, { title: 'No GitHub repository linked' }),
    ...handlers,
  })
  renderApp({ path: '/TRK/settings', tokens: validTokens, fetchFn })
  return fetchFn
}

describe('ProjectSettingsPage', () => {
  it('links a repository with the default folder and branch', async () => {
    const fetchFn = renderProjectSettings({
      [`PUT ${repositoryPath}`]: () => new Response(JSON.stringify(link), { status: 201, headers: { 'Content-Type': 'application/json', ETag: '"5"' } }),
    })

    expect(await screen.findByText('No repository linked yet.')).toBeInTheDocument()
    const repository = await screen.findByRole('combobox', { name: 'Repository' })
    await screen.findByRole('option', { name: 'acme/wiki' })
    await userEvent.selectOptions(repository, 'acme/handbook')
    expect(screen.getByLabelText('Docs folder')).toHaveValue('docs/')
    expect(screen.getByLabelText('Branch')).toHaveValue('dev')
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByText(/Saved\. Trekky checked the repository/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Open on GitHub' })).toHaveAttribute('href', link.htmlUrl)
    const [request] = requestsTo(fetchFn, `PUT ${repositoryPath}`)
    expect(request?.headers.get('If-Match')).toBeNull()
    expect(await request?.json()).toEqual({ installationId: acme.id, repository: 'acme/handbook', docsPath: 'docs/', branch: 'dev' })
  })

  it('changes an existing link with its ETag', async () => {
    const fetchFn = renderProjectSettings({
      [`GET ${repositoryPath}`]: () => new Response(JSON.stringify(link), { headers: { 'Content-Type': 'application/json', ETag: '"5"' } }),
      [`PUT ${repositoryPath}`]: () =>
        new Response(JSON.stringify({ ...link, branch: 'main' }), { headers: { 'Content-Type': 'application/json', ETag: '"6"' } }),
    })

    const branch = await screen.findByLabelText('Branch')
    expect(branch).toHaveValue('dev')
    await userEvent.clear(branch)
    await userEvent.type(branch, 'main')
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByText(/Saved\./)).toBeInTheDocument()
    expect(requestsTo(fetchFn, `PUT ${repositoryPath}`)[0]?.headers.get('If-Match')).toBe('"5"')
  })

  it('keeps saying someone else changed the link after showing their version (412)', async () => {
    let version = 5
    renderProjectSettings({
      [`GET ${repositoryPath}`]: () =>
        new Response(JSON.stringify(version === 5 ? link : { ...link, branch: 'main' }), {
          headers: { 'Content-Type': 'application/json', ETag: `"${version}"` },
        }),
      [`PUT ${repositoryPath}`]: () => {
        version = 6
        return problemResponse(412, { title: 'Settings changed' })
      },
    })

    const branch = await screen.findByLabelText('Branch')
    await userEvent.clear(branch)
    await userEvent.type(branch, 'release')
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    // The form starts again from the other Admin's settings, and the notice stays.
    expect(await screen.findByDisplayValue('main')).toBeInTheDocument()
    expect(screen.getByText('Someone changed these settings meanwhile. Review them and save again.')).toBeInTheDocument()
  })

  it('shows what GitHub refused on its field', async () => {
    renderProjectSettings({
      [`PUT ${repositoryPath}`]: () =>
        problemResponse(422, {
          title: 'GitHub refused the settings',
          detail: 'This branch does not exist in the repository.',
          errors: { branch: ['This branch does not exist in the repository.'] },
        }),
    })

    await userEvent.selectOptions(await screen.findByRole('combobox', { name: 'Repository' }), 'acme/wiki')
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByLabelText('Branch')).toHaveAccessibleDescription(/This branch does not exist in the repository\./)
  })

  it('removes the link after confirming', async () => {
    let linked = true
    renderProjectSettings({
      [`GET ${repositoryPath}`]: () =>
        linked
          ? new Response(JSON.stringify(link), { headers: { 'Content-Type': 'application/json', ETag: '"5"' } })
          : problemResponse(404, { title: 'No GitHub repository linked' }),
      [`DELETE ${repositoryPath}`]: () => {
        linked = false
        return new Response(null, { status: 204 })
      },
    })

    await userEvent.click(await screen.findByRole('button', { name: 'Remove link' }))
    const confirm = screen.getByRole('group', { name: /Remove the docs repository/ })
    await userEvent.click(within(confirm).getByRole('button', { name: 'Remove link' }))

    expect(await screen.findByText('The docs repository was removed.')).toBeInTheDocument()
    expect(screen.getByText('No repository linked yet.')).toBeInTheDocument()
  })

  it('sends Admins to organization settings when no GitHub account is connected', async () => {
    renderProjectSettings({
      'GET /api/v1/github': () => jsonResponse({ configured: true, installUrl: 'https://github.com/apps/t/installations/new', installations: [] }),
    })

    expect(await screen.findByRole('link', { name: 'Go to Settings' })).toHaveAttribute('href', '/settings')
    expect(screen.queryByRole('button', { name: 'Save' })).not.toBeInTheDocument()
  })

  it('shows Viewers the link without the form and never asks for the GitHub settings', async () => {
    const fetchFn = renderProjectSettings({
      'GET /api/v1/me': () => jsonResponse({ ...me, role: 'viewer' }),
      [`GET ${repositoryPath}`]: () => new Response(JSON.stringify(link), { headers: { 'Content-Type': 'application/json', ETag: '"5"' } }),
    })

    expect(await screen.findByText('acme/handbook')).toBeInTheDocument()
    expect(screen.getByText('Only Admins and Owners can change where the docs live.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Save' })).not.toBeInTheDocument()
    expect(requestsTo(fetchFn, 'GET /api/v1/github')).toHaveLength(0)
    expect(screen.getByRole('link', { name: 'Settings' })).toHaveAttribute('href', '/TRK/settings')
  })
})
