import { useState } from 'react'
import { ProblemError } from '../api/problem.ts'
import { FormError } from '../components/FormError.tsx'
import { InlineConfirm } from '../components/InlineConfirm.tsx'
import { RelativeTime } from '../components/RelativeTime.tsx'
import { Button } from '../components/ui/Button.tsx'
import { messages } from '../i18n/messages.ts'
import { usePlatform } from '../platform/PlatformContext.ts'
import { startInstall } from './installState.ts'
import { useDisconnectGitHub, useGitHubSettings, type GitHubConnection } from './useGitHub.ts'

const t = messages.github

/**
 * Organization settings → GitHub (TREK-74): the connected installations of the Trekky GitHub App, a link to each one's
 * page on GitHub (to choose repositories or uninstall), Disconnect, and Connect, which starts the install on GitHub.
 */
export function GitHubSettingsSection({ notice }: { notice?: string }) {
  const settings = useGitHubSettings()
  const disconnect = useDisconnectGitHub()
  const { links } = usePlatform()
  const [confirming, setConfirming] = useState<string | null>(null)
  const [announcement, setAnnouncement] = useState(notice ?? '')

  function connect(installUrl: string) {
    const url = startInstall(installUrl)
    // The desktop app opens GitHub in the system browser; the web app leaves for GitHub and comes back to the callback.
    if (!links.open(url)) window.location.assign(url)
  }

  function confirmDisconnect(connection: GitHubConnection) {
    disconnect.mutate(connection.id, {
      onSuccess: () => {
        setConfirming(null)
        setAnnouncement(t.disconnected(connection.account))
      },
    })
  }

  const disconnectError =
    disconnect.error instanceof ProblemError ? (disconnect.error.problem.detail ?? disconnect.error.problem.title ?? null) : null

  return (
    <section aria-labelledby="github-title" className="grid gap-4 rounded-xl border border-border bg-surface p-4">
      <div className="grid gap-1">
        <h2 id="github-title" className="text-lg font-semibold">
          {t.title}
        </h2>
        <p className="text-sm text-muted-foreground">{t.intro}</p>
      </div>

      <p role="status" className="text-sm empty:sr-only">
        {announcement}
      </p>

      {settings.isPending && <p className="text-sm text-muted-foreground">{t.loading}</p>}
      {settings.isError && <FormError message={messages.common.unexpectedError} />}

      {settings.data && !settings.data.configured && <p className="text-sm">{t.notConfigured}</p>}

      {settings.data?.configured && (
        <>
          {settings.data.installations.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t.none}</p>
          ) : (
            <div className="grid gap-2">
              <h3 className="text-sm font-medium">{t.connections}</h3>
              <ul className="grid gap-2">
                {settings.data.installations.map((connection) => (
                  <li key={connection.id} className="grid gap-2 rounded-md border border-border p-3">
                    <div className="flex flex-wrap items-baseline justify-between gap-2">
                      <p>
                        <span className="font-medium">{connection.account}</span>{' '}
                        <span className="text-sm text-muted-foreground">({t.accountType(connection.accountType)})</span>
                      </p>
                      <p className="text-xs text-muted-foreground">
                        {t.connectedAt} <RelativeTime value={connection.connectedAt} />
                      </p>
                    </div>
                    {confirming === connection.id ? (
                      <>
                        <InlineConfirm
                          message={t.confirmDisconnect(connection.account)}
                          confirmLabel={t.disconnect(connection.account)}
                          pendingLabel={t.disconnecting}
                          cancelLabel={messages.github.callback.cancel}
                          pending={disconnect.isPending}
                          onConfirm={() => confirmDisconnect(connection)}
                          onCancel={() => {
                            setConfirming(null)
                            disconnect.reset()
                          }}
                        />
                        <FormError message={disconnectError} />
                      </>
                    ) : (
                      <div className="flex flex-wrap gap-x-4 gap-y-2 text-sm">
                        <a
                          href={connection.manageUrl}
                          target="_blank"
                          rel="noreferrer"
                          className="text-primary underline underline-offset-4 hover:no-underline"
                          onClick={(event) => {
                            if (links.open(connection.manageUrl)) event.preventDefault()
                          }}
                        >
                          {t.manage(connection.account)}
                        </a>
                        <Button variant="link" size="link" onClick={() => setConfirming(connection.id)}>
                          {t.disconnect(connection.account)}
                        </Button>
                      </div>
                    )}
                  </li>
                ))}
              </ul>
            </div>
          )}
          {settings.data.installUrl && (
            <div>
              <Button onClick={() => connect(settings.data.installUrl ?? '')}>
                {settings.data.installations.length === 0 ? t.connect : t.connectAnother}
              </Button>
            </div>
          )}
        </>
      )}
    </section>
  )
}
