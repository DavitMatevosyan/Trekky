/**
 * The bridge to the Windows desktop shell (Tauri, ADR-0018). The shell keeps the server address; the web app asks
 * for it, stores a new one through it and checks the server itself. Only loaded inside the desktop app.
 */
export interface DesktopShell {
  serverSettings(): Promise<ServerSettings>
  /** Stores the address and returns it as stored (the server's origin); rejects with a {@link ServerUrlError}. */
  setServerUrl(url: string): Promise<string>
  /** True when the server's health endpoint answers ok within the timeout. */
  checkServer(serverUrl: string): Promise<boolean>
}

export interface ServerSettings {
  /** The address the user chose, or null on first run. */
  serverUrl: string | null
  /** What the first-run screen offers. */
  defaultServerUrl: string
}

/** Why the shell refused an address: `invalid` (not a web address), `insecure` (plain http to another computer). */
export class ServerUrlError extends Error {
  constructor(readonly code: string) {
    super(`Server address refused: ${code}`)
    this.name = 'ServerUrlError'
  }
}

const HEALTH_TIMEOUT_MS = 10_000

/** True inside the Tauri window (its IPC bridge is on `window`). */
export function isDesktop(): boolean {
  return typeof window !== 'undefined' && '__TAURI_INTERNALS__' in window
}

/** Calls the server's anonymous health endpoint; any network error, timeout or non-ok answer counts as unreachable. */
export async function checkServer(serverUrl: string, fetchFn: typeof fetch = fetch, timeoutMs = HEALTH_TIMEOUT_MS): Promise<boolean> {
  try {
    const response = await fetchFn(`${serverUrl}/api/v1/health`, { signal: AbortSignal.timeout(timeoutMs) })
    if (!response.ok) return false
    const body: unknown = await response.json()
    return typeof body === 'object' && body !== null && (body as { status?: unknown }).status === 'ok'
  } catch {
    return false
  }
}

/** The real shell: Tauri commands from `desktop/src-tauri/src/lib.rs`. The API package is loaded on first use only. */
export function createTauriShell(): DesktopShell {
  const invoke = async <T>(command: string, args?: Record<string, unknown>): Promise<T> => {
    const core = await import('@tauri-apps/api/core')
    return core.invoke<T>(command, args)
  }
  return {
    serverSettings: () => invoke<ServerSettings>('server_settings'),
    setServerUrl: async (url) => {
      try {
        return await invoke<string>('set_server_url', { url })
      } catch (error) {
        throw new ServerUrlError(typeof error === 'string' ? error : 'unknown')
      }
    },
    checkServer: (serverUrl) => checkServer(serverUrl),
  }
}
