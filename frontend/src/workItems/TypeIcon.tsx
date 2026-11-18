import type { ReactNode } from 'react'
import { cn } from '../lib/cn.ts'
import { isWorkItemType, typeStyle, type WorkItemType } from './workItemTypes.ts'

/**
 * One shape per type (TREK-57), so the type never rests on colour alone (WCAG 1.4.1): Epic a lightning bolt, Story a
 * bookmark, Task a ticked square, SubTask a small square hanging off a line, Bug a bug, TechDebt a wrench. Drawn on a
 * 16px grid in `currentColor`, in the type's accent colour unless `className` sets another.
 */
const SHAPES: Record<WorkItemType, ReactNode> = {
  epic: <path d="M9.25 1.5 3.5 9h4.25l-1 5.5L12.5 7H8.25l1-5.5Z" />,
  story: <path d="M4 2.25h8v11.5l-4-2.75-4 2.75Z" />,
  task: (
    <>
      <rect x="2.25" y="2.25" width="11.5" height="11.5" rx="2" />
      <path d="m5.25 8.25 2 2 3.5-4.5" />
    </>
  ),
  subtask: (
    <>
      <path d="M3 2v6.75h3.5" />
      <rect x="7.25" y="6" width="6.25" height="6.25" rx="1.5" />
    </>
  ),
  bug: (
    <>
      <path d="M8 5.25a3 3 0 0 1 3 3V10a3 3 0 0 1-6 0V8.25a3 3 0 0 1 3-3Z" />
      <path d="M8 5.25V13M5 8H2.5M13.5 8H11M5 11H3M13 11h-2M6.25 4.5 5 2.75M9.75 4.5 11 2.75" />
    </>
  ),
  tech_debt: <path d="M10.75 2.25a3 3 0 0 0-2.9 3.75l-5.4 5.4a1.35 1.35 0 0 0 1.9 1.9l5.4-5.4a3 3 0 0 0 3.75-2.9l-1.9 1.4-1.7-.45-.45-1.7Z" />,
}

/** Any type the app does not know yet: a plain circle. */
const UNKNOWN_SHAPE = <circle cx="8" cy="8" r="4.5" />

/** The type's icon; decorative, as the type's name always goes with it (on screen or for screen readers). */
export function TypeIcon({ type, className }: { type: string; className?: string }) {
  return (
    <svg
      viewBox="0 0 16 16"
      className={cn('size-4 shrink-0', typeStyle(type).icon, className)}
      fill="none"
      stroke="currentColor"
      strokeWidth="1.5"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      data-type-icon={isWorkItemType(type) ? type : 'unknown'}
    >
      {isWorkItemType(type) ? SHAPES[type] : UNKNOWN_SHAPE}
    </svg>
  )
}
