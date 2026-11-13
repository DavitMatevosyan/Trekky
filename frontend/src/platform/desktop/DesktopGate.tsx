import { Fragment, useCallback, useEffect, useState, type ReactNode } from 'react'
import { messages } from '../../i18n/messages.ts'
import type { ServerSwitch } from '../Platform.ts'
import { ServerUrlError, type DesktopShell } from './desktopShell.ts'
import { ServerSetupScreen } from './ServerSetupScreen.tsx'
import { ServerUnreachableScreen } from './ServerUnreachableScreen.tsx'

type GateState =
  | { kind: 'loading' }
  | { kind: 'setup'; initialUrl: string; current: string | null; error: string | null; busy: boolean }
  | { kind: 'checking'; serverUrl: string }
  | { kind: 'unreachable'; serverUrl: string; busy: boolean }
  | { kind: 'ready'; serverUrl: string }

export interface DesktopGateProps {
  shell: DesktopShell
  /** The app, once the server answers; `server` lets it show and change the address. */
  children(server: ServerSwitch): ReactNode
}

/**
 * Desktop app start (ADR-0018): ask for the server on first run, check it answers, and show "can't reach server" with
 * retry until it does. The app is mounted only for a reachable server, and remounted from scratch after a change.
 */
export function DesktopGate({ shell, children }: DesktopGateProps) {
  const [state, setState] = useState<GateState>({ kind: 'loading' })

  const check = useCallback(
    async (serverUrl: string, from: 'start' | 'retry') => {
      setState(from === 'start' ? { kind: 'checking', serverUrl } : { kind: 'unreachable', serverUrl, busy: true })
      const reachable = await shell.checkServer(serverUrl)
      setState(reachable ? { kind: 'ready', serverUrl } : { kind: 'unreachable', serverUrl, busy: false })
    },
    [shell],
  )

  useEffect(() => {
    let cancelled = false
    shell
      .serverSettings()
      .then((settings) => {
        if (cancelled) return
        if (settings.serverUrl) void check(settings.serverUrl, 'start')
        else setState({ kind: 'setup', initialUrl: settings.defaultServerUrl, current: null, error: null, busy: false })
      })
      .catch(() => {
        if (!cancelled) setState({ kind: 'setup', initialUrl: '', current: null, error: messages.common.unexpectedError, busy: false })
      })
    return () => {
      cancelled = true
    }
  }, [shell, check])

  const changeServer = useCallback((current: string) => {
    setState({ kind: 'setup', initialUrl: current, current, error: null, busy: false })
  }, [])

  async function save(input: string, current: string | null) {
    setState({ kind: 'setup', initialUrl: input, current, error: null, busy: true })
    try {
      const serverUrl = await shell.setServerUrl(input)
      await check(serverUrl, 'start')
    } catch (error) {
      const code = error instanceof ServerUrlError ? error.code : 'unknown'
      setState({ kind: 'setup', initialUrl: input, current, error: messages.desktop.setup.errors(code), busy: false })
    }
  }

  switch (state.kind) {
    case 'loading':
    case 'checking':
      return (
        <main className="flex min-h-svh items-center justify-center p-4">
          <p role="status" className="text-sm text-muted-foreground">
            {state.kind === 'checking' ? messages.desktop.connecting(state.serverUrl) : messages.desktop.starting}
          </p>
        </main>
      )
    case 'setup': {
      const { current } = state
      return (
        <ServerSetupScreen
          // A new key per opening, so the field shows initialUrl again.
          key={`${current ?? ''}|${state.error ?? ''}`}
          initialUrl={state.initialUrl}
          error={state.error}
          busy={state.busy}
          onSubmit={(input) => void save(input, current)}
          onCancel={current ? () => void check(current, 'start') : undefined}
        />
      )
    }
    case 'unreachable': {
      const { serverUrl } = state
      return (
        <ServerUnreachableScreen
          serverUrl={serverUrl}
          busy={state.busy}
          onRetry={() => void check(serverUrl, 'retry')}
          onChangeServer={() => changeServer(serverUrl)}
        />
      )
    }
    case 'ready': {
      const { serverUrl } = state
      return <Fragment key={serverUrl}>{children({ url: serverUrl, change: () => changeServer(serverUrl) })}</Fragment>
    }
  }
}
