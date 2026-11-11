import { useId, useRef, useState } from 'react'
import { Link, useLocation, useParams } from 'react-router'
import { ProblemError } from '../api/problem.ts'
import { MarkdownText } from '../components/MarkdownText.tsx'
import { RelativeTime } from '../components/RelativeTime.tsx'
import { Button } from '../components/ui/Button.tsx'
import { messages } from '../i18n/messages.ts'
import { useUpdateWorkItem, useWorkItem, type UpdateWorkItemInput, type VersionedWorkItem } from '../workItems/useWorkItems.ts'
import { StatusSelect } from '../workItems/StatusSelect.tsx'
import { WorkItemComments } from '../workItems/WorkItemComments.tsx'
import { WorkItemForm, type WorkItemFormValues } from '../workItems/WorkItemForm.tsx'
import { WorkItemHistory } from '../workItems/WorkItemHistory.tsx'
import { isWorkItemType, statusLabel, typeLabel } from '../workItems/workItemTypes.ts'

/** The later of two ISO times. */
function later(a: string, b: string | null): string {
  return b !== null && Date.parse(b) > Date.parse(a) ? b : a
}

/** One item (US-09, wireframes: Item detail) at /TRK/items/TRK-42, with its status select (US-10), comments (US-12) and history. */
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
function changesFrom(item: VersionedWorkItem['item'], values: WorkItemFormValues): UpdateWorkItemInput {
  const changes: UpdateWorkItemInput = {}
  if (values.type !== item.type) changes.type = values.type
  if (values.title !== item.title) changes.title = values.title
  if (values.descriptionMd !== (item.descriptionMd ?? '')) changes.descriptionMd = values.descriptionMd || null
  if ((values.parent?.id ?? null) !== (item.parent?.id ?? null)) changes.parentId = values.parent?.id ?? null
  return changes
}

function WorkItemView({ projectKey, current }: { projectKey: string; current: VersionedWorkItem }) {
  const { item } = current
  // Set by a board card, so the breadcrumb returns to the board instead of the list.
  const backTo = (useLocation().state as { backTo?: string } | null)?.backTo
  // Moving to a parent or child keeps the way back.
  const itemLinkState = backTo ? { backTo } : undefined
  const update = useUpdateWorkItem(current)
  const [editing, setEditing] = useState(false)
  const [announcement, setAnnouncement] = useState('')
  // A comment is not an edit of the item, but its event joins the history: the history waits for it like for an edit.
  const [lastCommentAt, setLastCommentAt] = useState<string | null>(null)
  const editButton = useRef<HTMLButtonElement>(null)
  const statusLabelId = useId()

  const stale = update.error instanceof ProblemError && update.error.problem.status === 412

  function closeForm() {
    setEditing(false)
    update.reset()
    requestAnimationFrame(() => editButton.current?.focus())
  }

  function save(values: WorkItemFormValues) {
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
        <Link to={backTo ?? `/${projectKey}`} className="font-mono underline-offset-4 hover:underline">
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
          projectId={item.projectId}
          itemId={item.id}
          initial={{
            type: isWorkItemType(item.type) ? item.type : 'task',
            title: item.title,
            descriptionMd: item.descriptionMd ?? '',
            parent: item.parent ? { id: item.parent.id, key: item.parent.key, type: item.parent.type, title: item.parent.title } : null,
          }}
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
              <MarkdownText>{item.descriptionMd}</MarkdownText>
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
                    <Link to={`/${projectKey}/items/${child.key}`} state={itemLinkState} className="font-mono font-semibold underline-offset-4 hover:underline">
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
            <dt id={statusLabelId} className="self-center text-muted-foreground">
              {messages.workItem.status}
            </dt>
            <dd>
              <StatusSelect
                current={current}
                labelledBy={statusLabelId}
                // A move gives the item a new version, which would restart the open form and lose what was typed.
                disabled={editing}
                onMoved={(status) => setAnnouncement(messages.workItem.statusChanged(item.key, statusLabel(status)))}
              />
            </dd>
            <dt className="text-muted-foreground">{messages.workItem.parent}</dt>
            <dd>
              {item.parent ? (
                <Link to={`/${projectKey}/items/${item.parent.key}`} state={itemLinkState} className="underline-offset-4 hover:underline">
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

      {/* Keyed so that moving to another item never shows the previous item's comments or events while loading. */}
      <WorkItemComments
        key={`comments-${item.id}`}
        workItemId={item.id}
        onPosted={(createdAt) => {
          setLastCommentAt(createdAt)
          // Cleared first, so a second comment in a row is announced again.
          setAnnouncement('')
          window.setTimeout(() => setAnnouncement(messages.comments.posted), 50)
        }}
      />
      <WorkItemHistory key={item.id} workItemId={item.id} updatedAt={later(item.updatedAt, lastCommentAt)} />
    </article>
  )
}
