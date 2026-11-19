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

/** The workflow's statuses in order (US-10 AC1); the API decides which moves are allowed. */
export const WORK_ITEM_STATUSES: readonly string[] = ['todo', 'in_progress', 'in_review', 'done']

export function isWorkItemType(value: string): value is WorkItemType {
  return (WORK_ITEM_TYPES as readonly string[]).includes(value)
}

/** Label for a type value; unknown values are shown as sent. */
export function typeLabel(type: string): string {
  return isWorkItemType(type) ? messages.workItemTypes[type] : type
}

/**
 * How a type looks (TREK-57, ADR-0025): Tailwind classes on the type's theme tokens in index.css. `token` names the
 * tokens (`--type-<token>-{tint,accent,text}`); the class names are spelled out so Tailwind finds them.
 */
export interface TypeStyle {
  /** The tokens' name, or null for the neutral style of an unknown type. */
  token: string | null
  /** Tinted background, type-coloured text and a 1px border in the accent. */
  badge: string
  /** The icon in the accent colour. */
  icon: string
  /** The 3px left stripe on cards and rows. */
  stripe: string
  /** The faint background of a row or card that should stand out (an Epic). */
  tint: string
}

const TYPE_STYLES: Record<WorkItemType, TypeStyle> = {
  epic: {
    token: 'epic',
    badge: 'bg-type-epic-tint text-type-epic-text border-type-epic-accent',
    icon: 'text-type-epic-accent',
    stripe: 'border-l-[3px] border-l-type-epic-accent',
    tint: 'bg-type-epic-tint',
  },
  story: {
    token: 'story',
    badge: 'bg-type-story-tint text-type-story-text border-type-story-accent',
    icon: 'text-type-story-accent',
    stripe: 'border-l-[3px] border-l-type-story-accent',
    tint: 'bg-type-story-tint',
  },
  task: {
    token: 'task',
    badge: 'bg-type-task-tint text-type-task-text border-type-task-accent',
    icon: 'text-type-task-accent',
    stripe: 'border-l-[3px] border-l-type-task-accent',
    tint: 'bg-type-task-tint',
  },
  subtask: {
    token: 'subtask',
    badge: 'bg-type-subtask-tint text-type-subtask-text border-type-subtask-accent',
    icon: 'text-type-subtask-accent',
    stripe: 'border-l-[3px] border-l-type-subtask-accent',
    tint: 'bg-type-subtask-tint',
  },
  bug: {
    token: 'bug',
    badge: 'bg-type-bug-tint text-type-bug-text border-type-bug-accent',
    icon: 'text-type-bug-accent',
    stripe: 'border-l-[3px] border-l-type-bug-accent',
    tint: 'bg-type-bug-tint',
  },
  tech_debt: {
    token: 'tech-debt',
    badge: 'bg-type-tech-debt-tint text-type-tech-debt-text border-type-tech-debt-accent',
    icon: 'text-type-tech-debt-accent',
    stripe: 'border-l-[3px] border-l-type-tech-debt-accent',
    tint: 'bg-type-tech-debt-tint',
  },
}

/** An unknown type (one the API added later) looks neutral rather than borrowing another type's colour. */
const NEUTRAL_STYLE: TypeStyle = {
  token: null,
  badge: 'bg-muted text-muted-foreground border-border',
  icon: 'text-muted-foreground',
  // input, not border: a stripe still meets 3:1 against the panels when the type is unknown.
  stripe: 'border-l-[3px] border-l-input',
  tint: '',
}

/** The look of a type value; unknown values get the neutral style. */
export function typeStyle(type: string): TypeStyle {
  return isWorkItemType(type) ? TYPE_STYLES[type] : NEUTRAL_STYLE
}

/**
 * A list row's background and hover by type (TREK-58): an Epic sits on a faint tint of its colour, so epics stand out
 * from their children, and darkens to the full tint on hover (the tint is as light as `muted`, the usual hover).
 */
export function typeRow(type: string): string {
  // No hover ring here: on a table row the dividers and the stripe hide two of its sides.
  return type === 'epic' ? 'bg-type-epic-tint/50 hover:bg-type-epic-tint' : 'hover:bg-muted'
}

/** On a card the tint alone barely changes on hover, so an Epic card also gets a thin ring in its accent. */
const EPIC_HOVER_RING = 'hover:ring-1 hover:ring-inset hover:ring-type-epic-accent'

/**
 * A board card's background and hover by type (TREK-58): an Epic a little more tinted than its row, a SubTask flat on
 * the column (a step quieter than a Task), the rest raised.
 */
export function typeCard(type: string): string {
  if (type === 'epic') return `bg-type-epic-tint/70 hover:bg-type-epic-tint ${EPIC_HOVER_RING}`
  if (type === 'subtask') return 'bg-surface hover:bg-muted'
  return 'bg-surface-raised hover:bg-muted'
}

/**
 * The card being dragged (TREK-58): it floats above other cards, so it is opaque (an Epic at its full tint) and has no
 * hover, looking the same whether it is dragged by mouse, touch or keyboard.
 */
export function typeOverlay(type: string): string {
  if (type === 'epic') return 'bg-type-epic-tint'
  if (type === 'subtask') return 'bg-surface'
  return 'bg-surface-raised'
}

/**
 * The type's 3px left stripe, for a card or row (TREK-57). Put it after any all-sides border colour (`border-border`)
 * in `cn(...)`: tailwind-merge drops a side colour that comes before an all-sides one.
 */
export function typeAccent(type: string): string {
  return typeStyle(type).stripe
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
