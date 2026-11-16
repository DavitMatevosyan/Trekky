import { messages } from '../i18n/messages.ts'
import { useFindWorkItem } from './useWorkItems.ts'

/** The chosen parent, with what the parent field shows for it. */
export interface ParentChoice {
  id: string
  key: string
  type: string
  title: string
}

export type ParentResolution = { choice: ParentChoice } | { error: string }

const KEY_PATTERN = /^[A-Z][A-Z0-9]{1,9}-[1-9]\d{0,9}$/

/**
 * Turns text typed in the parent field but not chosen from the list into a parent (TRK-11.1): only an exact key of an
 * item in this project, of an allowed type and not the item itself. Anything else is an error for the field, so a
 * parent is never silently dropped or guessed.
 */
export function useResolveParent(projectId: string, excludeId?: string) {
  const findWorkItem = useFindWorkItem()
  return async (text: string, allowedTypes: readonly string[]): Promise<ParentResolution> => {
    const key = text.trim().toUpperCase()
    if (!KEY_PATTERN.test(key)) return { error: messages.workItemForm.parentNotChosen }
    let found
    try {
      found = await findWorkItem(key)
    } catch {
      return { error: messages.workItemForm.parentOptionsLoadFailed }
    }
    if (!found || found.projectId !== projectId) return { error: messages.workItemForm.parentNotFound(key) }
    if (found.id === excludeId || !allowedTypes.includes(found.type)) return { error: messages.workItemForm.parentNotAllowed(key) }
    return { choice: { id: found.id, key: found.key, type: found.type, title: found.title } }
  }
}
