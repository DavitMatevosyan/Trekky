import { useEffect, useRef, useState, type FormEvent } from 'react'
import { fieldErrors, formLevelError, ProblemError, type ProblemDetails } from '../api/problem.ts'
import { FormError } from '../components/FormError.tsx'
import { FormField } from '../components/FormField.tsx'
import { SelectField } from '../components/SelectField.tsx'
import { TextAreaField } from '../components/TextAreaField.tsx'
import { Button } from '../components/ui/Button.tsx'
import { messages } from '../i18n/messages.ts'
import { ParentPicker, type ParentChoice } from './ParentPicker.tsx'
import { useResolveParent } from './useResolveParent.ts'
import { ALLOWED_PARENT_TYPES, isWorkItemType, parentHint, typeLabel, WORK_ITEM_TYPES, type WorkItemType } from './workItemTypes.ts'

export interface WorkItemFormValues {
  type: WorkItemType
  title: string
  descriptionMd: string
  parent: ParentChoice | null
}

const FIELDS = ['type', 'title', 'descriptionMd', 'parentId']

function fitsType(parent: ParentChoice | null, type: WorkItemType): boolean {
  return parent === null || ALLOWED_PARENT_TYPES[type].some((allowed) => allowed === parent.type)
}
const TYPE_OPTIONS = WORK_ITEM_TYPES.map((type) => ({ value: type, label: typeLabel(type) }))

/** Where a problem from the API shows: parent rules on the parent field, children that no longer fit on the type. */
function errorsFor(problem: ProblemDetails | null): Record<string, string | undefined> {
  const errors: Record<string, string | undefined> = problem ? fieldErrors(problem) : {}
  let parent = errors.parentId
  let type = errors.type
  if (problem?.status === 422 && problem.detail) {
    if (problem.conflictingChildKeys?.length) type = problem.detail
    else parent = problem.detail
  }
  return { ...errors, parent, type }
}

/** The message above the form; parent rule problems (422) show on their fields instead. */
function formMessage(error: Error | null, problem: ProblemDetails | null, notice: string | null) {
  if (notice) return notice
  if (!error) return null
  if (!problem) return messages.common.unexpectedError
  return problem.status === 422 ? null : formLevelError(problem, FIELDS)
}

/**
 * The fields of a work item (US-06 AC1, US-07): type, title, Markdown description and parent. The parent is chosen
 * from the project's items of the types the chosen type allows (TRK-11.1); a type change drops a parent that no
 * longer fits. A key typed but not chosen is looked up before saving, so the item is never saved without the parent
 * the field shows.
 */
export function WorkItemForm({
  heading,
  projectId,
  itemId,
  initial,
  submitLabel,
  submittingLabel,
  pending,
  error,
  notice,
  onSubmit,
  onCancel,
}: {
  heading: string
  projectId: string
  /** The item being edited, which is never offered as its own parent; absent when creating. */
  itemId?: string
  initial: WorkItemFormValues
  submitLabel: string
  submittingLabel: string
  pending: boolean
  error: Error | null
  /** A message above the fields that is not an error of this form, e.g. "changed by someone else". */
  notice?: string | null
  onSubmit: (values: WorkItemFormValues) => void
  onCancel: () => void
}) {
  const [values, setValues] = useState(initial)
  const resolveParent = useResolveParent(projectId, itemId)
  // Text typed in the parent field but not chosen; an error found for it shows until the text changes.
  const [pendingParent, setPendingParent] = useState('')
  const [parentError, setParentError] = useState<{ text: string; message: string } | null>(null)
  const [resolving, setResolving] = useState(false)
  const titleInput = useRef<HTMLInputElement>(null)
  const headingId = `work-item-form-${heading.replace(/\W+/g, '-').toLowerCase()}`

  useEffect(() => titleInput.current?.focus(), [])

  const problem = error instanceof ProblemError ? error.problem : null
  const errors = errorsFor(problem)
  const formError = formMessage(error, problem, notice ?? null)

  function update(changes: Partial<WorkItemFormValues>) {
    setValues((current) => ({ ...current, ...changes }))
  }

  function changeType(type: WorkItemType) {
    setValues((current) => ({
      ...current,
      type,
      // A parent the new type does not allow is dropped rather than sent and refused.
      parent: fitsType(current.parent, type) ? current.parent : null,
    }))
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setParentError(null)
    let parent = values.parent
    if (!parent && pendingParent) {
      setResolving(true)
      const result = await resolveParent(pendingParent, ALLOWED_PARENT_TYPES[values.type])
      setResolving(false)
      if ('error' in result) {
        setParentError({ text: pendingParent, message: result.error })
        return
      }
      parent = result.choice
      update({ parent })
    }
    onSubmit({ ...values, parent })
  }

  const busy = pending || resolving
  const parentFieldError = parentError?.text === pendingParent ? parentError.message : errors.parent

  return (
    <form aria-labelledby={headingId} className="grid gap-4 rounded-xl border border-border bg-card p-4" onSubmit={(event) => void submit(event)} noValidate>
      <h2 id={headingId} className="text-lg font-semibold">
        {heading}
      </h2>
      <FormError message={formError} />
      <SelectField
        label={messages.workItemForm.type}
        name="type"
        className="w-48"
        options={TYPE_OPTIONS}
        value={values.type}
        onChange={(event) => {
          if (isWorkItemType(event.target.value)) changeType(event.target.value)
        }}
        error={errors.type}
      />
      <FormField
        ref={titleInput}
        label={messages.workItemForm.title}
        name="title"
        autoComplete="off"
        required
        maxLength={200}
        value={values.title}
        onChange={(event) => update({ title: event.target.value })}
        error={errors.title}
      />
      <TextAreaField
        label={messages.workItemForm.description}
        name="descriptionMd"
        maxLength={50_000}
        value={values.descriptionMd}
        onChange={(event) => update({ descriptionMd: event.target.value })}
        hint={messages.workItemForm.descriptionHint}
        error={errors.descriptionMd}
      />
      <ParentPicker
        projectId={projectId}
        allowedTypes={ALLOWED_PARENT_TYPES[values.type]}
        excludeId={itemId}
        value={values.parent}
        // A choice that no longer fits the type (a lookup that finished after a type change) is dropped.
        onChange={(parent) => setValues((current) => ({ ...current, parent: fitsType(parent, current.type) ? parent : null }))}
        onPendingChange={setPendingParent}
        hint={parentHint(values.type)}
        error={parentFieldError}
      />
      <div className="flex justify-end gap-2">
        <Button type="button" variant="outline" onClick={onCancel}>
          {messages.workItemForm.cancel}
        </Button>
        <Button type="submit" disabled={busy}>
          {busy ? submittingLabel : submitLabel}
        </Button>
      </div>
    </form>
  )
}
