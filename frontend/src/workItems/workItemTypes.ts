import { messages } from '../i18n/messages.ts'

/** The six types, top of the hierarchy first (US-06 AC1); values as the API sends them. */
export const WORK_ITEM_TYPES = ['epic', 'story', 'task', 'subtask', 'bug', 'tech_debt'] as const
export type WorkItemType = (typeof WORK_ITEM_TYPES)[number]

/** Which parent types each type allows (US-06 AC4). The API enforces the same table; this only drives hints. */
export const ALLOWED_PARENT_TYPES: Record<WorkItemType, readonly WorkItemType[]> = {
  epic: [],
  story: ['epic'],
  task: ['epic', 'story'],
  subtask: ['story', 'task'],
  bug: ['epic', 'story'],
  tech_debt: ['epic'],
}

export function isWorkItemType(value: string): value is WorkItemType {
  return (WORK_ITEM_TYPES as readonly string[]).includes(value)
}

/** Label for a type value; unknown values are shown as sent. */
export function typeLabel(type: string): string {
  return isWorkItemType(type) ? messages.workItemTypes[type] : type
}

/** Label for a status value; unknown values are shown as sent. */
export function statusLabel(status: string): string {
  return messages.workItemStatuses[status] ?? status
}

/** "Parent: Epic or Story (optional)" and the like, for the parent field's hint. */
export function parentHint(type: WorkItemType): string {
  const allowed = ALLOWED_PARENT_TYPES[type]
  if (allowed.length === 0) return messages.workItemForm.noParentAllowed(typeLabel(type))
  const labels = allowed.map(typeLabel).join(messages.workItemForm.or)
  return type === 'subtask' ? messages.workItemForm.parentRequired(labels) : messages.workItemForm.parentOptional(labels)
}
