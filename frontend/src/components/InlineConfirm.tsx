import { useEffect, useRef } from 'react'
import { Button } from './ui/Button.tsx'

interface InlineConfirmProps {
  message: string
  confirmLabel: string
  pendingLabel: string
  cancelLabel: string
  pending: boolean
  onConfirm: () => void
  onCancel: () => void
}

/**
 * A question with Confirm and Cancel in place of the button that asked it, for actions that remove something. Focus
 * moves to Cancel, the safe choice (WCAG 2.4.3).
 */
export function InlineConfirm({ message, confirmLabel, pendingLabel, cancelLabel, pending, onConfirm, onCancel }: InlineConfirmProps) {
  const cancel = useRef<HTMLButtonElement>(null)
  useEffect(() => cancel.current?.focus(), [])

  return (
    <div role="group" aria-label={message} className="grid gap-2 rounded-md border border-border bg-muted/50 p-3 text-sm">
      <p>{message}</p>
      <div className="flex flex-wrap gap-2">
        <Button size="sm" onClick={onConfirm} disabled={pending}>
          {pending ? pendingLabel : confirmLabel}
        </Button>
        <Button ref={cancel} size="sm" variant="outline" onClick={onCancel} disabled={pending}>
          {cancelLabel}
        </Button>
      </div>
    </div>
  )
}
