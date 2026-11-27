import { buildDocTree, defaultDoc, docRoute, resolveDocTarget, type DocsLocation } from './docPaths.ts'

const location: DocsLocation = { repository: 'acme/handbook', branch: 'dev', docsPath: 'docs/' }

describe('resolveDocTarget', () => {
  it.each([
    ['setup.md', 'guide/index.md', { kind: 'doc', path: 'guide/setup.md', hash: '' }],
    ['./setup.md#install', 'guide/index.md', { kind: 'doc', path: 'guide/setup.md', hash: 'install' }],
    ['../README.md', 'guide/index.md', { kind: 'doc', path: 'README.md', hash: '' }],
    ['/docs/adr/0001.md', 'guide/index.md', { kind: 'doc', path: 'adr/0001.md', hash: '' }],
    ['my%20notes.md?plain=1', 'README.md', { kind: 'doc', path: 'my notes.md', hash: '' }],
    ['deploy.MARKDOWN', 'README.md', { kind: 'doc', path: 'deploy.MARKDOWN', hash: '' }],
  ])('a Markdown link %s from %s opens inside Trekky', (href, from, expected) => {
    expect(resolveDocTarget(href, from, location, 'link')).toEqual(expected)
  })

  it.each([
    ['../src/Program.cs', 'README.md', 'https://github.com/acme/handbook/blob/dev/src/Program.cs'],
    ['../README.md', 'README.md', 'https://github.com/acme/handbook/blob/dev/README.md'],
    ['diagram.drawio', 'guide/a.md', 'https://github.com/acme/handbook/blob/dev/docs/guide/diagram.drawio'],
    ['../', 'README.md', 'https://github.com/acme/handbook/tree/dev'],
    ['guide/', 'README.md', 'https://github.com/acme/handbook/tree/dev/docs/guide'],
  ])('a link %s from %s outside the docs or not Markdown opens on GitHub', (href, from, url) => {
    expect(resolveDocTarget(href, from, location, 'link')).toEqual({ kind: 'repository', url })
  })

  it('keeps anchors, external links, and refuses paths that leave the repository', () => {
    expect(resolveDocTarget('#setup', 'README.md', location, 'link')).toEqual({ kind: 'anchor', hash: 'setup' })
    expect(resolveDocTarget('https://example.com/a', 'README.md', location, 'link')).toEqual({ kind: 'external', url: 'https://example.com/a' })
    expect(resolveDocTarget('mailto:ada@example.com', 'README.md', location, 'link')).toEqual({ kind: 'external', url: 'mailto:ada@example.com' })
    expect(resolveDocTarget('//cdn.example.com/x.png', 'README.md', location, 'image')).toEqual({ kind: 'external', url: '//cdn.example.com/x.png' })
    expect(resolveDocTarget('../../../etc/passwd', 'README.md', location, 'link')).toEqual({ kind: 'invalid' })
    expect(resolveDocTarget('%E0%A4%A', 'README.md', location, 'link')).toEqual({ kind: 'invalid' })
    expect(resolveDocTarget('  ', 'README.md', location, 'link')).toEqual({ kind: 'invalid' })
  })

  it('loads images inside the docs folder and links to others on GitHub', () => {
    expect(resolveDocTarget('../images/flow.png', 'guide/setup.md', location, 'image')).toEqual({ kind: 'image', path: 'images/flow.png' })
    expect(resolveDocTarget('flow.SVG', 'README.md', location, 'image')).toEqual({ kind: 'image', path: 'flow.SVG' })
    expect(resolveDocTarget('../logo.png', 'README.md', location, 'image')).toEqual({
      kind: 'repository',
      url: 'https://github.com/acme/handbook/blob/dev/logo.png',
    })
    expect(resolveDocTarget('notes.md', 'README.md', location, 'image')).toEqual({
      kind: 'repository',
      url: 'https://github.com/acme/handbook/blob/dev/docs/notes.md',
    })
  })

  it('treats the whole repository as the docs folder when docsPath is empty', () => {
    const root = { ...location, docsPath: '' }
    expect(resolveDocTarget('/README.md', 'guide/a.md', root, 'link')).toEqual({ kind: 'doc', path: 'README.md', hash: '' })
  })

  it('escapes branch and path segments in GitHub URLs', () => {
    const feature = { ...location, branch: 'feature/docs v2' }
    expect(resolveDocTarget('../a b.txt', 'README.md', feature, 'link')).toEqual({
      kind: 'repository',
      url: 'https://github.com/acme/handbook/blob/feature/docs%20v2/a%20b.txt',
    })
  })
})

describe('docRoute', () => {
  it('escapes each segment and keeps the hash', () => {
    expect(docRoute('TREK', 'guide/my notes.md', 'install')).toBe('/TREK/docs/guide/my%20notes.md#install')
    expect(docRoute('TREK', 'README.md')).toBe('/TREK/docs/README.md')
  })
})

describe('buildDocTree', () => {
  it('nests files in folders, folders first, each by name', () => {
    const tree = buildDocTree([
      { path: 'README.md', title: 'Handbook' },
      { path: 'guide/setup.md', title: 'Setting up' },
      { path: 'adr/0001.md', title: 'ADR 1' },
      { path: 'guide/deep/x.md', title: 'X' },
      { path: 'about.md', title: 'About' },
    ])

    expect(tree.map((n) => `${n.kind}:${n.name}`)).toEqual(['folder:adr', 'folder:guide', 'doc:about.md', 'doc:README.md'])
    const guide = tree[1]
    expect(guide?.kind === 'folder' && guide.children.map((n) => `${n.kind}:${n.path}`)).toEqual(['folder:guide/deep', 'doc:guide/setup.md'])
  })
})

describe('defaultDoc', () => {
  it('prefers the README at the top of the folder, then index.md', () => {
    expect(defaultDoc([{ path: 'a.md' }, { path: 'guide/README.md' }, { path: 'Readme.md' }])).toBe('Readme.md')
    expect(defaultDoc([{ path: 'index.md' }, { path: 'a.md' }])).toBe('index.md')
    expect(defaultDoc([{ path: 'a.md' }])).toBeNull()
  })
})
