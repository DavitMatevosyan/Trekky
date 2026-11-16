import { useEffect, useId } from 'react'
import { ProblemError } from '../api/problem.ts'
import { messages } from '../i18n/messages.ts'
import { useMembers } from './useMembers.ts'
import { useUpdateWorkItem, type VersionedWorkItem } from './useWorkItems.ts'

const text = messages.workItem
const NOBODY = ''

/** Why the assignee did not change, in words the user can act on. */
function errorMessage(error: Error): string {
  if (!(error instanceof ProblemError)) return messages.common.unexpectedError
  if (error.problem.status === 412) return text.assigneeStale
  if (error.problem.status === 422) return text.assigneeNotMember
  return messages.common.unexpectedError
}

/**
 * Who owns the item (US-13): one member of the organization, or nobody. Choosing saves right away (a merge patch with
 * If-Match) and reports the new assignee's name, or null for nobody, via `onAssigned`.
 */
export function AssigneeSelect({
  current,
  labelledBy,
  disabled = false,
  onAssigned,
}: {
  current: VersionedWorkItem
  /** The id of the visible label. */
  labelledBy: string
  disabled?: boolean
  onAssigned: (name: string | null) => void
}) {
  const errorId = `${useId()}-error`
  const { item } = current
  const members = useMembers()
  const save = useUpdateWorkItem(current)
  const { reset } = save
  useEffect(() => {
    if (disabled) reset()
  }, [disabled, reset])

  const people = (members.data ?? []).map((member) => member.user)
  // Someone no longer a member still shows as the current value until it is changed.
  const options = item.assignee && !people.some((person) => person.id === item.assignee?.id) ? [item.assignee, ...people] : people
  const value = save.isPending ? (save.variables.assigneeId ?? NOBODY) : (item.assignee?.id ?? NOBODY)
  const error = save.error ? errorMessage(save.error) : members.isError ? text.membersFailed : null

  return (
    <div className="grid gap-1">
      <select
        aria-labelledby={labelledBy}
        value={value}
        disabled={disabled || save.isPending || members.isPending}
        aria-busy={save.isPending || members.isPending || undefined}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? errorId : undefined}
        onChange={(event) => {
          const assigneeId = event.target.value === NOBODY ? null : event.target.value
          if (assigneeId === (item.assignee?.id ?? null)) return
          save.mutate({ assigneeId }, { onSuccess: (saved) => onAssigned(saved.item.assignee?.displayName ?? null) })
        }}
        className="h-8 w-full rounded-md border border-input bg-background px-2 text-sm shadow-xs outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50 aria-invalid:border-destructive"
      >
        <option value={NOBODY}>{text.nobody}</option>
        {options.map((person) => (
          <option key={person.id} value={person.id}>
            {person.displayName}
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
