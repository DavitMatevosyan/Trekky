import { useId, useState } from 'react'
import { Button } from '../components/ui/Button.tsx'
import { Input } from '../components/ui/Input.tsx'
import { Label } from '../components/ui/Label.tsx'
import { messages } from '../i18n/messages.ts'
import { inviteNoticeText } from './inviteNoticeText.ts'
import type { SentInvite } from './useInvites.ts'

export interface InviteNoticeProps {
  sent: SentInvite
  /** A resend says that the earlier link stopped working. */
  resent: boolean
  onDismiss: () => void
}

/**
 * What happened to a sent or resent invite (ADR-0027). When no email went out (no mail server yet, or sending failed),
 * the inviter gets the link to pass on themselves; it is shown only here, once, because the API keeps just its hash.
 */
export function InviteNotice({ sent, resent, onDismiss }: InviteNoticeProps) {
  const id = useId()
  const [copied, setCopied] = useState(false)
  const text = inviteNoticeText(sent, resent)

  async function copy() {
    try {
      await navigator.clipboard.writeText(sent.acceptUrl)
      setCopied(true)
    } catch {
      // No clipboard access (permissions, insecure origin): the link stays selectable in the field.
      setCopied(false)
    }
  }

  return (
    <div className="grid gap-3 rounded-xl border border-border bg-surface p-4">
      {/* Announced by the page's live region (MembersPage); a region mounted with its text is often not read out. */}
      <p className="text-sm">{text}</p>
      {!sent.emailSent && (
        <div className="grid gap-1.5">
          <Label htmlFor={`${id}-link`}>{messages.invites.linkLabel}</Label>
          <div className="flex gap-2">
            <Input
              id={`${id}-link`}
              readOnly
              value={sent.acceptUrl}
              aria-describedby={`${id}-hint`}
              className="font-mono text-xs"
              onFocus={(event) => event.currentTarget.select()}
            />
            <Button type="button" variant="outline" onClick={() => void copy()}>
              {copied ? messages.invites.copied : messages.invites.copy}
            </Button>
          </div>
          <p id={`${id}-hint`} className="text-xs text-muted-foreground">
            {messages.invites.linkHint}
          </p>
        </div>
      )}
      <Button type="button" variant="link" size="link" className="justify-self-start" onClick={onDismiss}>
        {messages.invites.dismiss}
      </Button>
    </div>
  )
}
