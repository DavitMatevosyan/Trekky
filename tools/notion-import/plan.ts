/** What the Notion snapshot (tasks.json) holds, and the pure rules for turning it into Trekky work items (TRK-21). */

export type Status = 'todo' | 'in_progress' | 'in_review' | 'done'

export interface SnapshotItem {
  /** The key the item had in Notion (TRK-6.1); kept in the title, because Trekky numbers its items itself. */
  notionKey?: string
  /** `task` unless said otherwise. */
  type?: 'task' | 'bug'
  title: string
  description: string
  status: Status
}

export interface SnapshotEpic {
  title: string
  description: string
  items: SnapshotItem[]
}

export interface Snapshot {
  source: string
  project: { key: string; name: string }
  epics: SnapshotEpic[]
}

/** The workflow in order (US-10): an import only moves items forward along it. */
export const WORKFLOW: readonly Status[] = ['todo', 'in_progress', 'in_review', 'done']

/** Trekky's limit on titles (US-06 AC1). */
export const TITLE_MAX_LENGTH = 200

/** The title in Trekky: the Notion key first, so the item is found by the key that branches and commits used. */
export function itemTitle(item: SnapshotItem): string {
  return item.notionKey ? `${item.notionKey} · ${item.title}` : item.title
}

/**
 * The Notion key at the start of a Trekky title (`TRK-6.1 · …` → `TRK-6.1`), or null. Imported items are matched by
 * it, so renaming one in Trekky does not make a second run import it again.
 */
export function notionKeyOf(title: string): string | null {
  return /^([A-Z][A-Z0-9]*-\d+(?:\.\d+)?) · /.exec(title)?.[1] ?? null
}

/** How a snapshot item is found among existing items: by its Notion key when it has one, else by its title. */
export function matchKey(item: SnapshotItem): string {
  return item.notionKey ?? itemTitle(item)
}

/** The match key of an existing Trekky item, the counterpart of {@link matchKey}. */
export function existingMatchKey(title: string): string {
  return notionKeyOf(title) ?? title
}

/** The description in Trekky: Notion's text, then where it came from. */
export function itemDescription(item: Pick<SnapshotItem, 'description' | 'notionKey'>, source: string): string {
  const key = item.notionKey ? ` (${item.notionKey})` : ''
  return `${item.description}\n\n_Imported from Notion${key}: ${source}._`
}

/**
 * The status changes that take an item from `from` to `to`, one workflow step at a time (each step is allowed: forward
 * one step). Empty when the item is already there or further on: an import never moves work back.
 */
export function stepsTo(from: string, to: Status): Status[] {
  const start = WORKFLOW.indexOf(from as Status)
  const end = WORKFLOW.indexOf(to)
  if (start === -1 || end <= start) return []
  return WORKFLOW.slice(start + 1, end + 1)
}

/** Everything wrong with a snapshot, so a bad file is refused before anything is written. */
export function validate(snapshot: Snapshot): string[] {
  const problems: string[] = []
  if (!/^[A-Z][A-Z0-9]{1,9}$/.test(snapshot.project.key)) problems.push(`Project key "${snapshot.project.key}" is not valid.`)
  // ADR-0016: TRK-<n> keeps meaning the Notion items, so the import never goes into a project keyed TRK.
  if (snapshot.project.key === 'TRK') problems.push('Project key "TRK" would give Notion\'s keys a second meaning (ADR-0016); use TREK.')
  const notionKeys = new Set<string>()
  const titles = new Set<string>()
  const check = (title: string) => {
    if (title.trim().length === 0) problems.push('A title is empty.')
    if (title.length > TITLE_MAX_LENGTH) problems.push(`"${title.slice(0, 40)}…" is longer than ${TITLE_MAX_LENGTH} characters.`)
    // Titles identify items on a second run, so each must be unique.
    if (titles.has(title)) problems.push(`"${title}" appears twice.`)
    titles.add(title)
  }
  for (const epic of snapshot.epics) {
    check(epic.title)
    for (const item of epic.items) {
      check(itemTitle(item))
      // Items are matched by their Notion key on a second run, so each must be unique too.
      if (item.notionKey !== undefined) {
        if (notionKeys.has(item.notionKey)) problems.push(`Notion key ${item.notionKey} appears twice.`)
        notionKeys.add(item.notionKey)
      }
      if (!WORKFLOW.includes(item.status)) problems.push(`"${itemTitle(item)}" has an unknown status "${String(item.status)}".`)
    }
  }
  return problems
}
