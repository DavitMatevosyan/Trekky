import { useRef, useState, type ReactNode } from 'react'
import { Button } from '../components/ui/Button.tsx'
import { messages } from '../i18n/messages.ts'
import { useCreateWorkItem } from '../workItems/useWorkItems.ts'
import { WorkItemForm } from '../workItems/WorkItemForm.tsx'
import { ProjectHeader } from './ProjectHeader.tsx'

const EMPTY_ITEM = { type: 'task', title: '', descriptionMd: '', parent: null } as const

interface ProjectViewProps {
  projectId: string
  projectKey: string
  projectName: string
  children: ReactNode
}

/** What every view of a project shares (wireframes: App shell): the project bar with "New item" (US-06) and its form. */
export function ProjectView({ projectId, projectKey, projectName, children }: ProjectViewProps) {
  const createItem = useCreateWorkItem(projectId)
  const [creating, setCreating] = useState(false)
  const [announcement, setAnnouncement] = useState('')
  const newItemButton = useRef<HTMLButtonElement>(null)

  function closeForm() {
    setCreating(false)
    createItem.reset()
    // Return focus to where the user started (WCAG 2.4.3).
    requestAnimationFrame(() => newItemButton.current?.focus())
  }

  return (
    <section className="grid gap-6">
      <ProjectHeader
        projectKey={projectKey}
        projectName={projectName}
        action={
          !creating && (
            <Button ref={newItemButton} onClick={() => setCreating(true)}>
              <span aria-hidden="true">+</span> {messages.project.newItem}
            </Button>
          )
        }
      />

      {creating && (
        <WorkItemForm
          heading={messages.workItemForm.newTitle}
          projectId={projectId}
          initial={EMPTY_ITEM}
          submitLabel={messages.workItemForm.create}
          submittingLabel={messages.workItemForm.creating}
          pending={createItem.isPending}
          error={createItem.error}
          onCancel={closeForm}
          onSubmit={(values) =>
            createItem.mutate(
              { type: values.type, title: values.title, descriptionMd: values.descriptionMd || null, parentId: values.parent?.id ?? null },
              {
                onSuccess: (created) => {
                  setAnnouncement(messages.project.created(created.key, created.title))
                  closeForm()
                },
              },
            )
          }
        />
      )}

      <p role="status" className="sr-only">
        {announcement}
      </p>

      {children}
    </section>
  )
}
