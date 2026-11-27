import { useRef, useState } from 'react'
import { InlineConfirm } from '../components/InlineConfirm.tsx'
import { Button } from '../components/ui/Button.tsx'
import { messages } from '../i18n/messages.ts'
import type { Member } from '../workItems/useMembers.ts'
import type { MemberRole } from './useMemberChanges.ts'

const text = messages.members

export interface MemberRowProps {
  member: Member
  self: boolean
  /** The roles the caller may give this member; none means no controls (see rolesFor). */
  roles: readonly MemberRole[]
  saving: boolean
  removing: boolean
  onSaveRole: (role: MemberRole, done: () => void) => void
  onRemove: () => void
  /** Called when the removal confirmation opens, so an earlier error is cleared. */
  onStartRemove: () => void
}

/**
 * One member (TREK-48): name, email, person or bot, and role. Choosing a role only picks it; "Save role" applies it, so
 * arrow keys on the select change nothing by themselves (WCAG 3.2.2). Busy controls stay focusable (aria-disabled), and
 * focus returns to the select once the role is saved.
 */
export function MemberRow({ member, self, roles, saving, removing, onSaveRole, onRemove, onStartRemove }: MemberRowProps) {
  const name = member.user.displayName
  const [draft, setDraft] = useState<MemberRole | null>(null)
  const [confirming, setConfirming] = useState(false)
  const select = useRef<HTMLSelectElement>(null)
  const chosen = draft ?? (member.role as MemberRole)
  const busy = saving || removing

  return (
    <li className="grid gap-2 px-4 py-3">
      <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
        <span className="font-medium">
          {name}
          {self && <span className="ml-1 text-muted-foreground">{text.you}</span>}
        </span>
        <span className="min-w-0 flex-1 truncate text-sm text-muted-foreground">{member.user.email}</span>
        <span className="rounded-full border border-border px-2 py-0.5 text-xs text-muted-foreground">{text.kinds[member.kind] ?? member.kind}</span>
        {roles.length > 0 ? (
          <>
            <select
              ref={select}
              aria-label={text.roleOf(name)}
              value={chosen}
              aria-disabled={busy || undefined}
              onChange={(event) => {
                if (!busy) setDraft(event.target.value as MemberRole)
              }}
              className="h-8 rounded-md border border-input bg-background px-2 text-sm shadow-xs outline-none focus-visible:ring-2 focus-visible:ring-ring aria-disabled:opacity-50"
            >
              {roles.map((role) => (
                <option key={role} value={role}>
                  {messages.roles[role] ?? role}
                </option>
              ))}
            </select>
            {draft !== null && draft !== member.role && (
              <Button
                size="sm"
                aria-label={text.saveRoleLabel(name)}
                aria-disabled={busy || undefined}
                onClick={() => {
                  if (busy) return
                  onSaveRole(draft, () => {
                    setDraft(null)
                    select.current?.focus()
                  })
                }}
              >
                {saving ? text.savingRole : text.saveRole}
              </Button>
            )}
            {!confirming && (
              <Button
                variant="outline"
                size="sm"
                aria-label={text.removeLabel(name)}
                aria-disabled={busy || undefined}
                onClick={() => {
                  if (busy) return
                  onStartRemove()
                  setConfirming(true)
                }}
              >
                {text.remove}
              </Button>
            )}
          </>
        ) : (
          <span className="text-sm">{messages.roles[member.role] ?? member.role}</span>
        )}
      </div>
      {confirming && (
        <InlineConfirm
          message={text.confirmRemove(name)}
          confirmLabel={text.remove}
          pendingLabel={text.removing}
          cancelLabel={text.cancel}
          pending={removing}
          onConfirm={onRemove}
          onCancel={() => setConfirming(false)}
        />
      )}
    </li>
  )
}
