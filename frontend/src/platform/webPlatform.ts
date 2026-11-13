import type { Platform } from './Platform.ts'

/** The path the OAuth server sends the browser back to (registered for `trekky-web`). */
export const CALLBACK_PATH = '/auth/callback'

/** The browser's OAuth client, registered by the API. */
export const WEB_CLIENT_ID = 'trekky-web'

/**
 * Settings the page can be given at run time, before the app starts (e.g. by the desktop shell). In the browser the
 * API must stay on the page's origin: sign-in relies on the SameSite=Strict session cookie reaching /connect/authorize
 * (ADR-0010), so a different apiBaseUrl only works for a platform that signs in another way (the desktop app, TRK-24..26).
 */
export interface RuntimeConfig {
  /** An absolute http(s) origin for the API; the page's own origin when absent. */
  apiBaseUrl?: string
}

declare global {
  interface Window {
    __TREKKY_CONFIG__?: RuntimeConfig
  }
}

/** The API origin from runtime config, if it is a usable absolute http(s) URL; otherwise the page's own origin. */
export function apiBaseUrlFrom(config: RuntimeConfig | undefined, pageOrigin: string): string {
  const configured = config?.apiBaseUrl
  if (configured) {
    try {
      const url = new URL(configured)
      if (url.protocol === 'https:' || url.protocol === 'http:') return url.origin
    } catch {
      // Not a URL: fall back to the page's origin.
    }
  }
  return pageOrigin
}

/**
 * The browser: the API on the page's origin unless runtime config says otherwise, sign-in in this tab, the code
 * flow's state in sessionStorage, tokens in memory only (nothing stored), and links followed by the browser as usual.
 * `overrides` replace parts, for tests.
 */
export function createWebPlatform(overrides: Partial<Platform> = {}): Platform {
  return {
    apiBaseUrl: apiBaseUrlFrom(window.__TREKKY_CONFIG__, window.location.origin),
    clientId: WEB_CLIENT_ID,
    login: {
      redirectUri: () => `${window.location.origin}${CALLBACK_PATH}`,
      open: (url) => window.location.assign(url),
    },
    pendingAuthorization: window.sessionStorage,
    tokens: { load: () => null, save: () => {} },
    links: { open: () => false },
    ...overrides,
  }
}
