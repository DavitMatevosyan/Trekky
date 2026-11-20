import { ProblemError } from '../api/problem.ts'
import { RelativeTime } from '../components/RelativeTime.tsx'
import { Button } from '../components/ui/Button.tsx'
import { messages } from '../i18n/messages.ts'
import { useInvites, useResendInvite, useRevokeInvite, type Invite, type SentInvite } from './useInvites.ts'

export interface PendingInvitesProps {
  /** An Admin may not resend or withdraw an Owner's invite for an Admin (ADR-0026). */
  inviteAdmins: boolean
  onResent: (sent: SentInvite) => void
  onRevoked: (invite: Invite) => void
  /** Window.confirm by default; tests pass their own. */
  confirm?: (message: string) => boolean
}

/** Open invites with Resend and Withdraw (TREK-47). An expired invite stays listed until it is resent or withdrawn. */
export function PendingInvites({ inviteAdmins, onResent, onRevoked, confirm = (message) => window.confirm(message) }: PendingInvitesProps) {
  const invites = useInvites({ enabled: true })
  const resend = useResendInvite()
  const revoke = useRevokeInvite()
  const failed = resend.error ?? revoke.error

  return (
    <section aria-labelledby="pending-invites-title" className="grid gap-3">
      <h2 id="pending-invites-title" className="text-lg font-semibold">
        {messages.invites.pendingTitle}
      </h2>
      <div role="alert">
        {failed && (
          <p className="text-sm text-destructive">{failed instanceof ProblemError ? failed.message : messages.common.unexpectedError}</p>
        )}
      </div>
      {invites.isPending && <p className="text-muted-foreground">{messages.invites.loading}</p>}
      {invites.isError && <p role="alert">{messages.common.unexpectedError}</p>}
      {invites.isSuccess && invites.data.length === 0 && <p className="text-sm text-muted-foreground">{messages.invites.pendingEmpty}</p>}
      {invites.isSuccess && invites.data.length > 0 && (
        <ul aria-label={messages.invites.pendingTitle} className="divide-y divide-border rounded-xl border border-border bg-surface">
          {invites.data.map((invite) => {
            const manageable = inviteAdmins || invite.role !== 'admin'
            const busy = (resend.isPending && resend.variables === invite.id) || (revoke.isPending && revoke.variables === invite.id)
            return (
              <li key={invite.id} className="flex flex-wrap items-center gap-x-4 gap-y-2 px-4 py-3">
                <div className="grid min-w-0 flex-1 gap-0.5">
                  <span className="truncate font-medium">{invite.email}</span>
                  <span className="text-xs text-muted-foreground">
                    {messages.roles[invite.role] ?? invite.role}
                    {invite.invitedBy && <> · {messages.invites.invitedBy(invite.invitedBy.displayName)}</>}
                    {' · '}
                    {invite.status === 'expired' ? (
                      <span className="font-medium text-destructive">{messages.invites.expired}</span>
                    ) : (
                      <>
                        {messages.invites.expires} <RelativeTime value={invite.expiresAt} />
                      </>
                    )}
                  </span>
                </div>
                {manageable && (
                  <div className="flex gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      aria-label={messages.invites.resendLabel(invite.email)}
                      disabled={busy}
                      onClick={() => resend.mutate(invite.id, { onSuccess: onResent })}
                    >
                      {resend.isPending && resend.variables === invite.id ? messages.invites.resending : messages.invites.resend}
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      aria-label={messages.invites.revokeLabel(invite.email)}
                      disabled={busy}
                      onClick={() => {
                        if (confirm(messages.invites.confirmRevoke(invite.email))) revoke.mutate(invite.id, { onSuccess: () => onRevoked(invite) })
                      }}
                    >
                      {revoke.isPending && revoke.variables === invite.id ? messages.invites.revoking : messages.invites.revoke}
                    </Button>
                  </div>
                )}
              </li>
            )
          })}
        </ul>
      )}
    </section>
  )
}
