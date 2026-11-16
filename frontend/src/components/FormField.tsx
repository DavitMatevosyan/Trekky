import { useId, type ComponentProps } from 'react'
import { Input } from './ui/Input.tsx'
import { Label } from './ui/Label.tsx'

export interface FormFieldProps extends ComponentProps<'input'> {
  label: string
  hint?: string
  error?: string
}

/** A labelled input with an optional hint and error, wired up with aria-describedby for screen readers. */
export function FormField({ label, hint, error, ...inputProps }: FormFieldProps) {
  const id = useId()
  const hintId = `${id}-hint`
  const errorId = `${id}-error`
  const describedBy = [hint && hintId, error && errorId].filter(Boolean).join(' ') || undefined

  return (
    <div className="grid gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      <Input id={id} aria-invalid={error ? true : undefined} aria-describedby={describedBy} {...inputProps} />
      {hint && (
        <p id={hintId} className="text-xs text-muted-foreground">
          {hint}
        </p>
      )}
      {error && (
        <p id={errorId} className="text-xs text-destructive">
          {error}
        </p>
      )}
    </div>
  )
}
