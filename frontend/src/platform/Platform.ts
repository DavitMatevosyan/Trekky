/**
 * What the app needs from where it runs (TRK-23). The web app has one implementation (`createWebPlatform`); the
 * desktop app (Tauri, TRK-25/26) brings its own, so sign-in, token storage, the API address and links work there too
 * without the rest of the app knowing.
 */
export interface Platform {
  /** The API's origin, without a trailing slash: `/api/v1/…` and the OAuth endpoints (`/connect/…`) live under it. */
  apiBaseUrl: string
  /** The OAuth client this app signs in as (ADR-0003): `trekky-web` in the browser; the desktop app has its own (TRK-24). */
  clientId: string
  login: LoginRedirect
  /** Where the code flow keeps its state and PKCE verifier between the redirect and the callback. */
  pendingAuthorization: KeyValueStore
  tokens: TokenStore
  links: ExternalLinks
}

/** Sends the user to the authorization server and says where it comes back. */
export interface LoginRedirect {
  /** The callback URL registered for this client. */
  redirectUri(): string
  /** Opens the authorization URL (the web app: this tab; the desktop app: the system browser). */
  open(url: string): void
}

/** The part of the Web Storage API the code flow uses. */
export interface KeyValueStore {
  getItem(key: string): string | null
  setItem(key: string, value: string): void
  removeItem(key: string): void
}

/** Where the refresh token outlives the page or the app. The web app keeps tokens in memory only (ADR-0003). */
export interface TokenStore {
  /** The stored refresh token, or null. */
  load(): string | null
  /** Stores the current refresh token, or forgets it (null). */
  save(refreshToken: string | null): void
}

/** Opens a link that leads out of the app. */
export interface ExternalLinks {
  /** Opens `url` itself and returns true, or returns false to let the browser follow the link as usual. */
  open(url: string): boolean
}
