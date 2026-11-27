import { Link } from 'react-router'
import { messages } from '../i18n/messages.ts'
import { cn } from '../lib/cn.ts'
import { docRoute, type DocTreeNode } from './docPaths.ts'

interface DocsTreeNavProps {
  projectKey: string
  nodes: DocTreeNode[]
  /** The open doc, highlighted; its folders start expanded. */
  current: string | null
}

function NodeList({ projectKey, nodes, current }: DocsTreeNavProps) {
  return (
    <ul className="grid gap-0.5">
      {nodes.map((node) =>
        node.kind === 'folder' ? (
          <li key={node.path}>
            <details open={current?.startsWith(`${node.path}/`) ?? false} className="group">
              <summary className="cursor-pointer rounded px-2 py-1 text-sm font-medium hover:bg-muted">{node.name}</summary>
              <div className="ml-3 border-l border-border pl-1">
                <NodeList projectKey={projectKey} nodes={node.children} current={current} />
              </div>
            </details>
          </li>
        ) : (
          <li key={node.path}>
            <Link
              to={docRoute(projectKey, node.path)}
              aria-current={node.path === current ? 'page' : undefined}
              title={node.path}
              className={cn(
                'block truncate rounded px-2 py-1 text-sm underline-offset-4 hover:bg-muted hover:underline',
                node.path === current ? 'bg-muted font-medium text-foreground' : 'text-muted-foreground',
              )}
            >
              {node.title}
            </Link>
          </li>
        ),
      )}
    </ul>
  )
}

/** The docs folder as a tree of folders (expandable) and docs, by title (TREK-75). */
export function DocsTreeNav(props: DocsTreeNavProps) {
  return (
    <nav aria-label={messages.docs.files}>
      <NodeList {...props} />
    </nav>
  )
}
