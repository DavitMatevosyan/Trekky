import { useEffect, useId } from 'react'
import { ProblemError } from '../api/problem.ts'
import { messages } from '../i18n/messages.ts'
import { useChangeWorkItemStatus, type VersionedWorkItem } from './useWorkItems.ts'
import { statusLabel, WORK_ITEM_STATUSES } from './workItemTypes.ts'

/** Why a move failed, in words the user can act on. */
function errorMessage(error: Error): string {
  if (!(error instanceof ProblemError)) return messages.common.unexpectedError
  const { status, allowedTransitions } = error.problem
  if (status === 412) return messages.workItem.statusStale
  if (status === 422 && allowedTransitions && allowedTransitions.length > 0) {
    return messages.workItem.statusNotAllowed(allowedTransitions.map(statusLabel).join(messages.workItemForm.or))
  }
  return messages.common.unexpectedError
}

/**
 * The item's status as a select that offers only the moves the workflow allows from it (US-10; wireframes: "The
 * status dropdown only offers allowed transitions"). Choosing one saves it right away and reports it via `onMoved`.
 */
export function StatusSelect({
  current,
  labelledBy,
  disabled = false,
  onMoved,
}: {
  current: VersionedWorkItem
  /** The id of the visible label. */
  labelledBy: string
  disabled?: boolean
  onMoved: (status: string) => void
}) {
  const errorId = `${useId()}-error`
  const { item } = current
  const change = useChangeWorkItemStatus(current)
  // Opening the edit form starts other work; a failed move from before must not still look current next to it.
  const { reset } = change
  useEffect(() => {
    if (disabled) reset()
  }, [disabled, reset])
  // Workflow order, so the list reads the same from every status; a status the app does not know still shows as is.
  const known = WORK_ITEM_STATUSES.filter((status) => status === item.status || item.allowedTransitions.includes(status))
  const options = known.includes(item.status) ? known : [item.status, ...known]
  const error = change.error ? errorMessage(change.error) : null

  return (
    <div className="grid gap-1">
      <select
        aria-labelledby={labelledBy}
        // While saving, show where the item is going; afterwards the item itself says where it is.
        value={change.isPending ? change.variables : item.status}
        disabled={disabled || change.isPending || item.allowedTransitions.length === 0}
        aria-busy={change.isPending || undefined}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? errorId : undefined}
        onChange={(event) => {
          const to = event.target.value
          if (to !== item.status) change.mutate(to, { onSuccess: (moved) => onMoved(moved.item.status) })
        }}
        className="h-8 w-full rounded-md border border-input bg-background px-2 text-sm shadow-xs outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50 aria-invalid:border-destructive"
      >
        {options.map((status) => (
          <option key={status} value={status}>
            {statusLabel(status)}
          </option>
        ))}
      </select>
      {error && (
        <p id={errorId} role="alert" className="text-xs text-destructive">
          {error}
        </p>
      )}
    </div>
  )
}
