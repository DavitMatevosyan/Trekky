import { AuthCard } from '../../components/AuthCard.tsx'
import { Button } from '../../components/ui/Button.tsx'
import { messages } from '../../i18n/messages.ts'

export interface ServerUnreachableScreenProps {
  serverUrl: string
  busy: boolean
  onRetry(): void
  onChangeServer(): void
}

/** Desktop app: the server did not answer (offline, server down, wrong address). Online only, so nothing else to show. */
export function ServerUnreachableScreen({ serverUrl, busy, onRetry, onChangeServer }: ServerUnreachableScreenProps) {
  return (
    <AuthCard title={messages.desktop.unreachable.title} footer={messages.desktop.unreachable.footer}>
      <div className="grid gap-4">
        <p className="text-sm" role="alert">
          {messages.desktop.unreachable.body(serverUrl)}
        </p>
        <Button type="button" onClick={onRetry} disabled={busy}>
          {busy ? messages.desktop.unreachable.retrying : messages.desktop.unreachable.retry}
        </Button>
        <Button type="button" variant="outline" onClick={onChangeServer} disabled={busy}>
          {messages.desktop.unreachable.changeServer}
        </Button>
      </div>
    </AuthCard>
  )
}
