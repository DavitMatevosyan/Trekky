import { useState, type FormEvent } from 'react'
import { fieldErrors, formLevelError, ProblemError } from '../api/problem.ts'
import { FormError } from '../components/FormError.tsx'
import { FormField } from '../components/FormField.tsx'
import { SelectField } from '../components/SelectField.tsx'
import { Button } from '../components/ui/Button.tsx'
import { messages } from '../i18n/messages.ts'
import { useCreateInvite, type InvitableRole, type SentInvite } from './useInvites.ts'

const FIELDS = ['email', 'role']

function roleOptions(inviteAdmins: boolean): { value: InvitableRole; label: string }[] {
  const roles: InvitableRole[] = inviteAdmins ? ['admin', 'member', 'viewer'] : ['member', 'viewer']
  return roles.map((role) => ({ value: role, label: messages.roles[role] ?? role }))
}

/** "Invite people" (TREK-47): an email and a role. Only an Owner is offered Admin (ADR-0026). */
export function InviteForm({ inviteAdmins, onSent }: { inviteAdmins: boolean; onSent: (sent: SentInvite) => void }) {
  const createInvite = useCreateInvite()
  const [email, setEmail] = useState('')
  const [role, setRole] = useState<InvitableRole>('member')

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    createInvite.mutate(
      { email, role },
      {
        onSuccess: (sent) => {
          setEmail('')
          onSent(sent)
        },
      },
    )
  }

  const problem = createInvite.error instanceof ProblemError ? createInvite.error.problem : null
  const errors = problem ? fieldErrors(problem) : {}
  const formError = createInvite.error ? (problem ? formLevelError(problem, FIELDS) : messages.common.unexpectedError) : null

  return (
    <form aria-labelledby="invite-form-title" className="grid gap-4 rounded-xl border border-border bg-surface p-4" onSubmit={onSubmit} noValidate>
      <h2 id="invite-form-title" className="text-lg font-semibold">
        {messages.invites.formTitle}
      </h2>
      <FormError message={formError} />
      <div className="grid gap-4 sm:grid-cols-[1fr_12rem]">
        <FormField
          label={messages.invites.email}
          name="email"
          type="email"
          autoComplete="off"
          required
          maxLength={254}
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          error={errors.email}
        />
        <SelectField
          label={messages.invites.role}
          name="role"
          value={role}
          options={roleOptions(inviteAdmins)}
          onChange={(event) => setRole(event.target.value as InvitableRole)}
          error={errors.role}
        />
      </div>
      <p className="text-xs text-muted-foreground">{messages.invites.roleHint}</p>
      <div className="flex justify-end">
        <Button type="submit" disabled={createInvite.isPending}>
          {createInvite.isPending ? messages.invites.submitting : messages.invites.submit}
        </Button>
      </div>
    </form>
  )
}
