import { useRef } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router'
import { RelativeTime } from '../components/RelativeTime.tsx'
import { Button } from '../components/ui/Button.tsx'
import { messages } from '../i18n/messages.ts'
import { cn } from '../lib/cn.ts'
import { ProjectLoader } from '../projects/ProjectLoader.tsx'
import { ProjectView } from '../projects/ProjectView.tsx'
import { FilterBar } from '../workItems/FilterBar.tsx'
import { useWorkItemFilters } from '../workItems/useWorkItemFilters.ts'
import { DEFAULT_FILTERS, isDefaultFilters, type WorkItemFilters } from '../workItems/workItemFilters.ts'
import { useWorkItems } from '../workItems/useWorkItems.ts'
import { statusLabel, typeLabel } from '../workItems/workItemTypes.ts'

/**
 * A project's items as a list, newest update first (US-08, wireframes: List view), with "New item" (US-06) and the filter
 * bar (TREK-53), whose filters live in the URL.
 */
export function ProjectItemsPage() {
  const { projectKey = '' } = useParams()
  return (
    <ProjectLoader projectKey={projectKey}>
      {/* Keyed by project, so one project's rows never stand in for another's while they load. */}
      {(project) => (
        <ProjectItems
          key={project.id}
          projectId={project.id}
          projectKey={project.key}
          projectName={project.name}
          itemCount={Number(project.itemCount)}
        />
      )}
    </ProjectLoader>
  )
}

interface ProjectItemsProps {
  projectId: string
  projectKey: string
  projectName: string
  /** Every item in the project, whatever the filters. */
  itemCount: number
}

function ProjectItems({ projectId, projectKey, projectName, itemCount }: ProjectItemsProps) {
  const [filters, setFilters] = useWorkItemFilters()
  const items = useWorkItems(projectId, filters)
  const navigate = useNavigate()
  const { search } = useLocation()

  const rows = items.data?.pages.flatMap((page) => page.items) ?? []
  const matches = Number(items.data?.pages[0]?.totalCount ?? 0)
  const filtered = !isDefaultFilters(filters)
  // The buttons below go away once clicked; the focus goes to the search box instead of the page (WCAG 2.4.3).
  const searchBox = useRef<HTMLInputElement>(null)
  function showFiltered(next: WorkItemFilters) {
    setFilters(next)
    searchBox.current?.focus()
  }

  return (
    <ProjectView projectId={projectId} projectKey={projectKey} projectName={projectName}>
      <FilterBar projectId={projectId} filters={filters} onChange={setFilters} searchRef={searchBox} />

      {items.isPending && <p className="text-muted-foreground">{messages.project.loadingItems}</p>}
      {items.isError && <p role="alert">{messages.common.unexpectedError}</p>}
      {/* Polite, so a screen reader hears the new count after each filter change without losing its place. */}
      <p aria-live="polite" className={cn('text-sm text-muted-foreground', !items.isSuccess && 'sr-only')}>
        {items.isSuccess && !items.isPlaceholderData ? messages.filters.matchCount(matches) : ''}
      </p>

      {/* Not from the previous filters' result while the new one loads: it would say the wrong thing. */}
      {items.isSuccess && !items.isPlaceholderData && rows.length === 0 && (
        // Nothing at all only when the project has no items; otherwise the filters (or the default one) hide them.
        <div className="grid justify-items-start gap-2">
          <p className="text-muted-foreground">
            {filtered ? messages.filters.noMatches : itemCount > 0 ? messages.filters.allUnderCompletedEpics : messages.project.empty}
          </p>
          {filtered && (
            <Button variant="outline" size="sm" onClick={() => showFiltered(DEFAULT_FILTERS)}>
              {messages.filters.clearFilters}
            </Button>
          )}
          {!filtered && itemCount > 0 && (
            <Button variant="outline" size="sm" onClick={() => showFiltered({ ...filters, showCompletedEpics: true })}>
              {messages.filters.showCompletedEpics}
            </Button>
          )}
        </div>
      )}

      {rows.length > 0 && (
        <div className={cn('overflow-x-auto rounded-xl border border-border bg-surface', items.isPlaceholderData && 'opacity-60')} aria-busy={items.isPlaceholderData || undefined}>
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
                // The item page's breadcrumb returns to this filtered list.
                const state = { backTo: `/${projectKey}${search}` }
                return (
                  // US-08 AC3: the whole row opens the item; the key is the link for keyboard and screen-reader users.
                  <tr key={item.id} className="cursor-pointer hover:bg-muted" onClick={() => void navigate(href, { state })}>
                    <td className="px-4 py-2 font-mono whitespace-nowrap">
                      <Link to={href} state={state} className="font-semibold underline-offset-4 hover:underline" onClick={(e) => e.stopPropagation()}>
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
