import { useState } from 'react'
import { useMe } from '../auth/useMe.ts'
import { usePermissions } from '../auth/usePermissions.ts'
import { messages } from '../i18n/messages.ts'
import { InviteForm } from '../members/InviteForm.tsx'
import { InviteNotice } from '../members/InviteNotice.tsx'
import { inviteNoticeText } from '../members/inviteNoticeText.ts'
import { PendingInvites } from '../members/PendingInvites.tsx'
import type { SentInvite } from '../members/useInvites.ts'
import { useMembers } from '../workItems/useMembers.ts'

/**
 * The organization's members (TREK-47). Admins and Owners also invite people and manage pending invites here;
 * changing roles and removing members come with TREK-48.
 */
export function MembersPage() {
  const me = useMe()
  const can = usePermissions()
  const members = useMembers()
  const [notice, setNotice] = useState<{ sent: SentInvite; resent: boolean } | null>(null)
  const [announcement, setAnnouncement] = useState('')

  return (
    <section className="grid gap-6">
      <h1 className="text-2xl font-semibold">{messages.members.title}</h1>

      {can.manageMembers && (
        <>
          <InviteForm
            inviteAdmins={can.inviteAdmins}
            onSent={(sent) => {
              setAnnouncement(inviteNoticeText(sent, false))
              setNotice({ sent, resent: false })
            }}
          />
          {notice && (
            <InviteNotice key={notice.sent.acceptUrl} sent={notice.sent} resent={notice.resent} onDismiss={() => setNotice(null)} />
          )}
          <p role="status" className="sr-only">
            {announcement}
          </p>
          <PendingInvites
            inviteAdmins={can.inviteAdmins}
            onResent={(sent) => {
              setAnnouncement(inviteNoticeText(sent, true))
              setNotice({ sent, resent: true })
            }}
            onRevoked={(invite) => {
              setNotice(null)
              setAnnouncement(messages.invites.revoked(invite.email))
            }}
          />
        </>
      )}

      <section aria-labelledby="members-list-title" className="grid gap-3">
        <h2 id="members-list-title" className="text-lg font-semibold">
          {messages.members.listLabel}
        </h2>
        {members.isPending && <p className="text-muted-foreground">{messages.members.loading}</p>}
        {members.isError && <p role="alert">{messages.common.unexpectedError}</p>}
        {members.isSuccess && (
          <ul aria-label={messages.members.listLabel} className="divide-y divide-border rounded-xl border border-border bg-surface">
            {members.data.map((member) => (
              <li key={member.user.id} className="flex flex-wrap items-center gap-x-4 gap-y-1 px-4 py-3">
                <span className="font-medium">
                  {member.user.displayName}
                  {member.user.id === me.data?.user.id && <span className="ml-1 text-muted-foreground">{messages.members.you}</span>}
                </span>
                <span className="flex-1 truncate text-sm text-muted-foreground">{member.user.email}</span>
                <span className="text-sm">{messages.roles[member.role] ?? member.role}</span>
              </li>
            ))}
          </ul>
        )}
      </section>
    </section>
  )
}
