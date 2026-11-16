import { Link, useNavigate, useParams } from 'react-router'
import { RelativeTime } from '../components/RelativeTime.tsx'
import { Button } from '../components/ui/Button.tsx'
import { messages } from '../i18n/messages.ts'
import { ProjectLoader } from '../projects/ProjectLoader.tsx'
import { ProjectView } from '../projects/ProjectView.tsx'
import { useWorkItems } from '../workItems/useWorkItems.ts'
import { statusLabel, typeLabel } from '../workItems/workItemTypes.ts'

/** A project's items as a list, newest update first (US-08, wireframes: List view), with "New item" (US-06). */
export function ProjectItemsPage() {
  const { projectKey = '' } = useParams()
  return (
    <ProjectLoader projectKey={projectKey}>
      {(project) => <ProjectItems projectId={project.id} projectKey={project.key} projectName={project.name} />}
    </ProjectLoader>
  )
}

function ProjectItems({ projectId, projectKey, projectName }: { projectId: string; projectKey: string; projectName: string }) {
  const items = useWorkItems(projectId)
  const navigate = useNavigate()

  const rows = items.data?.pages.flatMap((page) => page.items) ?? []

  return (
    <ProjectView projectId={projectId} projectKey={projectKey} projectName={projectName}>
      {items.isPending && <p className="text-muted-foreground">{messages.project.loadingItems}</p>}
      {items.isError && <p role="alert">{messages.common.unexpectedError}</p>}
      {items.isSuccess && rows.length === 0 && <p className="text-muted-foreground">{messages.project.empty}</p>}

      {rows.length > 0 && (
        <div className="overflow-x-auto rounded-xl border border-border">
          <table className="w-full text-left text-sm">
            <caption className="sr-only">{messages.project.items}</caption>
            <thead className="border-b border-border text-muted-foreground">
              <tr>
                <th scope="col" className="px-4 py-2 font-medium">{messages.project.columns.key}</th>
                <th scope="col" className="px-4 py-2 font-medium">{messages.project.columns.type}</th>
                <th scope="col" className="px-4 py-2 font-medium">{messages.project.columns.title}</th>
                <th scope="col" className="px-4 py-2 font-medium">{messages.project.columns.status}</th>
                <th scope="col" className="px-4 py-2 font-medium">{messages.project.columns.assignee}</th>
                <th scope="col" className="px-4 py-2 font-medium">{messages.project.columns.parent}</th>
                <th scope="col" className="px-4 py-2 font-medium">{messages.project.columns.updated}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {rows.map((item) => {
                const href = `/${projectKey}/items/${item.key}`
                return (
                  // US-08 AC3: the whole row opens the item; the key is the link for keyboard and screen-reader users.
                  <tr key={item.id} className="cursor-pointer hover:bg-muted" onClick={() => void navigate(href)}>
                    <td className="px-4 py-2 font-mono whitespace-nowrap">
                      <Link to={href} className="font-semibold underline-offset-4 hover:underline" onClick={(e) => e.stopPropagation()}>
                        {item.key}
                      </Link>
                    </td>
                    <td className="px-4 py-2 whitespace-nowrap">{typeLabel(item.type)}</td>
                    <td className="px-4 py-2">{item.title}</td>
                    <td className="px-4 py-2 whitespace-nowrap">{statusLabel(item.status)}</td>
                    <td className="px-4 py-2 whitespace-nowrap">{item.assignee?.displayName ?? messages.workItem.nobody}</td>
                    <td className="px-4 py-2 font-mono whitespace-nowrap">{item.parentKey ?? messages.project.noParent}</td>
                    <td className="px-4 py-2 whitespace-nowrap text-muted-foreground">
                      <RelativeTime value={item.updatedAt} />
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}

      {items.hasNextPage && (
        <Button variant="outline" className="justify-self-center" onClick={() => void items.fetchNextPage()} disabled={items.isFetchingNextPage}>
          {messages.project.loadMore}
        </Button>
      )}
    </ProjectView>
  )
}
