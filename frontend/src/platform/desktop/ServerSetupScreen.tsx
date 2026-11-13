import type { FormEvent } from 'react'
import { AuthCard } from '../../components/AuthCard.tsx'
import { FormError } from '../../components/FormError.tsx'
import { FormField } from '../../components/FormField.tsx'
import { Button } from '../../components/ui/Button.tsx'
import { messages } from '../../i18n/messages.ts'

export interface ServerSetupScreenProps {
  /** Pre-filled: the default server on first run, the current one when changing it. */
  initialUrl: string
  error: string | null
  busy: boolean
  onSubmit(url: string): void
  /** Shown when there is a server to go back to. */
  onCancel?: () => void
}

/** Desktop app, first run or "Change server": which Trekky server to use (ADR-0018). */
export function ServerSetupScreen({ initialUrl, error, busy, onSubmit, onCancel }: ServerSetupScreenProps) {
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    onSubmit(String(new FormData(event.currentTarget).get('serverUrl') ?? ''))
  }

  return (
    <AuthCard title={messages.desktop.setup.title} footer={messages.desktop.setup.footer}>
      <form className="grid gap-4" onSubmit={submit} noValidate>
        <p className="text-sm text-muted-foreground">{messages.desktop.setup.intro}</p>
        <FormError message={error} />
        <FormField
          label={messages.desktop.setup.serverUrl}
          name="serverUrl"
          type="url"
          inputMode="url"
          autoComplete="url"
          spellCheck={false}
          defaultValue={initialUrl}
          hint={messages.desktop.setup.serverUrlHint}
          required
        />
        <Button type="submit" disabled={busy}>
          {busy ? messages.desktop.setup.connecting : messages.desktop.setup.submit}
        </Button>
        {onCancel && (
          <Button type="button" variant="outline" onClick={onCancel} disabled={busy}>
            {messages.desktop.setup.cancel}
          </Button>
        )}
      </form>
    </AuthCard>
  )
}
