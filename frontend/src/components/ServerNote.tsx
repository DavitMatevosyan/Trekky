import { messages } from '../i18n/messages.ts'
import { usePlatform } from '../platform/PlatformContext.ts'

/** Desktop app only: which server the sign-in goes to, with a way to change it (ADR-0018). Nothing in the browser. */
export function ServerNote() {
  const { server } = usePlatform()
  if (!server) return null

  return (
    <p className="mt-4 flex flex-wrap items-center justify-center gap-x-2 text-center text-xs text-muted-foreground">
      <span>{messages.desktop.server.label(new URL(server.url).host)}</span>
      <button
        type="button"
        onClick={server.change}
        className="font-medium text-primary underline underline-offset-4 hover:no-underline focus-visible:outline-2 focus-visible:outline-ring"
      >
        {messages.desktop.server.change}
      </button>
    </p>
  )
}
