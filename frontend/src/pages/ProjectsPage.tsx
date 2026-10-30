import { useRef, useState } from 'react'
import { useMe } from '../auth/useMe.ts'
import { Button } from '../components/ui/Button.tsx'
import { messages } from '../i18n/messages.ts'
import { NewProjectForm } from '../projects/NewProjectForm.tsx'
import { useProjects } from '../projects/useProjects.ts'

/** The start page after login: the organization's projects by name (US-05 AC3, wireframes: Projects). */
export function ProjectsPage() {
  const me = useMe()
  const projects = useProjects()
  const [creating, setCreating] = useState(false)
  const [announcement, setAnnouncement] = useState('')
  const newProjectButton = useRef<HTMLButtonElement>(null)

  const items = projects.data?.pages.flatMap((page) => page.items) ?? []

  function closeForm() {
    setCreating(false)
    // Return focus to where the user started (WCAG 2.4.3).
    requestAnimationFrame(() => newProjectButton.current?.focus())
  }

  return (
    <section className="grid gap-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="grid gap-1">
          <h1 className="text-2xl font-semibold">{messages.projects.title}</h1>
          {me.isError && <p role="alert">{messages.common.unexpectedError}</p>}
          {me.data && (
            <p data-testid="org-summary" className="text-sm text-muted-foreground">
              {messages.projects.welcome(me.data.organization.name, messages.roles[me.data.role] ?? me.data.role)}
            </p>
          )}
        </div>
        {!creating && (
          <Button ref={newProjectButton} onClick={() => setCreating(true)}>
            <span aria-hidden="true">+</span> {messages.projects.newProject}
          </Button>
        )}
      </div>

      {creating && (
        <NewProjectForm
          onCancel={closeForm}
          onCreated={(project) => {
            setAnnouncement(messages.projects.created(project.key, project.name))
            closeForm()
          }}
        />
      )}

      <p role="status" className="sr-only">
        {announcement}
      </p>

      {projects.isPending && <p className="text-muted-foreground">{messages.projects.loading}</p>}
      {projects.isError && <p role="alert">{messages.common.unexpectedError}</p>}
      {projects.isSuccess && items.length === 0 && <p className="text-muted-foreground">{messages.projects.empty}</p>}

      {items.length > 0 && (
        <ul aria-label={messages.projects.title} className="divide-y divide-border rounded-xl border border-border">
          {items.map((project) => (
            <li key={project.id} className="flex items-center gap-4 px-4 py-3">
              <span className="w-16 font-mono text-sm font-semibold">{project.key}</span>
              <span className="flex-1">{project.name}</span>
              <span className="text-sm text-muted-foreground">{messages.projects.itemCount(Number(project.itemCount))}</span>
            </li>
          ))}
        </ul>
      )}

      {projects.hasNextPage && (
        <Button
          variant="outline"
          className="justify-self-center"
          onClick={() => void projects.fetchNextPage()}
          disabled={projects.isFetchingNextPage}
        >
          {messages.projects.loadMore}
        </Button>
      )}
    </section>
  )
}
