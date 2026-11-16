import type { ReactNode } from 'react'
import { Link, NavLink } from 'react-router'
import { messages } from '../i18n/messages.ts'
import { cn } from '../lib/cn.ts'

const tabClass = ({ isActive }: { isActive: boolean }) =>
  cn(
    'rounded-md px-3 py-1 text-sm font-medium underline-offset-4 hover:underline',
    isActive ? 'bg-muted text-foreground' : 'text-muted-foreground',
  )

/** The project bar (wireframes: App shell): key and name, the Board and List tabs, and the screen's primary action. */
export function ProjectHeader({ projectKey, projectName, action }: { projectKey: string; projectName: string; action?: ReactNode }) {
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
          <NavLink to={`/${projectKey}/board`} className={tabClass}>
            {messages.project.board}
          </NavLink>
          <NavLink to={`/${projectKey}`} end className={tabClass}>
            {messages.project.list}
          </NavLink>
        </nav>
      </div>
      {action}
    </div>
  )
}
