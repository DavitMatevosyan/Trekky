import type { Platform } from '../platform/Platform.ts'
import { createWebPlatform } from '../platform/webPlatform.ts'
import { createPkcePair, randomUrlSafeString } from './pkce.ts'
import type { Tokens } from './Tokens.ts'

const SCOPE = 'openid email profile offline_access'
const PENDING_KEY = 'trekky.auth.pending'

interface PendingAuthorization {
  state: string
  verifier: string
  returnTo: string
}

interface TokenEndpointResponse {
  access_token: string
  refresh_token?: string
  expires_in?: number
}

/** Used when the token response omits expires_in: refresh soon rather than never. */
const FALLBACK_LIFETIME_SECONDS = 60

/** The code flow could not finish; `loginRequired` means there is no valid session cookie. */
export class AuthorizationError extends Error {
  readonly loginRequired: boolean
  /** Where the user was headed, so the login page can send them back there. */
  readonly returnTo: string
  /** HTTP status of a failed token request, if that is what failed. */
  readonly status: number | undefined

  constructor(
    message: string,
    { loginRequired = false, returnTo = '/', status }: { loginRequired?: boolean; returnTo?: string; status?: number } = {},
  ) {
    super(message)
    this.name = 'AuthorizationError'
    this.loginRequired = loginRequired
    this.returnTo = returnTo
    this.status = status
  }
}

export interface BeginAuthorizationOptions {
  /** Path to open after the callback. Only same-origin paths are kept. */
  returnTo?: string
  /** `none` re-authenticates silently from the session cookie, or fails with login_required. */
  prompt?: 'none'
  /** Where the flow runs (TRK-23): the API address, the redirect and where its state is kept. */
  platform?: Platform
  /**
   * `sign-up` opens the server's sign-up page first, which continues to the authorization request once the account
   * exists (the desktop app in the system browser, ADR-0019). By default the request itself is opened, and the server
   * sends a user without a session to its login page.
   */
  screen?: 'sign-up'
}

/** The server's authorization endpoint, as a same-origin path. */
const AUTHORIZE_PATH = '/connect/authorize'

/**
 * True when a login or sign-up page's `returnTo` continues an authorization request that another app (the desktop app,
 * ADR-0019) started in this browser. Those pages then go back to the server after login instead of signing in here.
 */
export function continuesAuthorization(returnTo: string): boolean {
  return returnTo.startsWith(`${AUTHORIZE_PATH}?`)
}

/** Keeps only same-origin paths, so the callback can never redirect to another site. */
export function safeReturnTo(path: string | null | undefined): string {
  if (!path || !path.startsWith('/')) return '/'
  const url = new URL(path, window.location.origin)
  return url.origin === window.location.origin ? url.pathname + url.search + url.hash : '/'
}

/**
 * Starts the authorization code flow by sending the user to /connect/authorize (the web app: a full-page redirect; the
 * desktop app: the system browser). Returns the address it opened, so the same request can be opened again.
 */
export async function beginAuthorization({
  returnTo,
  prompt,
  platform = createWebPlatform(),
  screen,
}: BeginAuthorizationOptions = {}): Promise<string> {
  const { verifier, challenge } = await createPkcePair()
  const pending: PendingAuthorization = { state: randomUrlSafeString(16), verifier, returnTo: safeReturnTo(returnTo) }
  platform.pendingAuthorization.setItem(PENDING_KEY, JSON.stringify(pending))

  const query = new URLSearchParams({
    client_id: platform.clientId,
    response_type: 'code',
    redirect_uri: platform.login.redirectUri(),
    scope: SCOPE,
    state: pending.state,
    code_challenge: challenge,
    code_challenge_method: 'S256',
  })
  if (prompt) query.set('prompt', prompt)
  const authorize = `${AUTHORIZE_PATH}?${query.toString()}`
  const url =
    screen === 'sign-up'
      ? `${platform.apiBaseUrl}/sign-up?${new URLSearchParams({ returnTo: authorize }).toString()}`
      : `${platform.apiBaseUrl}${authorize}`
  await platform.login.open(url)
  return url
}

/**
 * True when `params` answer the authorization request this app is waiting for (its `state`). Anything else, such as a
 * `trekky://` link from some web page (ADR-0019), is ignored rather than allowed to cancel the sign-in in progress.
 */
export function answersPendingAuthorization(params: URLSearchParams, platform: Pick<Platform, 'pendingAuthorization'>): boolean {
  const pending = parsePending(platform.pendingAuthorization.getItem(PENDING_KEY))
  return pending !== null && params.get('state') === pending.state
}

export interface CompletedAuthorization {
  tokens: Tokens
  returnTo: string
}

/** Handles the callback: checks state, then exchanges the code for tokens. Throws AuthorizationError. */
export async function completeAuthorization(
  params: URLSearchParams,
  {
    platform = createWebPlatform(),
    fetchFn = fetch,
    now = Date.now,
  }: { platform?: Platform; fetchFn?: typeof fetch; now?: () => number } = {},
): Promise<CompletedAuthorization> {
  const pending = parsePending(platform.pendingAuthorization.getItem(PENDING_KEY))
  const returnTo = safeReturnTo(pending?.returnTo)

  // State first: a response this tab did not ask for is never acted on, and does not cancel the request it waits for.
  if (!pending || params.get('state') !== pending.state) {
    throw new AuthorizationError('The sign-in response does not match the request.', { loginRequired: true, returnTo })
  }
  platform.pendingAuthorization.removeItem(PENDING_KEY)
  const error = params.get('error')
  if (error) {
    throw new AuthorizationError(params.get('error_description') ?? error, { loginRequired: error === 'login_required', returnTo })
  }
  const code = params.get('code')
  if (!code) throw new AuthorizationError('The sign-in response has no code.', { loginRequired: true, returnTo })

  try {
    const tokens = await requestTokens(
      platform,
      { grant_type: 'authorization_code', code, redirect_uri: platform.login.redirectUri(), code_verifier: pending.verifier },
      fetchFn,
      now,
    )
    return { tokens, returnTo }
  } catch (cause) {
    if (cause instanceof AuthorizationError) throw new AuthorizationError(cause.message, { loginRequired: true, returnTo })
    throw cause
  }
}

/** Redeems a refresh token; the server rotates it, so the old one must not be used again. */
export function refreshTokens(
  refreshToken: string,
  platform: Pick<Platform, 'apiBaseUrl' | 'clientId'>,
  fetchFn: typeof fetch = fetch,
  now: () => number = Date.now,
): Promise<Tokens> {
  return requestTokens(platform, { grant_type: 'refresh_token', refresh_token: refreshToken }, fetchFn, now)
}

async function requestTokens(
  { apiBaseUrl, clientId }: Pick<Platform, 'apiBaseUrl' | 'clientId'>,
  form: Record<string, string>,
  fetchFn: typeof fetch,
  now: () => number,
): Promise<Tokens> {
  const response = await fetchFn(`${apiBaseUrl}/connect/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({ client_id: clientId, ...form }),
  })
  if (!response.ok) {
    throw new AuthorizationError(`The token request failed (${response.status}).`, { loginRequired: true, status: response.status })
  }

  const body = (await response.json()) as TokenEndpointResponse
  return {
    accessToken: body.access_token,
    refreshToken: body.refresh_token ?? null,
    expiresAt: now() + (body.expires_in ?? FALLBACK_LIFETIME_SECONDS) * 1000,
  }
}

function parsePending(raw: string | null): PendingAuthorization | null {
  if (!raw) return null
  try {
    return JSON.parse(raw) as PendingAuthorization
  } catch {
    return null
  }
}
