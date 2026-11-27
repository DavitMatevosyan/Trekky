import type { ReactNode } from 'react'
import { Link, NavLink, useLocation } from 'react-router'
import { messages } from '../i18n/messages.ts'
import { cn } from '../lib/cn.ts'
import { filterSearch } from '../workItems/workItemFilters.ts'

const tabClass = ({ isActive }: { isActive: boolean }) =>
  cn(
    'rounded-md px-3 py-1 text-sm font-medium underline-offset-4 hover:underline',
    isActive ? 'bg-muted text-foreground' : 'text-muted-foreground',
  )

/**
 * The project bar (wireframes: App shell): key and name, the Board, List, Docs (TREK-75) and Settings (TREK-74) tabs, and the screen's primary action. The
 * tabs carry the active filters, so switching views keeps them (TREK-54).
 */
export function ProjectHeader({ projectKey, projectName, action }: { projectKey: string; projectName: string; action?: ReactNode }) {
  const filters = filterSearch(useLocation().search)
  return (
    <div className="flex flex-wrap items-start justify-between gap-4">
      <div className="grid gap-1">
        <Link to="/" className="text-sm text-muted-foreground underline-offset-4 hover:underline">
          {messages.project.backToProjects}
        </Link>
        <h1 className="text-2xl font-semibold">
          <span className="font-mono">{projectKey}</span> · {projectName}
        </h1>
        <nav aria-label={messages.project.views} className="flex gap-1">
          <NavLink to={`/${projectKey}/board${filters}`} className={tabClass}>
            {messages.project.board}
          </NavLink>
          <NavLink to={`/${projectKey}${filters}`} end className={tabClass}>
            {messages.project.list}
          </NavLink>
          <NavLink to={`/${projectKey}/docs`} className={tabClass}>
            {messages.docs.tab}
          </NavLink>
          <NavLink to={`/${projectKey}/settings`} className={tabClass}>
            {messages.projectSettings.tab}
          </NavLink>
        </nav>
      </div>
      {action}
    </div>
  )
}
