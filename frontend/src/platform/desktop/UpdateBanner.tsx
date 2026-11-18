import { useEffect, useState } from 'react'
import { Button } from '../../components/ui/Button.tsx'
import { messages } from '../../i18n/messages.ts'
import type { AvailableUpdate, DesktopShell } from './desktopShell.ts'

type UpdateState =
  | { kind: 'none' }
  | { kind: 'available'; update: AvailableUpdate; installing: boolean; failed: boolean }
  | { kind: 'dismissed' }

export interface UpdateBannerProps {
  shell: Pick<DesktopShell, 'checkForUpdate' | 'installUpdate'>
}

/**
 * The desktop app's update notice (ADR-0024). DesktopGate shows it above every screen and mounts it once per start, so
 * it checks the release feed once per start and "Later" lasts until the next one. A feed that cannot be reached is
 * silent.
 */
export function UpdateBanner({ shell }: UpdateBannerProps) {
  const [state, setState] = useState<UpdateState>({ kind: 'none' })

  useEffect(() => {
    let cancelled = false
    shell
      .checkForUpdate()
      .then((update) => {
        if (!cancelled && update) setState({ kind: 'available', update, installing: false, failed: false })
      })
      .catch(() => undefined)
    return () => {
      cancelled = true
    }
  }, [shell])

  if (state.kind !== 'available') return null
  const { update, installing, failed } = state

  async function install() {
    setState({ kind: 'available', update, installing: true, failed: false })
    try {
      // On success the installer closes the app, so this only returns on failure in practice.
      await shell.installUpdate()
      setState({ kind: 'available', update, installing: false, failed: false })
    } catch {
      setState({ kind: 'available', update, installing: false, failed: true })
    }
  }

  return (
    <section aria-label={messages.updates.region} className="border-b border-border bg-muted">
      <div className="mx-auto flex max-w-5xl flex-wrap items-center gap-x-4 gap-y-2 px-4 py-2 text-sm">
        <div className="grid gap-0.5">
          <p role="status">{messages.updates.available(update.version)}</p>
          <p className="text-muted-foreground">{messages.updates.hint}</p>
          {failed && <p role="alert">{messages.updates.failed}</p>}
        </div>
        <div className="ml-auto flex gap-2">
          <Button size="sm" onClick={() => void install()} disabled={installing}>
            {installing ? messages.updates.installing : messages.updates.install}
          </Button>
          <Button size="sm" variant="outline" onClick={() => setState({ kind: 'dismissed' })} disabled={installing}>
            {messages.updates.later}
          </Button>
        </div>
      </div>
    </section>
  )
}
