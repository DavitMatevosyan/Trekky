import { useEffect, useRef, useState, type FormEvent } from 'react'
import { fieldErrors, formLevelError, ProblemError, type ProblemDetails } from '../api/problem.ts'
import { FormError } from '../components/FormError.tsx'
import { FormField } from '../components/FormField.tsx'
import { Button } from '../components/ui/Button.tsx'
import { messages } from '../i18n/messages.ts'
import { suggestProjectKey } from './suggestProjectKey.ts'
import { useCreateProject, type Project } from './useProjects.ts'

const FIELDS = ['name', 'key']

/** The message above the form; a taken key is shown on the key field instead. */
function formErrorFor(error: Error | null, problem: ProblemDetails | null, keyTaken: string | undefined): string | null {
  if (!error || keyTaken) return null
  if (!problem) return messages.common.unexpectedError
  return formLevelError(problem, FIELDS)
}

/**
 * "New project" (wireframes: Projects): name and key. The key follows the name's suggestion until the user edits
 * it (US-05 AC2); a taken key (409) is shown on the key field.
 */
export function NewProjectForm({ onCreated, onCancel }: { onCreated: (project: Project) => void; onCancel: () => void }) {
  const createProject = useCreateProject()
  const [name, setName] = useState('')
  const [key, setKey] = useState('')
  const [keyEdited, setKeyEdited] = useState(false)
  const nameInput = useRef<HTMLInputElement>(null)

  useEffect(() => nameInput.current?.focus(), [])

  function onNameChange(value: string) {
    setName(value)
    if (!keyEdited) setKey(suggestProjectKey(value))
  }

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    createProject.mutate({ name, key }, { onSuccess: onCreated })
  }

  const problem = createProject.error instanceof ProblemError ? createProject.error.problem : null
  const errors = problem ? fieldErrors(problem) : {}
  const keyTaken = problem?.status === 409 ? (problem.detail ?? undefined) : undefined
  const formError = formErrorFor(createProject.error, problem, keyTaken)

  return (
    <form aria-labelledby="new-project-title" className="grid gap-4 rounded-xl border border-border bg-card p-4" onSubmit={onSubmit} noValidate>
      <h2 id="new-project-title" className="text-lg font-semibold">
        {messages.newProject.title}
      </h2>
      <FormError message={formError} />
      <FormField
        ref={nameInput}
        label={messages.newProject.name}
        name="name"
        autoComplete="off"
        required
        maxLength={80}
        value={name}
        onChange={(event) => onNameChange(event.target.value)}
        error={errors.name}
      />
      <FormField
        label={messages.newProject.key}
        name="key"
        autoComplete="off"
        required
        maxLength={10}
        className="w-32 font-mono uppercase"
        value={key}
        onChange={(event) => {
          setKeyEdited(true)
          setKey(event.target.value.toUpperCase())
        }}
        hint={messages.newProject.keyHint}
        error={errors.key ?? keyTaken}
      />
      <div className="flex justify-end gap-2">
        <Button type="button" variant="outline" onClick={onCancel}>
          {messages.newProject.cancel}
        </Button>
        <Button type="submit" disabled={createProject.isPending}>
          {createProject.isPending ? messages.newProject.submitting : messages.newProject.submit}
        </Button>
      </div>
    </form>
  )
}
