import { messages } from '../i18n/messages.ts'
import type { SentInvite } from './useInvites.ts'

/** The sentence for a sent or resent invite, shown in the notice and announced by the members page. */
export function inviteNoticeText(sent: SentInvite, resent: boolean): string {
  const email = sent.invite.email
  if (!sent.emailSent) return messages.invites.notEmailed(email)
  return resent ? messages.invites.resent(email) : messages.invites.sent(email)
}
