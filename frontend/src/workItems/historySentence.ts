import { messages } from '../i18n/messages.ts'
import type { HistoryEvent } from './useWorkItemHistory.ts'
import { statusLabel, typeLabel } from './workItemTypes.ts'

const text = messages.workItemHistory

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null
}

function asText(value: unknown): string | null {
  return typeof value === 'string' ? value : null
}

/** "from → to" for a `{ from, to }` change; `null` when the change is not of that shape. */
function arrow(change: unknown, label: (value: string) => string): string | null {
  if (!isRecord(change)) return null
  const from = asText(change.from)
  const to = asText(change.to)
  return from !== null && to !== null ? text.arrow(label(from), label(to)) : null
}

/** A parent side of a change: `{ id, key }`, or `null` for no parent. */
function parentKey(side: unknown): string | null {
  if (side === null) return text.noValue
  return isRecord(side) ? asText(side.key) : null
}

function parentChange(change: unknown): string | null {
  if (!isRecord(change)) return null
  const from = parentKey(change.from)
  const to = parentKey(change.to)
  return from !== null && to !== null ? text.parentChange(text.arrow(from, to)) : null
}

function updatedFields(changes: Record<string, unknown>): string[] {
  const title = arrow(changes.title, text.quoted)
  const type = arrow(changes.type, typeLabel)
  const parent = parentChange(changes.parent)
  return [
    title && text.titleChange(title),
    type && text.typeChange(type),
    parent,
    changes.descriptionMd ? text.descriptionChange : null,
  ].filter((part) => part !== null)
}

/**
 * What an event did, in one sentence that follows the actor's name (US-09 AC2; docs/tech/activity-events.md). An event
 * type or schema version this code does not know, or a payload it cannot read, still shows as a generic change: history
 * never hides one, and never guesses at a newer version's meaning.
 */
export function historySentence(event: Pick<HistoryEvent, 'eventType' | 'schemaVersion' | 'payload'>): string {
  const { eventType, schemaVersion, payload } = event
  if (Number(schemaVersion) !== 1 || !isRecord(payload)) return text.unknownChange

  switch (eventType) {
    case 'WorkItemCreated': {
      const type = asText(payload.type)
      return type === null ? text.unknownChange : text.created(typeLabel(type))
    }
    case 'WorkItemStatusChanged': {
      const change = arrow(payload, statusLabel)
      return change === null ? text.unknownChange : text.statusChanged(change)
    }
    case 'WorkItemUpdated': {
      const fields = isRecord(payload.changes) ? updatedFields(payload.changes) : []
      return fields.length === 0 ? text.unknownChange : text.updated(fields.join(text.fieldSeparator))
    }
    default:
      return text.unknownChange
  }
}

/** Who made a change: their current name, or a stand-in when there is none (they left, or it was not a person). */
export function actorName(actor: HistoryEvent['actor']): string {
  if (actor.displayName) return actor.displayName
  return actor.type === 'user' ? text.formerMember : messages.appName
}
