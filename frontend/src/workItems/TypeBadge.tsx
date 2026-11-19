import { cn } from '../lib/cn.ts'
import { TypeIcon } from './TypeIcon.tsx'
import { typeLabel, typeStyle } from './workItemTypes.ts'

/**
 * A work item's type at a glance (TREK-57): icon and label on the type's tint, in the type's colours. `compact` shows
 * the icon alone, for tight places like board cards; its label stays there for screen readers and as a tooltip.
 */
export function TypeBadge({
  type,
  compact = false,
  labelId,
  className,
}: {
  type: string
  compact?: boolean
  /** An id for the compact badge's label, so a nearby control can be described by it. */
  labelId?: string
  className?: string
}) {
  const style = typeStyle(type)
  const label = typeLabel(type)
  if (compact) {
    return (
      <span title={label} className={cn('inline-flex size-5 shrink-0 items-center justify-center rounded-md border', style.badge, className)}>
        <TypeIcon type={type} className="size-3.5" />
        <span id={labelId} className="sr-only">
          {label}
        </span>
      </span>
    )
  }
  return (
    <span className={cn('inline-flex items-center gap-1 rounded-md border px-1.5 py-0.5 text-xs font-medium whitespace-nowrap', style.badge, className)}>
      <TypeIcon type={type} className="size-3.5" />
      {label}
    </span>
  )
}
