import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Link, Outlet, useNavigate } from 'react-router'
import { useAuth } from '../auth/AuthContext.ts'
import { useMe } from '../auth/useMe.ts'
import { messages } from '../i18n/messages.ts'
import { Button } from './ui/Button.tsx'

/** Top bar with the organization and the user menu (wireframes: "App shell"); project switcher comes with projects. */
export function AppShell() {
  const { api, session } = useAuth()
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const me = useMe()

  const logOut = useMutation({
    // The API revokes every token and invalidates the session cookie (US-02 AC4).
    mutationFn: () => session.logOut(() => api.POST('/api/v1/auth/logout')),
    onSettled: () => {
      queryClient.clear()
      void navigate('/login', { replace: true })
    },
  })

  return (
    <div className="min-h-svh">
      <header className="border-b border-border">
        <nav aria-label={messages.shell.mainNav} className="mx-auto flex h-14 max-w-5xl items-center justify-between gap-4 px-4">
          <Link to="/" className="font-semibold">
            {messages.appName}
          </Link>
          <div className="flex items-center gap-3 text-sm">
            {me.data && <span className="text-muted-foreground">{me.data.user.email ?? me.data.user.displayName}</span>}
            <Button variant="outline" size="sm" onClick={() => logOut.mutate()} disabled={logOut.isPending}>
              {logOut.isPending ? messages.shell.loggingOut : messages.shell.logOut}
            </Button>
          </div>
        </nav>
      </header>
      <main className="mx-auto max-w-5xl px-4 py-8">
        <Outlet />
      </main>
    </div>
  )
}
