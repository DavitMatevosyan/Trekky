import type { ExternalLinks, Platform, ServerSwitch, TokenStore } from '../Platform.ts'
import { externalUrl } from '../externalUrl.ts'
import type { DesktopShell } from './desktopShell.ts'

/** The desktop app's OAuth client (ADR-0017). */
export const DESKTOP_CLIENT_ID = 'trekky-desktop'

/** Where the server sends the system browser after sign-in; Windows hands it to the running app (ADR-0019). */
export const DESKTOP_REDIRECT_URI = 'trekky://auth/callback'

/**
 * The desktop app's platform (ADR-0018, ADR-0019): the API on the chosen server; sign-in and sign-up in the system
 * browser as `trekky-desktop`, back through the `trekky://` deep link; the refresh token in Windows Credential Manager
 * (loaded before the app starts, so `load` stays synchronous); outside links in the system browser.
 */
export function createDesktopPlatform(
  server: ServerSwitch,
  shell: DesktopShell,
  storedRefreshToken: string | null,
  overrides: Partial<Platform> = {},
): Platform {
  return {
    apiBaseUrl: server.url,
    clientId: DESKTOP_CLIENT_ID,
    login: {
      redirectUri: () => DESKTOP_REDIRECT_URI,
      open: (url) => shell.openInBrowser(url),
      // No browser session in the app's window, so no silent sign-in: protected pages go to the login page.
      silent: false,
      mode: 'browser',
      onCallback: (listener) =>
        shell.onAuthCallback((url) => {
          if (url.startsWith(`${DESKTOP_REDIRECT_URI}?`)) listener(url)
        }),
    },
    // The code flow's state lives as long as the window, which is as long as a sign-in in the browser can take.
    pendingAuthorization: window.sessionStorage,
    tokens: credentialStore(server.url, shell, storedRefreshToken),
    links: desktopLinks(shell),
    server,
    ...overrides,
  }
}

/**
 * Outside links in the desktop app: http(s) addresses on another origin open in the system browser, because the window
 * never leaves the app (ADR-0018). Also used by the screens shown before a server is chosen (DesktopGate).
 */
export function desktopLinks(shell: DesktopShell): ExternalLinks {
  return {
    open: (url) => {
      const external = externalUrl(url)
      if (!external) return false
      shell.openInBrowser(external).catch(() => {
        // The link stays unopened; nothing else to do from a click.
      })
      return true
    },
  }
}

/**
 * A synchronous view of the stored refresh token; every change is written through to Credential Manager, one write
 * after the other, so the last token saved is the one kept (an older, already used one would revoke the session).
 */
function credentialStore(serverUrl: string, shell: DesktopShell, initial: string | null): TokenStore {
  let current = initial
  let writes: Promise<void> = Promise.resolve()
  return {
    load: () => current,
    save: (token) => {
      if (token === current) return
      current = token
      writes = writes
        .then(() => shell.saveRefreshToken(serverUrl, token))
        .catch(() => {
          // Not fatal: the app keeps working; it only starts signed out next time.
        })
    },
  }
}
