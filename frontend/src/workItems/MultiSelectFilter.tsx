import { useEffect, useId, useRef, useState, type KeyboardEvent } from 'react'
import { Button } from '../components/ui/Button.tsx'
import { messages } from '../i18n/messages.ts'
import { toggle } from './workItemFilters.ts'

export interface FilterOption {
  value: string
  label: string
}

/**
 * A filter that takes several values (TREK-53): a button that opens a group of checkboxes (WAI-ARIA disclosure pattern).
 * Escape, a click outside or leaving with Tab closes it; Escape returns the focus to the button.
 */
export function MultiSelectFilter({
  label,
  options,
  selected,
  onChange,
  error,
}: {
  label: string
  options: readonly FilterOption[]
  selected: readonly string[]
  onChange: (selected: string[]) => void
  /** Why the options could not be shown, e.g. the members failed to load. */
  error?: string
}) {
  const panelId = useId()
  const container = useRef<HTMLDivElement>(null)
  const button = useRef<HTMLButtonElement>(null)
  const [open, setOpen] = useState(false)

  useEffect(() => {
    if (!open) return
    const closeOutside = (event: MouseEvent) => {
      if (!container.current?.contains(event.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', closeOutside)
    return () => document.removeEventListener('mousedown', closeOutside)
  }, [open])

  function onKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    if (event.key === 'Escape' && open) {
      event.preventDefault()
      event.stopPropagation()
      setOpen(false)
      button.current?.focus()
    }
  }

  return (
    <div
      ref={container}
      className="relative"
      onKeyDown={onKeyDown}
      onBlur={(event) => {
        if (!container.current?.contains(event.relatedTarget)) setOpen(false)
      }}
    >
      <Button
        ref={button}
        type="button"
        variant="outline"
        size="sm"
        aria-expanded={open}
        aria-controls={open ? panelId : undefined}
        onClick={() => setOpen((current) => !current)}
      >
        {messages.filters.withCount(label, selected.length)}
        <span aria-hidden="true">▾</span>
      </Button>
      {/* Only while open, so the closed bar adds no second "Status" or "Type" label to the page. */}
      {open && (
        <div
          id={panelId}
          role="group"
          aria-label={label}
          className="absolute left-0 z-20 mt-1 grid max-h-72 min-w-48 gap-1 overflow-y-auto rounded-md border border-border bg-card p-2 text-sm shadow-md"
        >
          {error && <p className="px-1 text-xs text-destructive">{error}</p>}
          {options.map((option) => (
            <label key={option.value} className="flex cursor-pointer items-center gap-2 rounded px-1 py-1 hover:bg-muted">
              <input
                type="checkbox"
                className="size-4 accent-primary"
                checked={selected.includes(option.value)}
                onChange={() => onChange(toggle(selected, option.value))}
              />
              {option.label}
            </label>
          ))}
        </div>
      )}
    </div>
  )
}
