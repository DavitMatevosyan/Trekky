/** Where a project's docs live: the repository, branch and docs folder (`docs/`, or `''` for the whole repository). */
export interface DocsLocation {
  repository: string
  branch: string
  docsPath: string
}

/** What a link or image in a doc points at, resolved against the doc it appears in (TREK-75). */
export type DocTarget =
  /** A heading on the same page: `#setup`. */
  | { kind: 'anchor'; hash: string }
  /** Another doc in the docs folder, opened inside Trekky. `path` is relative to the docs folder. */
  | { kind: 'doc'; path: string; hash: string }
  /** An image in the docs folder, loaded through the API. */
  | { kind: 'image'; path: string }
  /** Anything else in the repository, opened on GitHub. */
  | { kind: 'repository'; url: string }
  /** A link that leads out of the repository. */
  | { kind: 'external'; url: string }
  /** A relative path that climbs out of the repository or cannot be decoded. */
  | { kind: 'invalid' }

const MARKDOWN = /\.(md|markdown)$/i
const IMAGE = /\.(png|jpe?g|gif|webp|svg)$/i
const SCHEME = /^[a-z][a-z0-9+.-]*:/i

export function isMarkdown(path: string): boolean {
  return MARKDOWN.test(path)
}

/** Escapes each segment of a slash-separated path, keeping the slashes. */
export function encodePath(path: string): string {
  return path.split('/').map(encodeURIComponent).join('/')
}

/** The Trekky URL of a doc: `/TREK/docs/guide/setup.md#install`. */
export function docRoute(projectKey: string, path: string, hash = ''): string {
  return `/${projectKey}/docs/${encodePath(path)}${hash ? `#${hash}` : ''}`
}

/** A file or folder in the repository on GitHub. */
export function gitHubUrl(location: DocsLocation, repositoryPath: string, folder = false): string {
  const base = `https://github.com/${location.repository}/${folder ? 'tree' : 'blob'}/${encodePath(location.branch)}`
  return repositoryPath ? `${base}/${encodePath(repositoryPath)}` : base
}

/**
 * Resolves `href` from the doc at `fromDoc` (a path inside the docs folder) the way GitHub does: relative to the doc's
 * folder, or to the repository root when it starts with `/`. Markdown files (for links) and images (for images) inside
 * the docs folder stay in Trekky; every other file opens on GitHub.
 */
export function resolveDocTarget(href: string, fromDoc: string, location: DocsLocation, use: 'link' | 'image'): DocTarget {
  const value = href.trim()
  if (!value) return { kind: 'invalid' }
  if (value.startsWith('#')) return { kind: 'anchor', hash: value.slice(1) }
  if (SCHEME.test(value) || value.startsWith('//')) return { kind: 'external', url: value }

  const hashAt = value.indexOf('#')
  const hash = hashAt >= 0 ? value.slice(hashAt + 1) : ''
  const withoutHash = hashAt >= 0 ? value.slice(0, hashAt) : value
  const queryAt = withoutHash.indexOf('?')
  const rawPath = queryAt >= 0 ? withoutHash.slice(0, queryAt) : withoutHash

  let path: string
  try {
    path = decodeURIComponent(rawPath)
  } catch {
    return { kind: 'invalid' }
  }

  const fromFolder = (location.docsPath + fromDoc).split('/').slice(0, -1)
  const segments = path.startsWith('/') ? [] : fromFolder
  const resolved = [...segments]
  for (const segment of path.split('/')) {
    if (segment === '' || segment === '.') continue
    if (segment === '..') {
      if (resolved.length === 0) return { kind: 'invalid' }
      resolved.pop()
    } else {
      resolved.push(segment)
    }
  }

  const repositoryPath = resolved.join('/')
  const folder = path.endsWith('/') || repositoryPath === ''
  const inDocs = repositoryPath.startsWith(location.docsPath) && repositoryPath.length > location.docsPath.length
  const relative = repositoryPath.slice(location.docsPath.length)
  if (!folder && inDocs) {
    if (use === 'link' && isMarkdown(relative)) return { kind: 'doc', path: relative, hash }
    if (use === 'image' && IMAGE.test(relative)) return { kind: 'image', path: relative }
  }
  return { kind: 'repository', url: gitHubUrl(location, repositoryPath, folder) }
}

export type DocTreeNode =
  | { kind: 'folder'; name: string; path: string; children: DocTreeNode[] }
  | { kind: 'doc'; name: string; path: string; title: string }

/** The docs folder's files as nested folders: folders first, then docs, each by name. */
export function buildDocTree(files: readonly { path: string; title: string }[]): DocTreeNode[] {
  const root: DocTreeNode[] = []
  const folders = new Map<string, DocTreeNode[]>([['', root]])

  function folderOf(path: string): DocTreeNode[] {
    const existing = folders.get(path)
    if (existing) return existing
    const slash = path.lastIndexOf('/')
    const parent = folderOf(slash < 0 ? '' : path.slice(0, slash))
    const children: DocTreeNode[] = []
    parent.push({ kind: 'folder', name: path.slice(slash + 1), path, children })
    folders.set(path, children)
    return children
  }

  for (const file of files) {
    const slash = file.path.lastIndexOf('/')
    folderOf(slash < 0 ? '' : file.path.slice(0, slash)).push({
      kind: 'doc',
      name: file.path.slice(slash + 1),
      path: file.path,
      title: file.title,
    })
  }

  const sort = (nodes: DocTreeNode[]) => {
    nodes.sort((a, b) => (a.kind === b.kind ? a.name.localeCompare(b.name) : a.kind === 'folder' ? -1 : 1))
    for (const node of nodes) if (node.kind === 'folder') sort(node.children)
  }
  sort(root)
  return root
}

/** The doc shown when none is chosen: the folder's README or index, as on GitHub. */
export function defaultDoc(files: readonly { path: string }[]): string | null {
  for (const name of ['readme.md', 'index.md', 'readme.markdown']) {
    const match = files.find((file) => file.path.toLowerCase() === name)
    if (match) return match.path
  }
  return null
}
