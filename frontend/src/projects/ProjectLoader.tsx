import type { ReactNode } from 'react'
import { ProblemError } from '../api/problem.ts'
import { messages } from '../i18n/messages.ts'
import { useProjectByKey } from '../workItems/useWorkItems.ts'
import type { Project } from './useProjects.ts'

/** Loads the project behind a URL key and renders `children` with it, or the loading or not-found message. */
export function ProjectLoader({ projectKey, children }: { projectKey: string; children: (project: Project) => ReactNode }) {
  const project = useProjectByKey(projectKey)

  if (project.isPending) return <p className="text-muted-foreground">{messages.project.loading}</p>
  if (project.isError) {
    const notFound = project.error instanceof ProblemError && project.error.problem.status === 404
    return <p role="alert">{notFound ? messages.project.notFound : messages.common.unexpectedError}</p>
  }

  return children(project.data)
}
