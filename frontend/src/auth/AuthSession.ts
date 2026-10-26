import { AuthorizationError, completeAuthorization, refreshTokens } from './oauthClient.ts'
import type { Tokens } from './Tokens.ts'

/** Refresh this long before the access token expires, so requests never race the expiry. */
const REFRESH_MARGIN_MS = 30_000

type Listener = () => void

/** `loggedOut` = the user logged out in this tab, so guards go to the login page instead of re-authenticating. */
export type AuthStatus = 'authenticated' | 'anonymous' | 'loggedOut'

export interface AuthSessionOptions {
  fetchFn?: typeof fetch
  now?: () => number
}

/**
 * The signed-in state of this tab: tokens live in memory only (ADR-0003), so a reload starts anonymous and
 * re-runs the code flow silently. Sends API requests with the bearer token and refreshes it without the user
 * noticing (US-02 AC5).
 */
export class AuthSession {
  private tokens: Tokens | null = null
  private loggedOut = false
  private loggingOut = false
  /** Bumped whenever the tokens are dropped, so a refresh that finishes afterwards cannot bring them back. */
  private generation = 0
  private refreshing: Promise<Tokens | null> | null = null
  private readonly listeners = new Set<Listener>()
  private readonly fetchFn: typeof fetch
  private readonly now: () => number

  /** `fetchFn` is called as a method, so pass a wrapper rather than the bare global `fetch`. */
  constructor({ fetchFn = (input, init) => fetch(input, init), now = Date.now }: AuthSessionOptions = {}) {
    this.fetchFn = fetchFn
    this.now = now
  }

  get status(): AuthStatus {
    if (this.tokens !== null) return 'authenticated'
    return this.loggedOut ? 'loggedOut' : 'anonymous'
  }

  subscribe = (listener: Listener): (() => void) => {
    this.listeners.add(listener)
    return () => this.listeners.delete(listener)
  }

  getSnapshot = (): AuthStatus => this.status

  setTokens(tokens: Tokens): void {
    this.tokens = tokens
    this.loggedOut = false
    this.emit()
  }

  /** Forgets the tokens. Pass `loggedOut` when the user logged out, rather than the session lapsing. */
  clear({ loggedOut = false }: { loggedOut?: boolean } = {}): void {
    // While logging out, a lapsed session must not look 'anonymous': guards would start a silent sign-in.
    loggedOut ||= this.loggingOut
    if (this.tokens === null && this.loggedOut === loggedOut) return
    this.generation++
    this.tokens = null
    this.loggedOut = loggedOut
    this.emit()
  }

  /**
   * Logs out: runs the server call (which revokes the tokens and the session cookie), then forgets the tokens
   * whatever its outcome, so this tab is logged out either way.
   */
  async logOut(serverLogOut: () => Promise<unknown>): Promise<void> {
    this.loggingOut = true
    try {
      await serverLogOut()
    } finally {
      this.loggingOut = false
      this.clear({ loggedOut: true })
    }
  }

  /** Finishes the code flow on the callback page; returns the path to open next. Throws AuthorizationError. */
  async signInFromCallback(params: URLSearchParams): Promise<string> {
    const { tokens, returnTo } = await completeAuthorization(params, { fetchFn: this.fetchFn, now: this.now })
    this.setTokens(tokens)
    return returnTo
  }

  /** `fetch` for the API: adds the bearer token, and on 401 refreshes once and retries. */
  fetch = async (request: Request): Promise<Response> => {
    const retry = request.clone()
    const token = await this.validAccessToken()
    const response = await this.fetchFn(withBearer(request, token))
    if (response.status !== 401 || token === null) return response

    const refreshed = await this.refresh(token)
    return refreshed ? this.fetchFn(withBearer(retry, refreshed.accessToken)) : response
  }

  private async validAccessToken(): Promise<string | null> {
    const current = this.tokens
    if (current === null) return null
    if (current.expiresAt - this.now() > REFRESH_MARGIN_MS) return current.accessToken
    // If the refresh could not run (offline, server error), try the current token; the API decides.
    return (await this.refresh(current.accessToken))?.accessToken ?? this.tokens?.accessToken ?? null
  }

  /** One refresh at a time: concurrent callers share it, because each refresh token is single-use. */
  private refresh(staleAccessToken: string): Promise<Tokens | null> {
    if (this.tokens && this.tokens.accessToken !== staleAccessToken) return Promise.resolve(this.tokens)
    this.refreshing ??= this.redeemRefreshToken().finally(() => {
      this.refreshing = null
    })
    return this.refreshing
  }

  private async redeemRefreshToken(): Promise<Tokens | null> {
    const refreshToken = this.tokens?.refreshToken
    if (!refreshToken) {
      this.clear()
      return null
    }
    const generation = this.generation
    try {
      const tokens = await refreshTokens(refreshToken, this.fetchFn, this.now)
      if (generation !== this.generation) return null
      this.setTokens(tokens)
      return tokens
    } catch (error) {
      // Rejected (revoked, expired or reused): the user has to sign in again. Network or server errors keep the
      // tokens, so the next request tries again instead of sending the user through a redirect.
      if (error instanceof AuthorizationError && (error.status === 400 || error.status === 401)) this.clear()
      return null
    }
  }

  private emit(): void {
    for (const listener of this.listeners) listener()
  }
}

function withBearer(request: Request, token: string | null): Request {
  if (token === null) return request
  const headers = new Headers(request.headers)
  headers.set('Authorization', `Bearer ${token}`)
  return new Request(request, { headers })
}
