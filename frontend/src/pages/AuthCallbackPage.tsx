import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { useAuth } from '../auth/AuthContext.ts'
import { AuthorizationError } from '../auth/oauthClient.ts'
import { messages } from '../i18n/messages.ts'

/** The OAuth redirect target: exchanges the code for tokens, or sends the user to log in. */
export function AuthCallbackPage() {
  const { session } = useAuth()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const [failed, setFailed] = useState(false)
  // A code can be redeemed once; StrictMode runs effects twice in development.
  const started = useRef(false)

  useEffect(() => {
    if (started.current) return
    started.current = true

    session
      .signInFromCallback(searchParams)
      .then((returnTo) => navigate(returnTo, { replace: true }))
      .catch((error: unknown) => {
        if (error instanceof AuthorizationError && error.loginRequired) {
          const query = error.returnTo === '/' ? '' : `?${new URLSearchParams({ returnTo: error.returnTo }).toString()}`
          void navigate(`/login${query}`, { replace: true })
        } else {
          setFailed(true)
        }
      })
  }, [navigate, searchParams, session])

  if (failed) {
    return (
      <main className="p-8 text-center">
        <p role="alert">{messages.common.unexpectedError}</p>
        <Link to="/login" className="text-primary underline underline-offset-4">
          {messages.common.goToLogIn}
        </Link>
      </main>
    )
  }

  return (
    <main className="p-8 text-center text-muted-foreground">
      <p role="status">{messages.common.signingIn}</p>
    </main>
  )
}
