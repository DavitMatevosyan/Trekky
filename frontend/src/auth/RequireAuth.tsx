import { useEffect, useRef } from 'react'
import { Navigate, Outlet, useLocation } from 'react-router'
import { messages } from '../i18n/messages.ts'
import { usePlatform } from '../platform/PlatformContext.ts'
import { useAuthStatus } from './AuthContext.ts'
import { beginAuthorization } from './oauthClient.ts'

/**
 * Guards protected routes. Without tokens in memory (first load, reload) it re-runs the code flow with
 * prompt=none: a valid session cookie signs the user back in silently, otherwise the callback sends them to the
 * login page (US-02 AC3). After logout it goes straight to the login page, also on the back button (US-02 AC4).
 * Where no silent sign-in exists (the desktop app, ADR-0018) it goes to the login page with the path to return to.
 */
export function RequireAuth() {
  const status = useAuthStatus()
  const platform = usePlatform()
  const { pathname, search } = useLocation()
  // One redirect per anonymous spell: a second one (StrictMode, a route change) would overwrite the stored
  // state and verifier and make the callback reject the first response.
  const redirecting = useRef(false)

  useEffect(() => {
    if (status !== 'anonymous') {
      redirecting.current = false
      return
    }
    if (redirecting.current || !platform.login.silent) return
    redirecting.current = true
    void beginAuthorization({ prompt: 'none', returnTo: pathname + search, platform })
  }, [status, pathname, search, platform])

  if (status === 'loggedOut') return <Navigate to="/login" replace />
  // Without a silent sign-in (desktop app) the login page is the only way in; it brings the user back here.
  if (status === 'anonymous' && !platform.login.silent) {
    return <Navigate to={`/login?${new URLSearchParams({ returnTo: pathname + search }).toString()}`} replace />
  }
  if (status === 'anonymous') {
    return (
      <main className="p-8 text-center text-muted-foreground">
        <p role="status">{messages.common.signingIn}</p>
      </main>
    )
  }

  return <Outlet />
}
