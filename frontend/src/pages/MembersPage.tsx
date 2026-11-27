import { useState } from 'react'
import { useMe } from '../auth/useMe.ts'
import { usePermissions } from '../auth/usePermissions.ts'
import { messages } from '../i18n/messages.ts'
import { InviteForm } from '../members/InviteForm.tsx'
import { InviteNotice } from '../members/InviteNotice.tsx'
import { inviteNoticeText } from '../members/inviteNoticeText.ts'
import { MemberList } from '../members/MemberList.tsx'
import { PendingInvites } from '../members/PendingInvites.tsx'
import type { SentInvite } from '../members/useInvites.ts'

/**
 * The organization's members, for everyone (TREK-48). Admins and Owners also invite people and manage pending invites
 * (TREK-47), change members' roles and remove members here.
 */
export function MembersPage() {
  const me = useMe()
  const can = usePermissions()
  const [notice, setNotice] = useState<{ sent: SentInvite; resent: boolean } | null>(null)
  const [announcement, setAnnouncement] = useState('')

  return (
    <section className="grid gap-6">
      <h1 className="text-2xl font-semibold">{messages.members.title}</h1>

      {can.manageMembers && (
        <>
          <InviteForm
            inviteAdmins={can.manageAdmins}
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
            inviteAdmins={can.manageAdmins}
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

      <MemberList can={can} meId={me.data?.user.id} onAnnounce={setAnnouncement} />
    </section>
  )
}
