import { lazy, Suspense, useRef, useState } from 'react'
import { Link, useParams } from 'react-router'
import { ErrorBoundary } from '../components/ErrorBoundary.tsx'
import { ProblemError } from '../api/problem.ts'
import { RelativeTime } from '../components/RelativeTime.tsx'
import { Button } from '../components/ui/Button.tsx'
import { messages } from '../i18n/messages.ts'
import { useUpdateWorkItem, useWorkItem, type UpdateWorkItemInput, type VersionedWorkItem } from '../workItems/useWorkItems.ts'
import { WorkItemForm, type WorkItemFormSubmit } from '../workItems/WorkItemForm.tsx'
import { isWorkItemType, statusLabel, typeLabel } from '../workItems/workItemTypes.ts'

// The Markdown renderer is most of a bundle's weight, and only this page needs it.
const Markdown = lazy(() => import('../components/Markdown.tsx').then((module) => ({ default: module.Markdown })))

/** One item (US-09, wireframes: Item detail) at /TRK/items/TRK-42; history arrives with TRK-12. */
export function WorkItemPage() {
  const { projectKey = '', itemKey = '' } = useParams()
  // /TRK/items/WEB-1 names another project's item: not an address of this project.
  const inProject = itemKey.startsWith(`${projectKey}-`)
  const workItem = useWorkItem(itemKey, inProject)

  if (!inProject) return <p role="alert">{messages.workItem.notFound}</p>
  if (workItem.isPending) return <p className="text-muted-foreground">{messages.workItem.loading}</p>
  if (workItem.isError) {
    const notFound = workItem.error instanceof ProblemError && workItem.error.problem.status === 404
    return <p role="alert">{notFound ? messages.workItem.notFound : messages.common.unexpectedError}</p>
  }

  return <WorkItemView projectKey={projectKey} current={workItem.data} />
}

/** Only the fields that changed, so the merge patch does not touch the rest (US-07). */
function changesFrom(item: VersionedWorkItem['item'], values: WorkItemFormSubmit): UpdateWorkItemInput {
  const changes: UpdateWorkItemInput = {}
  if (values.type !== item.type) changes.type = values.type
  if (values.title !== item.title) changes.title = values.title
  if (values.descriptionMd !== (item.descriptionMd ?? '')) changes.descriptionMd = values.descriptionMd || null
  if (values.parentKey !== (item.parentKey ?? '')) changes.parentId = values.parentId
  return changes
}

function WorkItemView({ projectKey, current }: { projectKey: string; current: VersionedWorkItem }) {
  const { item } = current
  const update = useUpdateWorkItem(current)
  const [editing, setEditing] = useState(false)
  const [announcement, setAnnouncement] = useState('')
  const editButton = useRef<HTMLButtonElement>(null)

  const stale = update.error instanceof ProblemError && update.error.problem.status === 412

  function closeForm() {
    setEditing(false)
    update.reset()
    requestAnimationFrame(() => editButton.current?.focus())
  }

  function save(values: WorkItemFormSubmit) {
    const changes = changesFrom(item, values)
    if (Object.keys(changes).length === 0) {
      closeForm()
      return
    }
    update.mutate(changes, {
      onSuccess: (saved) => {
        setAnnouncement(messages.workItem.saved(saved.item.key))
        closeForm()
      },
    })
  }

  return (
    <article className="grid gap-6">
      <nav aria-label={messages.workItem.breadcrumb} className="text-sm text-muted-foreground">
        <Link to={`/${projectKey}`} className="font-mono underline-offset-4 hover:underline">
          {projectKey}
        </Link>
        <span aria-hidden="true"> / </span>
        <span className="font-mono" aria-current="page">
          {item.key}
        </span>
      </nav>

      <div className="flex flex-wrap items-start justify-between gap-4">
        <h1 className="flex flex-wrap items-center gap-3 text-2xl font-semibold">
          <span className="rounded-md border border-border px-2 py-0.5 text-sm font-medium">{typeLabel(item.type)}</span>
          <span>{item.title}</span>
        </h1>
        {!editing && (
          <Button ref={editButton} variant="outline" onClick={() => setEditing(true)}>
            {messages.workItem.edit}
          </Button>
        )}
      </div>

      <p role="status" className="sr-only">
        {announcement}
      </p>

      {editing && (
        <WorkItemForm
          // A new version (after a 412 reload) starts the form again from the latest values.
          key={current.etag}
          heading={messages.workItemForm.editTitle}
          projectKey={projectKey}
          initial={{
            type: isWorkItemType(item.type) ? item.type : 'task',
            title: item.title,
            descriptionMd: item.descriptionMd ?? '',
            parentKey: item.parentKey ?? '',
          }}
          initialParentId={item.parent?.id ?? null}
          submitLabel={messages.workItemForm.save}
          submittingLabel={messages.workItemForm.saving}
          pending={update.isPending}
          error={stale ? null : update.error}
          notice={stale ? messages.workItemForm.staleItem : null}
          onCancel={closeForm}
          onSubmit={save}
        />
      )}

      <div className="grid gap-6 md:grid-cols-[1fr_16rem]">
        <div className="grid content-start gap-6">
          <section aria-labelledby="description-heading" className="grid gap-2">
            <h2 id="description-heading" className="font-semibold">
              {messages.workItem.description}
            </h2>
            {item.descriptionMd ? (
              // Until the renderer loads, or if it fails to load, the description shows as plain text.
              <ErrorBoundary fallback={<p className="text-sm whitespace-pre-wrap">{item.descriptionMd}</p>}>
                <Suspense fallback={<p className="text-sm whitespace-pre-wrap">{item.descriptionMd}</p>}>
                  <Markdown>{item.descriptionMd}</Markdown>
                </Suspense>
              </ErrorBoundary>
            ) : <p className="text-sm text-muted-foreground">{messages.workItem.noDescription}</p>}
          </section>

          <section aria-labelledby="children-heading" className="grid gap-2">
            <h2 id="children-heading" className="font-semibold">
              {messages.workItem.children}
            </h2>
            {item.children.length === 0 ? (
              <p className="text-sm text-muted-foreground">{messages.workItem.noChildren}</p>
            ) : (
              <ul aria-labelledby="children-heading" className="divide-y divide-border rounded-xl border border-border text-sm">
                {item.children.map((child) => (
                  <li key={child.id} className="flex items-center gap-3 px-3 py-2">
                    <Link to={`/${projectKey}/items/${child.key}`} className="font-mono font-semibold underline-offset-4 hover:underline">
                      {child.key}
                    </Link>
                    <span className="text-muted-foreground">{typeLabel(child.type)}</span>
                    <span className="flex-1">{child.title}</span>
                    <span className="whitespace-nowrap">{statusLabel(child.status)}</span>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </div>

        <section aria-labelledby="details-heading" className="grid content-start gap-3 rounded-xl border border-border p-4 text-sm">
          <h2 id="details-heading" className="sr-only">
            {messages.workItem.details}
          </h2>
          <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2">
            <dt className="text-muted-foreground">{messages.workItem.status}</dt>
            <dd>{statusLabel(item.status)}</dd>
            <dt className="text-muted-foreground">{messages.workItem.parent}</dt>
            <dd>
              {item.parent ? (
                <Link to={`/${projectKey}/items/${item.parent.key}`} className="underline-offset-4 hover:underline">
                  <span className="font-mono">{item.parent.key}</span> {typeLabel(item.parent.type)}
                </Link>
              ) : (
                messages.workItem.noParent
              )}
            </dd>
            <dt className="text-muted-foreground">{messages.workItem.created}</dt>
            <dd>
              <RelativeTime value={item.createdAt} />
            </dd>
            <dt className="text-muted-foreground">{messages.workItem.updated}</dt>
            <dd>
              <RelativeTime value={item.updatedAt} />
            </dd>
          </dl>
        </section>
      </div>
    </article>
  )
}
