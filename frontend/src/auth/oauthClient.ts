import { createPkcePair, randomUrlSafeString } from './pkce.ts'
import type { Tokens } from './Tokens.ts'

/** The web app's OAuth client, registered by the API (ADR-0003): public, authorization code + PKCE. */
export const CLIENT_ID = 'trekky-web'
export const CALLBACK_PATH = '/auth/callback'
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
  storage?: Storage
  navigate?: (url: string) => void
}

function redirectUri(): string {
  return `${window.location.origin}${CALLBACK_PATH}`
}

/** Keeps only same-origin paths, so the callback can never redirect to another site. */
export function safeReturnTo(path: string | null | undefined): string {
  if (!path || !path.startsWith('/')) return '/'
  const url = new URL(path, window.location.origin)
  return url.origin === window.location.origin ? url.pathname + url.search + url.hash : '/'
}

/** Starts the authorization code flow with a full-page redirect to /connect/authorize. */
export async function beginAuthorization({
  returnTo,
  prompt,
  storage = sessionStorage,
  navigate = (url) => window.location.assign(url),
}: BeginAuthorizationOptions = {}): Promise<void> {
  const { verifier, challenge } = await createPkcePair()
  const pending: PendingAuthorization = { state: randomUrlSafeString(16), verifier, returnTo: safeReturnTo(returnTo) }
  storage.setItem(PENDING_KEY, JSON.stringify(pending))

  const query = new URLSearchParams({
    client_id: CLIENT_ID,
    response_type: 'code',
    redirect_uri: redirectUri(),
    scope: SCOPE,
    state: pending.state,
    code_challenge: challenge,
    code_challenge_method: 'S256',
  })
  if (prompt) query.set('prompt', prompt)
  navigate(`/connect/authorize?${query.toString()}`)
}

export interface CompletedAuthorization {
  tokens: Tokens
  returnTo: string
}

/** Handles the callback: checks state, then exchanges the code for tokens. Throws AuthorizationError. */
export async function completeAuthorization(
  params: URLSearchParams,
  { storage = sessionStorage, fetchFn = fetch, now = Date.now }: { storage?: Storage; fetchFn?: typeof fetch; now?: () => number } = {},
): Promise<CompletedAuthorization> {
  const raw = storage.getItem(PENDING_KEY)
  storage.removeItem(PENDING_KEY)
  const pending = parsePending(raw)
  const returnTo = safeReturnTo(pending?.returnTo)

  // State first: a response this tab did not ask for is never acted on.
  if (!pending || params.get('state') !== pending.state) {
    throw new AuthorizationError('The sign-in response does not match the request.', { loginRequired: true, returnTo })
  }
  const error = params.get('error')
  if (error) {
    throw new AuthorizationError(params.get('error_description') ?? error, { loginRequired: error === 'login_required', returnTo })
  }
  const code = params.get('code')
  if (!code) throw new AuthorizationError('The sign-in response has no code.', { loginRequired: true, returnTo })

  try {
    const tokens = await requestTokens(
      { grant_type: 'authorization_code', code, redirect_uri: redirectUri(), code_verifier: pending.verifier },
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
export function refreshTokens(refreshToken: string, fetchFn: typeof fetch = fetch, now: () => number = Date.now): Promise<Tokens> {
  return requestTokens({ grant_type: 'refresh_token', refresh_token: refreshToken }, fetchFn, now)
}

async function requestTokens(form: Record<string, string>, fetchFn: typeof fetch, now: () => number): Promise<Tokens> {
  const response = await fetchFn('/connect/token', {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({ client_id: CLIENT_ID, ...form }),
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
