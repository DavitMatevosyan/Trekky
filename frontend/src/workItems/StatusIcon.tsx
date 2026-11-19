import type { ReactNode } from 'react'
import { cn } from '../lib/cn.ts'
import { statusStyle } from './workItemTypes.ts'

/**
 * One dot per status (TREK-59), so the workflow position never rests on colour alone (WCAG 1.4.1): To Do an empty ring,
 * In Progress a half-filled ring, In Review a ring with a dot, Done a filled disc with a check. Drawn on a 16px grid in
 * `currentColor` (the status accent unless `className` sets another); the check is drawn in `stroke-surface`.
 */
const SHAPES: Record<string, ReactNode | undefined> = {
  todo: <circle cx="8" cy="8" r="5.25" />,
  in_progress: (
    <>
      <circle cx="8" cy="8" r="5.25" />
      <path d="M8 2.75a5.25 5.25 0 0 1 0 10.5Z" fill="currentColor" />
    </>
  ),
  in_review: (
    <>
      <circle cx="8" cy="8" r="5.25" />
      <circle cx="8" cy="8" r="2" fill="currentColor" />
    </>
  ),
  done: (
    <>
      <circle cx="8" cy="8" r="6" fill="currentColor" />
      <path d="m5.5 8.25 1.75 1.75 3.25-3.75" className="stroke-surface" />
    </>
  ),
}

/** Any status the app does not know yet: a dashed ring. */
const UNKNOWN_SHAPE = <circle cx="8" cy="8" r="5.25" strokeDasharray="2.5 2" />

/** The status's icon; decorative, as the status's name always goes with it. */
export function StatusIcon({ status, className }: { status: string; className?: string }) {
  const shape = SHAPES[status]
  return (
    <svg
      viewBox="0 0 16 16"
      className={cn('size-3.5 shrink-0', statusStyle(status).icon, className)}
      fill="none"
      stroke="currentColor"
      strokeWidth="1.5"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      data-status-icon={shape ? status : 'unknown'}
    >
      {shape ?? UNKNOWN_SHAPE}
    </svg>
  )
}
