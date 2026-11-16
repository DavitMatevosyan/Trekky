import type { DesktopShell } from '../platform/desktop/desktopShell.ts'

/** A desktop shell for tests: records what the app asked of it and lets a test send a sign-in callback. */
export interface FakeDesktopShell extends DesktopShell {
  /** Addresses passed to setServerUrl. */
  saved: string[]
  /** URLs the app opened in the "system browser". */
  opened: string[]
  /** Credential Manager: server → refresh token. */
  credentials: Map<string, string>
  /** Sends a trekky:// URL as if Windows had opened it. */
  sendCallback(url: string): void
}

export function fakeDesktopShell(overrides: Partial<DesktopShell> = {}): FakeDesktopShell {
  const saved: string[] = []
  const opened: string[] = []
  const credentials = new Map<string, string>()
  const listeners = new Set<(url: string) => void>()
  return {
    saved,
    opened,
    credentials,
    sendCallback: (url) => listeners.forEach((listener) => listener(url)),
    serverSettings: vi.fn().mockResolvedValue({ serverUrl: null, defaultServerUrl: 'https://default.example.com' }),
    setServerUrl: vi.fn(async (url: string) => {
      saved.push(url)
      return url.replace(/\/+$/, '')
    }),
    checkServer: vi.fn().mockResolvedValue(true),
    openInBrowser: vi.fn(async (url: string) => {
      opened.push(url)
    }),
    loadRefreshToken: vi.fn(async (server: string) => credentials.get(server) ?? null),
    saveRefreshToken: vi.fn(async (server: string, token: string | null) => {
      if (token === null) credentials.delete(server)
      else credentials.set(server, token)
    }),
    onAuthCallback: (listener) => {
      listeners.add(listener)
      return () => listeners.delete(listener)
    },
    ...overrides,
  }
}
