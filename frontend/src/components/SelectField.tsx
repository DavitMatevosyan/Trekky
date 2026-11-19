import { useId, type ComponentProps, type ReactNode } from 'react'
import { cn } from '../lib/cn.ts'
import { Label } from './ui/Label.tsx'

export interface SelectFieldProps extends ComponentProps<'select'> {
  label: string
  options: readonly { value: string; label: string }[]
  error?: string
  /** Shown inside the field on the left, e.g. the chosen type's icon (decorative). */
  icon?: ReactNode
}

/**
 * A labelled native select (keyboard and screen-reader friendly by default) with an optional error and icon.
 * `className` sizes the field (the select and its icon), not the label.
 */
export function SelectField({ label, options, error, icon, className, ...props }: SelectFieldProps) {
  const id = useId()
  const errorId = `${id}-error`

  return (
    <div className="grid gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      {/* The icon fades with a disabled select. */}
      <div className={cn('relative has-[select:disabled]:[&>span]:opacity-50', className)}>
        {icon && <span className="pointer-events-none absolute inset-y-0 left-2.5 flex items-center">{icon}</span>}
        <select
          id={id}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? errorId : undefined}
          className={cn(
            'h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm shadow-xs outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50 aria-invalid:border-destructive',
            icon && 'pl-8',
          )}
          {...props}
        >
          {options.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </select>
      </div>
      {error && (
        <p id={errorId} className="text-xs text-destructive">
          {error}
        </p>
      )}
    </div>
  )
}
