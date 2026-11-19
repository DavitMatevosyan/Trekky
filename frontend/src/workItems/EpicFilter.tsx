import { useId, useRef, useState, type KeyboardEvent } from 'react'
import { messages } from '../i18n/messages.ts'
import { cn } from '../lib/cn.ts'
import { useDebounced } from '../lib/useDebounced.ts'
import { TypeIcon } from './TypeIcon.tsx'
import { useParentCandidates, useWorkItem } from './useWorkItems.ts'

const SEARCH_DELAY_MS = 250
const EPIC: readonly string[] = ['epic']

/** The chosen epic: its key, and its status, since an epic in Done needs completed epics shown. */
export interface EpicChoice {
  key: string
  status: string
}

/**
 * The Epic filter (TREK-53): a combobox (WAI-ARIA 1.2 pattern) that searches the project's epics by key or title, like
 * the parent picker. Choosing one filters to everything under it; leaving without choosing keeps the current epic.
 */
export function EpicFilter({
  projectId,
  value,
  onChange,
}: {
  projectId: string
  value: string | null
  onChange: (epic: EpicChoice | null) => void
}) {
  const id = useId()
  const listId = `${id}-list`
  const input = useRef<HTMLInputElement>(null)
  const [text, setText] = useState('')
  const [editing, setEditing] = useState(false)
  const [open, setOpen] = useState(false)
  const [active, setActive] = useState(-1)

  // The chosen epic's title, for the field; the key alone until it loads or if it is gone.
  const chosen = useWorkItem(value ?? '', value !== null)
  const chosenLabel = value ? (chosen.data ? `${value} · ${chosen.data.item.title}` : value) : ''

  const search = useDebounced(editing ? text.trim() : '', SEARCH_DELAY_MS)
  const candidates = useParentCandidates(projectId, EPIC, search, open)
  const options = candidates.data ?? []
  const settled = search === (editing ? text.trim() : '') && !candidates.isPlaceholderData && candidates.isSuccess

  // A new list starts with nothing highlighted, so the active option always belongs to the list on screen.
  const [listFor, setListFor] = useState(candidates.data)
  if (listFor !== candidates.data) {
    setListFor(candidates.data)
    setActive(-1)
  }

  function close() {
    setOpen(false)
    setActive(-1)
    setEditing(false)
    setText('')
  }

  function choose(epic: EpicChoice | null) {
    close()
    if ((epic?.key ?? null) !== value) onChange(epic)
  }

  function onKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault()
      if (!open) {
        setOpen(true)
        return
      }
      if (options.length === 0) return
      const step = event.key === 'ArrowDown' ? 1 : -1
      setActive((current) => (current + step + options.length) % options.length)
    } else if (event.key === 'Enter' && open) {
      event.preventDefault()
      const option = options[active]
      if (option && settled) choose(option)
    } else if (event.key === 'Escape' && open) {
      event.preventDefault()
      event.stopPropagation()
      close()
    }
  }

  const optionId = (itemId: string) => `${id}-option-${itemId}`
  const activeOption = open && active >= 0 ? options[active] : undefined
  const emptyMessage = candidates.isError
    ? messages.filters.epicsLoadFailed
    : candidates.isSuccess
      ? messages.filters.noEpicMatches
      : messages.filters.searchingEpics
  // mousedown on the list must not blur the input and close the list.
  const keepFocus = (event: { preventDefault: () => void }) => event.preventDefault()

  return (
    <div className="relative w-full sm:w-56">
      <label htmlFor={id} className="sr-only">
        {messages.filters.epic}
      </label>
      <input
        ref={input}
        id={id}
        role="combobox"
        aria-expanded={open && options.length > 0}
        aria-controls={listId}
        aria-autocomplete="list"
        aria-activedescendant={activeOption ? optionId(activeOption.id) : undefined}
        autoComplete="off"
        placeholder={messages.filters.epicPlaceholder}
        className={cn(
          'flex h-8 w-full rounded-md border border-input bg-background px-3 text-sm shadow-xs outline-none placeholder:text-muted-foreground focus-visible:ring-2 focus-visible:ring-ring',
          value && 'pr-8',
        )}
        value={editing ? text : chosenLabel}
        onChange={(event) => {
          setEditing(true)
          setText(event.target.value)
          setOpen(true)
          setActive(-1)
        }}
        onFocus={() => setOpen(true)}
        onClick={() => setOpen(true)}
        onBlur={close}
        onKeyDown={onKeyDown}
      />
      {value && (
        <button
          type="button"
          aria-label={messages.filters.clearEpic}
          className="absolute inset-y-0 right-0 flex w-8 items-center justify-center rounded-md text-muted-foreground hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
          onClick={() => {
            choose(null)
            // The button disappears with the choice; keep keyboard users in the field (WCAG 2.4.3).
            input.current?.focus()
          }}
        >
          <span aria-hidden="true">×</span>
        </button>
      )}
      <ul
        id={listId}
        role="listbox"
        aria-label={messages.filters.epic}
        hidden={!open || options.length === 0}
        onMouseDown={keepFocus}
        className="absolute z-20 mt-1 max-h-64 w-full min-w-64 overflow-y-auto rounded-md border border-border bg-surface-raised py-1 text-sm shadow-md"
      >
        {options.map((option, index) => (
          <li
            key={option.id}
            id={optionId(option.id)}
            role="option"
            aria-selected={index === active}
            className={cn('flex cursor-pointer items-center gap-2 px-3 py-1.5', index === active && 'bg-muted outline-2 -outline-offset-2 outline-ring')}
            onMouseDown={(event) => {
              event.preventDefault()
              choose(option)
            }}
            onMouseEnter={() => setActive(index)}
          >
            <TypeIcon type={option.type} className="size-3.5" />
            <span className="font-mono font-semibold">{option.key}</span>
            <span className="truncate">{option.title}</span>
          </li>
        ))}
      </ul>
      {open && (
        <p role="status" className="sr-only">
          {options.length === 0 && !candidates.isPending ? emptyMessage : ''}
        </p>
      )}
      {open && options.length === 0 && (
        <p
          aria-hidden="true"
          onMouseDown={keepFocus}
          className="absolute z-20 mt-1 w-full rounded-md border border-border bg-surface-raised px-3 py-2 text-sm text-muted-foreground shadow-md"
        >
          {emptyMessage}
        </p>
      )}
    </div>
  )
}
