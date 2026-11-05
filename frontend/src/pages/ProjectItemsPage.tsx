import { useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { ProblemError } from '../api/problem.ts'
import { RelativeTime } from '../components/RelativeTime.tsx'
import { Button } from '../components/ui/Button.tsx'
import { messages } from '../i18n/messages.ts'
import { useCreateWorkItem, useProjectByKey, useWorkItems } from '../workItems/useWorkItems.ts'
import { WorkItemForm } from '../workItems/WorkItemForm.tsx'
import { statusLabel, typeLabel } from '../workItems/workItemTypes.ts'

const EMPTY_ITEM = { type: 'task', title: '', descriptionMd: '', parent: null } as const

/** A project's items as a list, newest update first (US-08, wireframes: List view), with "New item" (US-06). */
export function ProjectItemsPage() {
  const { projectKey = '' } = useParams()
  const project = useProjectByKey(projectKey)

  if (project.isPending) return <p className="text-muted-foreground">{messages.project.loading}</p>
  if (project.isError) {
    const notFound = project.error instanceof ProblemError && project.error.problem.status === 404
    return <p role="alert">{notFound ? messages.project.notFound : messages.common.unexpectedError}</p>
  }

  return <ProjectItems projectId={project.data.id} projectKey={project.data.key} projectName={project.data.name} />
}

function ProjectItems({ projectId, projectKey, projectName }: { projectId: string; projectKey: string; projectName: string }) {
  const items = useWorkItems(projectId)
  const createItem = useCreateWorkItem(projectId)
  const navigate = useNavigate()
  const [creating, setCreating] = useState(false)
  const [announcement, setAnnouncement] = useState('')
  const newItemButton = useRef<HTMLButtonElement>(null)

  const rows = items.data?.pages.flatMap((page) => page.items) ?? []

  function closeForm() {
    setCreating(false)
    createItem.reset()
    // Return focus to where the user started (WCAG 2.4.3).
    requestAnimationFrame(() => newItemButton.current?.focus())
  }

  return (
    <section className="grid gap-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="grid gap-1">
          <Link to="/" className="text-sm text-muted-foreground underline-offset-4 hover:underline">
            {messages.project.backToProjects}
          </Link>
          <h1 className="text-2xl font-semibold">
            <span className="font-mono">{projectKey}</span> · {projectName}
          </h1>
        </div>
        {!creating && (
          <Button ref={newItemButton} onClick={() => setCreating(true)}>
            <span aria-hidden="true">+</span> {messages.project.newItem}
          </Button>
        )}
      </div>

      {creating && (
        <WorkItemForm
          heading={messages.workItemForm.newTitle}
          projectId={projectId}
          initial={EMPTY_ITEM}
          submitLabel={messages.workItemForm.create}
          submittingLabel={messages.workItemForm.creating}
          pending={createItem.isPending}
          error={createItem.error}
          onCancel={closeForm}
          onSubmit={(values) =>
            createItem.mutate(
              { type: values.type, title: values.title, descriptionMd: values.descriptionMd || null, parentId: values.parent?.id ?? null },
              {
                onSuccess: (created) => {
                  setAnnouncement(messages.project.created(created.key, created.title))
                  closeForm()
                },
              },
            )
          }
        />
      )}

      <p role="status" className="sr-only">
        {announcement}
      </p>

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
    </section>
  )
}
