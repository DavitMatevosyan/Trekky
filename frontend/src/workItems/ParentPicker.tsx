import { useEffect, useId, useRef, useState, type KeyboardEvent } from 'react'
import { Label } from '../components/ui/Label.tsx'
import { messages } from '../i18n/messages.ts'
import { cn } from '../lib/cn.ts'
import { useResolveParent, type ParentChoice } from './useResolveParent.ts'
import { useParentCandidates } from './useWorkItems.ts'
import { typeLabel } from './workItemTypes.ts'

export type { ParentChoice } from './useResolveParent.ts'

const SEARCH_DELAY_MS = 250

function choiceLabel(choice: ParentChoice): string {
  return `${choice.key} · ${choice.title}`
}

/** `value` once it has stopped changing for `delayMs`. */
function useDebounced(value: string, delayMs: number): string {
  const [debounced, setDebounced] = useState(value)
  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs)
    return () => clearTimeout(timer)
  }, [value, delayMs])
  return debounced
}

/**
 * Picks a work item's parent (TRK-11.1): a combobox (WAI-ARIA 1.2 pattern, keyboard and screen-reader friendly) that
 * searches the project's items of the allowed types by key or title. Typing clears the current choice; leaving the
 * field without choosing takes an exact key of an allowed item, and otherwise keeps the text with an error. The text
 * still unchosen is reported through `onPendingChange`, so the form can resolve it before saving.
 */
export function ParentPicker({
  projectId,
  allowedTypes,
  excludeId,
  value,
  onChange,
  onPendingChange,
  hint,
  error,
}: {
  projectId: string
  allowedTypes: readonly string[]
  /** The item being edited: never its own parent. */
  excludeId?: string
  value: ParentChoice | null
  onChange: (choice: ParentChoice | null) => void
  /** The text typed but not chosen (trimmed), or `''`. */
  onPendingChange?: (text: string) => void
  hint: string
  error?: string
}) {
  const id = useId()
  const listId = `${id}-list`
  const hintId = `${id}-hint`
  const errorId = `${id}-error`
  const input = useRef<HTMLInputElement>(null)
  const blurLookup = useRef(0)
  const resolveParent = useResolveParent(projectId, excludeId)
  const [text, setText] = useState(value ? choiceLabel(value) : '')
  const [open, setOpen] = useState(false)
  const [active, setActive] = useState(-1)
  const [blurError, setBlurError] = useState<string | null>(null)
  const disabled = allowedTypes.length === 0

  // A lookup started for other allowed types (the type changed meanwhile) must not apply, nor its error stay.
  useEffect(() => {
    blurLookup.current += 1
  }, [allowedTypes])
  const [typesFor, setTypesFor] = useState(allowedTypes)
  if (typesFor !== allowedTypes) {
    setTypesFor(allowedTypes)
    setBlurError(null)
    // A type with no parent leaves nothing to type here.
    if (allowedTypes.length === 0) setText('')
  }

  // What the field shows follows the choice when it changes from outside (e.g. a type change drops it). Changes made
  // here update `shown` themselves, so this never overwrites what the user is typing.
  const [shown, setShown] = useState(value)
  if (shown !== value) {
    setShown(value)
    setText(value ? choiceLabel(value) : '')
  }

  const typed = value || disabled ? '' : text.trim()
  useEffect(() => onPendingChange?.(typed), [typed, onPendingChange])
  const search = useDebounced(typed, SEARCH_DELAY_MS)
  const candidates = useParentCandidates(projectId, allowedTypes, search, open && !disabled)
  const options = (candidates.data ?? []).filter((item) => item.id !== excludeId)
  // Until the list belongs to what is typed now, Enter must not pick from an older search.
  const settled = search === typed && !candidates.isPlaceholderData && candidates.isSuccess

  // A new list starts with nothing highlighted, so the active option always belongs to the list on screen.
  const [listFor, setListFor] = useState(candidates.data)
  if (listFor !== candidates.data) {
    setListFor(candidates.data)
    setActive(-1)
  }

  function choose(choice: ParentChoice | null) {
    blurLookup.current += 1
    setBlurError(null)
    onChange(choice)
    setShown(choice)
    setText(choice ? choiceLabel(choice) : '')
    setOpen(false)
    setActive(-1)
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
      // Choosing an option must not submit the form.
      event.preventDefault()
      const option = options[active]
      if (option && settled) choose(option)
    } else if (event.key === 'Escape' && open) {
      event.preventDefault()
      setOpen(false)
      setActive(-1)
      if (value) setText(choiceLabel(value))
    }
  }

  /** Leaving without choosing: an exact key of an allowed item in this project is chosen; anything else stays, with why. */
  async function onBlur() {
    setOpen(false)
    setActive(-1)
    if (value || !typed) return
    const lookup = ++blurLookup.current
    const result = await resolveParent(typed, allowedTypes)
    if (lookup !== blurLookup.current) return // the user chose, typed or changed the type meanwhile
    if ('choice' in result) choose(result.choice)
    else setBlurError(result.error)
  }

  const shownError = error ?? blurError ?? undefined
  const describedBy = [hintId, shownError && errorId].filter(Boolean).join(' ')
  const optionId = (itemId: string) => `${id}-option-${itemId}`
  const activeOption = open && active >= 0 ? options[active] : undefined
  const typesLabel = allowedTypes.map(typeLabel).join(messages.workItemForm.or)
  const emptyMessage = candidates.isError
    ? messages.workItemForm.parentOptionsLoadFailed
    : candidates.isSuccess
      ? messages.workItemForm.noParentMatches(typesLabel)
      : messages.workItemForm.searchingParents
  // mousedown on the list (an option, its scrollbar, the message) must not blur the input and close the list.
  const keepFocus = (event: { preventDefault: () => void }) => event.preventDefault()

  return (
    <div className="grid gap-1.5">
      <Label htmlFor={id}>{messages.workItemForm.parent}</Label>
      <div className="relative">
        <input
          ref={input}
          id={id}
          name="parent"
          role="combobox"
          aria-expanded={open && options.length > 0}
          aria-controls={listId}
          aria-autocomplete="list"
          aria-activedescendant={activeOption ? optionId(activeOption.id) : undefined}
          aria-describedby={describedBy}
          aria-invalid={shownError ? true : undefined}
          autoComplete="off"
          disabled={disabled}
          placeholder={disabled ? undefined : messages.workItemForm.parentPlaceholder}
          className="flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 pr-9 text-sm shadow-xs outline-none placeholder:text-muted-foreground focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50 aria-invalid:border-destructive"
          value={text}
          onChange={(event) => {
            blurLookup.current += 1
            setBlurError(null)
            setText(event.target.value)
            if (value) {
              setShown(null)
              onChange(null)
            }
            setOpen(true)
            setActive(-1)
          }}
          onFocus={() => setOpen(true)}
          onClick={() => setOpen(true)}
          onBlur={() => void onBlur()}
          onKeyDown={onKeyDown}
        />
        {value && !disabled && (
          <button
            type="button"
            aria-label={messages.workItemForm.clearParent}
            className="absolute inset-y-0 right-0 flex w-9 items-center justify-center rounded-md text-muted-foreground hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
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
          aria-label={messages.workItemForm.parent}
          hidden={!open || options.length === 0}
          onMouseDown={keepFocus}
          className="absolute z-10 mt-1 max-h-64 w-full overflow-y-auto rounded-md border border-border bg-card py-1 text-sm shadow-md"
        >
          {options.map((option, index) => (
            <li
              key={option.id}
              id={optionId(option.id)}
              role="option"
              aria-selected={index === active}
              className={cn('flex cursor-pointer gap-2 px-3 py-1.5', index === active && 'bg-muted outline-2 -outline-offset-2 outline-ring')}
              onMouseDown={(event) => {
                event.preventDefault()
                choose(option)
              }}
              onMouseEnter={() => setActive(index)}
            >
              <span className="font-mono font-semibold">{option.key}</span>
              <span className="text-muted-foreground">{typeLabel(option.type)}</span>
              <span className="truncate">{option.title}</span>
            </li>
          ))}
        </ul>
        {/* Only while the list is open, so the page keeps a single status region the rest of the time. */}
        {open && (
          <p role="status" className="sr-only">
            {options.length === 0 && !candidates.isPending ? emptyMessage : ''}
          </p>
        )}
        {open && options.length === 0 && !disabled && (
          <p
            aria-hidden="true"
            onMouseDown={keepFocus}
            className="absolute z-10 mt-1 w-full rounded-md border border-border bg-card px-3 py-2 text-sm text-muted-foreground shadow-md"
          >
            {emptyMessage}
          </p>
        )}
      </div>
      <p id={hintId} className="text-xs text-muted-foreground">
        {hint}
      </p>
      {shownError && (
        <p id={errorId} className="text-xs text-destructive">
          {shownError}
        </p>
      )}
    </div>
  )
}
