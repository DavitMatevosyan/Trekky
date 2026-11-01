import { useId, type ComponentProps } from 'react'
import { cn } from '../lib/cn.ts'
import { Label } from './ui/Label.tsx'

export interface SelectFieldProps extends ComponentProps<'select'> {
  label: string
  options: readonly { value: string; label: string }[]
  error?: string
}

/** A labelled native select (keyboard and screen-reader friendly by default) with an optional error. */
export function SelectField({ label, options, error, className, ...props }: SelectFieldProps) {
  const id = useId()
  const errorId = `${id}-error`

  return (
    <div className="grid gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      <select
        id={id}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? errorId : undefined}
        className={cn(
          'h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm shadow-xs outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50 aria-invalid:border-destructive',
          className,
        )}
        {...props}
      >
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
      {error && (
        <p id={errorId} className="text-xs text-destructive">
          {error}
        </p>
      )}
    </div>
  )
}
