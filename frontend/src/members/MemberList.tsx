import { useRef } from 'react'
import { ProblemError } from '../api/problem.ts'
import type { Permissions } from '../auth/permissions.ts'
import { messages } from '../i18n/messages.ts'
import { useMembers } from '../workItems/useMembers.ts'
import { rolesFor } from './memberRoles.ts'
import { MemberRow } from './MemberRow.tsx'
import { useChangeMemberRole, useRemoveMember } from './useMemberChanges.ts'

const text = messages.members

/**
 * The organization's members with name, email, whether a person or a bot, and role (TREK-48). Owners and Admins change
 * roles and remove members after a confirmation; `onAnnounce` reports what changed for screen readers.
 */
export function MemberList({ can, meId, onAnnounce }: { can: Permissions; meId: string | undefined; onAnnounce: (message: string) => void }) {
  const members = useMembers()
  const change = useChangeMemberRole()
  const remove = useRemoveMember()
  const heading = useRef<HTMLHeadingElement>(null)
  // Only the latest action's error: each action clears the other's.
  const failed = change.error ?? remove.error
  const owners = members.data?.filter((member) => member.role === 'owner').length ?? 0

  return (
    <section aria-labelledby="members-list-title" className="grid gap-3">
      <h2 id="members-list-title" ref={heading} tabIndex={-1} className="text-lg font-semibold outline-none">
        {text.listLabel}
      </h2>
      <div role="alert">
        {failed && <p className="text-sm text-destructive">{failed instanceof ProblemError ? failed.message : messages.common.unexpectedError}</p>}
      </div>
      {members.isPending && <p className="text-muted-foreground">{text.loading}</p>}
      {members.isError && <p role="alert">{messages.common.unexpectedError}</p>}
      {members.isSuccess && (
        <ul aria-label={text.listLabel} className="divide-y divide-border rounded-xl border border-border bg-surface">
          {members.data.map((member) => {
            const name = member.user.displayName
            const self = member.user.id === meId
            return (
              <MemberRow
                key={member.user.id}
                member={member}
                self={self}
                roles={rolesFor(member, can, { self, lastOwner: member.role === 'owner' && owners <= 1 })}
                saving={change.isPending && change.variables.member.user.id === member.user.id}
                removing={remove.isPending && remove.variables.user.id === member.user.id}
                onSaveRole={(role, done) => {
                  remove.reset()
                  change.mutate(
                    { member, role },
                    {
                      onSuccess: (saved) => {
                        done()
                        onAnnounce(text.roleChanged(name, messages.roles[saved.role] ?? saved.role))
                      },
                    },
                  )
                }}
                onStartRemove={() => {
                  change.reset()
                  remove.reset()
                }}
                onRemove={() =>
                  remove.mutate(member, {
                    onSuccess: () => {
                      onAnnounce(text.removed(name))
                      // The row is gone: keep focus in the list's section rather than losing it (WCAG 2.4.3).
                      heading.current?.focus()
                    },
                  })
                }
              />
            )
          })}
        </ul>
      )}
    </section>
  )
}
