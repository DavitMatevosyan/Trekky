import { useEffect, useId, useRef, useState, type RefObject } from 'react'
import { useMe } from '../auth/useMe.ts'
import { Button } from '../components/ui/Button.tsx'
import { messages } from '../i18n/messages.ts'
import { cn } from '../lib/cn.ts'
import { EpicFilter } from './EpicFilter.tsx'
import { MultiSelectFilter, type FilterOption } from './MultiSelectFilter.tsx'
import { useMembers } from './useMembers.ts'
import type { SetWorkItemFilters } from './useWorkItemFilters.ts'
import { DONE } from './boardMoves.ts'
import {
  ASSIGNEE_ME,
  ASSIGNEE_NONE,
  DEFAULT_FILTERS,
  isDefaultFilters,
  QUERY_MAX_LENGTH,
  toggle,
  type WorkItemFilters,
} from './workItemFilters.ts'
import { statusLabel, typeLabel, WORK_ITEM_STATUSES, WORK_ITEM_TYPES } from './workItemTypes.ts'

const SEARCH_DELAY_MS = 250

const text = messages.filters
const statusOptions: FilterOption[] = WORK_ITEM_STATUSES.map((status) => ({ value: status, label: statusLabel(status) }))
const typeOptions: FilterOption[] = WORK_ITEM_TYPES.map((type) => ({ value: type, label: typeLabel(type) }))

interface Chip {
  id: string
  label: string
  without: WorkItemFilters
}

/** Inputs that take no text: `/` on them still goes to the search box. */
const NON_TEXT_INPUTS = ['checkbox', 'radio', 'button', 'submit', 'reset']

/** True when `/` was pressed outside anything that takes text, so it can move to the search box. */
function isSearchShortcut(event: KeyboardEvent): boolean {
  if (event.key !== '/' || event.ctrlKey || event.metaKey || event.altKey || event.defaultPrevented) return false
  const target = event.target
  if (!(target instanceof HTMLElement)) return true
  if (target instanceof HTMLInputElement) return NON_TEXT_INPUTS.includes(target.type)
  return !(target.isContentEditable || target.closest('textarea, select, [role="combobox"]'))
}

interface FilterBarProps {
  projectId: string
  filters: WorkItemFilters
  onChange: SetWorkItemFilters
  /** The search box, for a page that moves the focus there (e.g. after its own "Clear filters"). */
  searchRef?: RefObject<HTMLInputElement | null>
}

/**
 * The filters above the list and the board (TREK-51, shared by TREK-53 and TREK-54): quick filters, Status, Type,
 * Assignee, Epic, a search box (`/` focuses it) and "Show completed epics"; the active ones as removable chips.
 */
export function FilterBar({ projectId, filters, onChange, searchRef }: FilterBarProps) {
  const searchId = useId()
  const hintId = `${searchId}-hint`
  const ownSearchBox = useRef<HTMLInputElement>(null)
  const searchBox = searchRef ?? ownSearchBox
  const me = useMe()
  const members = useMembers()

  // The search box writes to the URL once typing pauses; until then it shows what is typed.
  const [search, setSearch] = useState(filters.q)
  const searchTimer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)
  /** The search this box last wrote, until the URL has it: that change is its own, not one from outside. */
  const written = useRef<string | null>(null)
  /** Typing has written its first search: the rest of the burst replaces that history entry instead of adding more. */
  const typedEntry = useRef(false)
  const latest = useRef({ filters, onChange })
  useEffect(() => {
    latest.current = { filters, onChange }
  })
  useEffect(() => () => clearTimeout(searchTimer.current), [])
  useEffect(() => {
    if (written.current !== null && filters.q === written.current) {
      written.current = null
      return
    }
    // Changed from outside (a chip, "Clear all", back or forward): a search still waiting must not undo it.
    written.current = null
    clearTimeout(searchTimer.current)
    typedEntry.current = false
    setSearch(filters.q)
  }, [filters])

  useEffect(() => {
    function focusSearch(event: KeyboardEvent) {
      if (!isSearchShortcut(event)) return
      event.preventDefault()
      searchBox.current?.focus()
      searchBox.current?.select()
    }
    document.addEventListener('keydown', focusSearch)
    return () => document.removeEventListener('keydown', focusSearch)
  }, [searchBox])

  function typeSearch(value: string) {
    setSearch(value)
    clearTimeout(searchTimer.current)
    searchTimer.current = setTimeout(() => {
      const current = latest.current
      // Spaces alone change no result and no URL: no history entry either.
      if (current.filters.q.trim() === value.trim()) return
      written.current = value
      // The first search of a burst is a step Back can return from; the next keys only refine it.
      current.onChange({ ...current.filters, q: value }, { replace: typedEntry.current })
      typedEntry.current = true
    }, SEARCH_DELAY_MS)
  }

  /** Any change but typing. A search still waiting to be written goes with it, so neither undoes the other. */
  function change(next: WorkItemFilters) {
    clearTimeout(searchTimer.current)
    typedEntry.current = false
    onChange({ ...next, q: next.q !== filters.q ? next.q : search })
  }

  function clearAll() {
    clearTimeout(searchTimer.current)
    typedEntry.current = false
    setSearch('')
    onChange(DEFAULT_FILTERS)
    searchBox.current?.focus()
  }

  const myId = me.data?.user.id
  const everyone = (members.data ?? []).map((member) => member.user)
  const people = everyone.filter((person) => person.id !== myId)
  const personName = (value: string) => {
    if (value === ASSIGNEE_ME || value === myId) return text.me
    if (value === ASSIGNEE_NONE) return text.unassigned
    const name = everyone.find((person) => person.id === value)?.displayName
    return name ?? (members.isPending ? text.loadingPerson : text.unknownPerson)
  }
  const assigneeOptions: FilterOption[] = [
    { value: ASSIGNEE_ME, label: text.me },
    { value: ASSIGNEE_NONE, label: text.unassigned },
    ...people.map((person) => ({ value: person.id, label: person.displayName })),
    // A person given by id in a shared link (the signed-in user by name, apart from "Me", or someone no longer a
    // member) can still be unticked.
    ...filters.assignees
      .filter((value) => value !== ASSIGNEE_ME && value !== ASSIGNEE_NONE && !people.some((person) => person.id === value))
      .map((value) => ({ value, label: value === myId && me.data ? me.data.user.displayName : personName(value) })),
  ]

  const chips: Chip[] = [
    ...(filters.open ? [{ id: 'open', label: text.open, without: { ...filters, open: false } }] : []),
    ...filters.statuses.map((status) => ({
      id: `status-${status}`,
      label: text.chip(text.status, statusLabel(status)),
      without: { ...filters, statuses: filters.statuses.filter((s) => s !== status) },
    })),
    ...filters.types.map((type) => ({
      id: `type-${type}`,
      label: text.chip(text.type, typeLabel(type)),
      without: { ...filters, types: filters.types.filter((t) => t !== type) },
    })),
    ...filters.assignees.map((assignee) => ({
      id: `assignee-${assignee}`,
      label: text.chip(text.assignee, personName(assignee)),
      without: { ...filters, assignees: filters.assignees.filter((a) => a !== assignee) },
    })),
    ...(filters.parent ? [{ id: 'parent', label: text.chip(text.epic, filters.parent), without: { ...filters, parent: null } }] : []),
    ...(filters.q.trim() ? [{ id: 'q', label: text.chip(text.search, text.searchValue(filters.q.trim())), without: { ...filters, q: '' } }] : []),
    ...(filters.showCompletedEpics ? [{ id: 'completed-epics', label: text.completedEpicsShown, without: { ...filters, showCompletedEpics: false } }] : []),
  ]

  /**
   * After a chip goes, the focus moves to the chip now in its place, else to the search box (WCAG 2.4.3); once the URL
   * change has rendered, since the router commits it later.
   */
  const chipList = useRef<HTMLUListElement>(null)
  const focusChipAt = useRef<number | null>(null)
  useEffect(() => {
    const index = focusChipAt.current
    if (index === null) return
    focusChipAt.current = null
    const buttons = chipList.current?.querySelectorAll('button')
    const next = buttons?.[Math.min(index, buttons.length - 1)]
    if (next) next.focus()
    else searchBox.current?.focus()
  }, [filters, searchBox])
  function removeChip(index: number, chip: Chip) {
    focusChipAt.current = index
    change(chip.without)
  }

  const quick = [
    { label: text.open, pressed: filters.open, next: () => ({ ...filters, open: !filters.open }) },
    { label: text.mine, pressed: filters.assignees.includes(ASSIGNEE_ME), next: () => ({ ...filters, assignees: toggle(filters.assignees, ASSIGNEE_ME) }) },
    { label: text.unassigned, pressed: filters.assignees.includes(ASSIGNEE_NONE), next: () => ({ ...filters, assignees: toggle(filters.assignees, ASSIGNEE_NONE) }) },
  ]

  return (
    <div role="search" aria-label={text.region} className="grid gap-3">
      <div className="flex flex-wrap items-center gap-2">
        <div className="relative w-full sm:w-56">
          <label htmlFor={searchId} className="sr-only">
            {text.search}
          </label>
          <input
            ref={searchBox}
            id={searchId}
            type="search"
            aria-keyshortcuts="/"
            aria-describedby={hintId}
            placeholder={text.searchPlaceholder}
            autoComplete="off"
            className="flex h-8 w-full rounded-md border border-input bg-background px-3 pr-8 text-sm shadow-xs outline-none placeholder:text-muted-foreground focus-visible:ring-2 focus-visible:ring-ring"
            maxLength={QUERY_MAX_LENGTH}
            value={search}
            onChange={(event) => typeSearch(event.target.value)}
            onBlur={() => {
              typedEntry.current = false
            }}
          />
          <kbd aria-hidden="true" className="pointer-events-none absolute inset-y-1.5 right-2 rounded border border-border px-1 text-xs leading-4 text-muted-foreground">
            /
          </kbd>
          <span id={hintId} className="sr-only">
            {text.searchShortcut}
          </span>
        </div>

        <div role="group" aria-label={text.quick} className="flex flex-wrap gap-1">
          {quick.map((filter) => (
            <Button
              key={filter.label}
              type="button"
              variant="outline"
              size="sm"
              aria-pressed={filter.pressed}
              className={cn(filter.pressed && 'border-primary bg-primary text-primary-foreground hover:bg-primary/90')}
              onClick={() => change(filter.next())}
            >
              {filter.label}
            </Button>
          ))}
        </div>

        <MultiSelectFilter label={text.status} options={statusOptions} selected={filters.statuses} onChange={(statuses) => change({ ...filters, statuses })} />
        <MultiSelectFilter label={text.type} options={typeOptions} selected={filters.types} onChange={(types) => change({ ...filters, types })} />
        <MultiSelectFilter
          label={text.assignee}
          options={assigneeOptions}
          selected={filters.assignees}
          error={members.isError ? text.membersFailed : undefined}
          onChange={(assignees) => change({ ...filters, assignees })}
        />
        <EpicFilter
          projectId={projectId}
          value={filters.parent}
          // Everything under an epic in Done is hidden by default, so choosing one shows completed epics too.
          onChange={(epic) => change({ ...filters, parent: epic?.key ?? null, showCompletedEpics: filters.showCompletedEpics || epic?.status === DONE })}
        />

        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            className="size-4 accent-primary"
            checked={filters.showCompletedEpics}
            onChange={(event) => change({ ...filters, showCompletedEpics: event.target.checked })}
          />
          {text.showCompletedEpics}
        </label>
      </div>

      {!isDefaultFilters(filters) && (
        <div className="flex flex-wrap items-center gap-2">
          <ul ref={chipList} aria-label={text.active} className="flex flex-wrap gap-2">
            {chips.map((chip, index) => (
              <li key={chip.id}>
                <button
                  type="button"
                  aria-label={text.removeChip(chip.label)}
                  className="inline-flex items-center gap-1 rounded-full border border-border bg-muted px-3 py-0.5 text-sm hover:bg-background focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
                  onClick={() => removeChip(index, chip)}
                >
                  {chip.label}
                  <span aria-hidden="true">×</span>
                </button>
              </li>
            ))}
          </ul>
          <Button type="button" variant="link" size="link" onClick={clearAll}>
            {text.clearAll}
          </Button>
        </div>
      )}
    </div>
  )
}
