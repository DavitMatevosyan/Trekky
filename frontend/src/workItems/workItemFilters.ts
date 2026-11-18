import { WORK_ITEM_STATUSES, WORK_ITEM_TYPES } from './workItemTypes.ts'

/** The assignee value for the signed-in user (TREK-52). */
export const ASSIGNEE_ME = 'me'
/** The assignee value for items assigned to nobody (TREK-52). */
export const ASSIGNEE_NONE = 'none'

/**
 * What the list and the board are narrowed to (TREK-51): the API's filter parameters (TREK-52), and the same names in
 * the page's URL, so a filtered view can be shared and survives reload and back/forward.
 */
export interface WorkItemFilters {
  /** Only items whose own status is not Done. */
  open: boolean
  /** Any of these statuses; empty means all. */
  statuses: readonly string[]
  /** Any of these types; empty means all. */
  types: readonly string[]
  /** Any of these: {@link ASSIGNEE_ME}, {@link ASSIGNEE_NONE} or a user id; empty means anyone. */
  assignees: readonly string[]
  /** Only items under this epic (its key), at any depth. */
  parent: string | null
  /** Key prefix or words in the title. */
  q: string
  /** Keep epics in Done and everything under them; hidden by default. */
  showCompletedEpics: boolean
}

export const DEFAULT_FILTERS: WorkItemFilters = {
  open: false,
  statuses: [],
  types: [],
  assignees: [],
  parent: null,
  q: '',
  showCompletedEpics: false,
}

/** The URL parameters the filters own; anything else in the URL (the board's "Show all") is left alone. */
const PARAM = { open: 'open', status: 'status', type: 'type', assignee: 'assignee', parent: 'parent', q: 'q', completedEpics: 'completedEpics' } as const

/** The API's limits (WorkItemFilter): longer searches and more assignees are a 400. */
export const QUERY_MAX_LENGTH = 200
const ASSIGNEES_MAX = 50

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
/** A work item key as the API reads it (WorkItemKey), once upper-cased. */
const KEY = /^[A-Z][A-Z0-9]{1,9}-[1-9][0-9]{0,9}$/

const isAssignee = (value: string) => value === ASSIGNEE_ME || value === ASSIGNEE_NONE || UUID.test(value)

/** Each value once, in the order first given, keeping only those `allowed` accepts. */
function distinct(values: readonly string[], allowed: (value: string) => boolean): string[] {
  return [...new Set(values)].filter(allowed)
}

/**
 * The filters in a URL. Values the API would refuse (a typo in a shared link) are dropped, so the page shows what it
 * can instead of an error.
 */
export function readFilters(params: URLSearchParams): WorkItemFilters {
  const parent = params.get(PARAM.parent)?.trim().toUpperCase() ?? ''
  return {
    open: params.get(PARAM.open) === 'true',
    statuses: distinct(params.getAll(PARAM.status), (value) => WORK_ITEM_STATUSES.includes(value)),
    types: distinct(params.getAll(PARAM.type), (value) => (WORK_ITEM_TYPES as readonly string[]).includes(value)),
    assignees: distinct(params.getAll(PARAM.assignee), isAssignee).slice(0, ASSIGNEES_MAX),
    parent: KEY.test(parent) ? parent : null,
    q: (params.get(PARAM.q) ?? '').replaceAll('\0', '').slice(0, QUERY_MAX_LENGTH),
    showCompletedEpics: params.get(PARAM.completedEpics) === 'show',
  }
}

/** `params` with the filters written in; a filter at its default is left out, so a plain view has a plain URL. */
export function writeFilters(params: URLSearchParams, filters: WorkItemFilters): URLSearchParams {
  const next = new URLSearchParams(params)
  for (const name of Object.values(PARAM)) next.delete(name)
  if (filters.open) next.set(PARAM.open, 'true')
  for (const status of filters.statuses) next.append(PARAM.status, status)
  for (const type of filters.types) next.append(PARAM.type, type)
  for (const assignee of filters.assignees) next.append(PARAM.assignee, assignee)
  if (filters.parent) next.set(PARAM.parent, filters.parent)
  if (filters.q.trim()) next.set(PARAM.q, filters.q)
  if (filters.showCompletedEpics) next.set(PARAM.completedEpics, 'show')
  return next
}

/** The query parameters for GET work-items and GET board. */
export function filterQuery(filters: WorkItemFilters) {
  const q = filters.q.trim()
  return {
    ...(filters.open ? { open: 'true' } : {}),
    ...(filters.statuses.length > 0 ? { status: [...filters.statuses] } : {}),
    ...(filters.types.length > 0 ? { type: [...filters.types] } : {}),
    ...(filters.assignees.length > 0 ? { assignee: [...filters.assignees] } : {}),
    ...(filters.parent ? { parent: filters.parent } : {}),
    ...(q ? { q } : {}),
    ...(filters.showCompletedEpics ? { completedEpics: 'show' } : {}),
  }
}

/** True when the filters narrow nothing beyond the default (completed epics hidden). */
export function isDefaultFilters(filters: WorkItemFilters): boolean {
  return (
    !filters.open &&
    filters.statuses.length === 0 &&
    filters.types.length === 0 &&
    filters.assignees.length === 0 &&
    filters.parent === null &&
    filters.q.trim() === '' &&
    !filters.showCompletedEpics
  )
}

/** `values` with `value` added, or removed when it is there. */
export function toggle(values: readonly string[], value: string): string[] {
  return values.includes(value) ? values.filter((v) => v !== value) : [...values, value]
}
