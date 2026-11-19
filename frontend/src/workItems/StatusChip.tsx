import { cn } from '../lib/cn.ts'
import { StatusIcon } from './StatusIcon.tsx'
import { statusLabel, statusStyle } from './workItemTypes.ts'

/** A work item's status at a glance (TREK-59): its dot and label on a quiet tint. */
export function StatusChip({ status, className }: { status: string; className?: string }) {
  return (
    <span className={cn('inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium whitespace-nowrap', statusStyle(status).chip, className)}>
      <StatusIcon status={status} />
      {statusLabel(status)}
    </span>
  )
}
