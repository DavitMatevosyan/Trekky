import { useEffect, useRef, useState, type FormEvent } from 'react'
import { fieldErrors, formLevelError, ProblemError, type ProblemDetails } from '../api/problem.ts'
import { FormError } from '../components/FormError.tsx'
import { FormField } from '../components/FormField.tsx'
import { SelectField } from '../components/SelectField.tsx'
import { TextAreaField } from '../components/TextAreaField.tsx'
import { Button } from '../components/ui/Button.tsx'
import { messages } from '../i18n/messages.ts'
import { useFindWorkItemId } from './useWorkItems.ts'
import { isWorkItemType, parentHint, typeLabel, WORK_ITEM_TYPES, type WorkItemType } from './workItemTypes.ts'

export interface WorkItemFormValues {
  type: WorkItemType
  title: string
  descriptionMd: string
  /** The parent's key as typed, e.g. `TRK-2`; empty for no parent. */
  parentKey: string
}

export interface WorkItemFormSubmit extends WorkItemFormValues {
  /** The parent's id, found from {@link WorkItemFormValues.parentKey}; `null` for no parent. */
  parentId: string | null
}

const FIELDS = ['type', 'title', 'descriptionMd', 'parentId']
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
function formMessage(error: Error | null, problem: ProblemDetails | null, notice: string | null, lookupFailed: boolean) {
  if (lookupFailed) return messages.common.unexpectedError
  if (notice) return notice
  if (!error) return null
  if (!problem) return messages.common.unexpectedError
  return problem.status === 422 ? null : formLevelError(problem, FIELDS)
}

/**
 * The fields of a work item (US-06 AC1, US-07): type, title, Markdown description and parent. The parent is typed as
 * a key (`TRK-2`) and looked up before saving; the hint under it names the parent types the chosen type allows.
 */
export function WorkItemForm({
  heading,
  projectKey,
  initial,
  initialParentId = null,
  submitLabel,
  submittingLabel,
  pending,
  error,
  notice,
  onSubmit,
  onCancel,
}: {
  heading: string
  projectKey: string
  initial: WorkItemFormValues
  /** The id behind {@link WorkItemFormValues.parentKey} in `initial`, so an unchanged parent needs no lookup. */
  initialParentId?: string | null
  submitLabel: string
  submittingLabel: string
  pending: boolean
  error: Error | null
  /** A message above the fields that is not an error of this form, e.g. "changed by someone else". */
  notice?: string | null
  onSubmit: (values: WorkItemFormSubmit) => void
  onCancel: () => void
}) {
  const findWorkItemId = useFindWorkItemId()
  const [values, setValues] = useState(initial)
  const [parentError, setParentError] = useState<string | null>(null)
  const [lookupError, setLookupError] = useState(false)
  const [resolving, setResolving] = useState(false)
  const titleInput = useRef<HTMLInputElement>(null)
  const headingId = `work-item-form-${heading.replace(/\W+/g, '-').toLowerCase()}`

  useEffect(() => titleInput.current?.focus(), [])

  const problem = error instanceof ProblemError ? error.problem : null
  const errors = errorsFor(problem)
  const formError = formMessage(error, problem, notice ?? null, lookupError)

  function update(changes: Partial<WorkItemFormValues>) {
    setValues((current) => ({ ...current, ...changes }))
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setParentError(null)
    setLookupError(false)
    const parentKey = values.parentKey.trim().toUpperCase()
    let parentId: string | null = null
    if (parentKey && parentKey === initial.parentKey && initialParentId) {
      parentId = initialParentId
    } else if (parentKey) {
      setResolving(true)
      try {
        parentId = await findWorkItemId(parentKey)
      } catch {
        setLookupError(true)
        return
      } finally {
        setResolving(false)
      }
      if (!parentId) {
        setParentError(messages.workItemForm.parentNotFound(parentKey))
        return
      }
    }
    onSubmit({ ...values, parentKey, parentId })
  }

  const busy = pending || resolving

  return (
    <form aria-labelledby={headingId} className="grid gap-4 rounded-xl border border-border bg-card p-4" onSubmit={(e) => void submit(e)} noValidate>
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
          if (isWorkItemType(event.target.value)) update({ type: event.target.value })
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
      <FormField
        label={messages.workItemForm.parent}
        name="parentKey"
        autoComplete="off"
        className="w-40 font-mono uppercase"
        placeholder={messages.workItemForm.parentPlaceholder(projectKey)}
        value={values.parentKey}
        onChange={(event) => update({ parentKey: event.target.value.toUpperCase() })}
        hint={parentHint(values.type)}
        error={parentError ?? errors.parent}
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
