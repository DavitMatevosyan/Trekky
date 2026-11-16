import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { createBrowserRouter, RouterProvider, type DataRouter } from 'react-router'
import { AuthProvider } from './auth/AuthProvider.tsx'
import { AuthSession } from './auth/AuthSession.ts'
import { answersPendingAuthorization } from './auth/oauthClient.ts'
import type { Platform } from './platform/Platform.ts'
import { PlatformContext } from './platform/PlatformContext.ts'
import { CALLBACK_PATH, createWebPlatform } from './platform/webPlatform.ts'
import { routes } from './routes.tsx'

export interface AppProps {
  /** Where the app runs (TRK-23): the browser unless the desktop shell or a test passes another. */
  platform?: Platform
  session?: AuthSession
  queryClient?: QueryClient
  router?: DataRouter
}

/** Providers and routes; tests pass their own session, query client or memory router. */
export function App({ platform, session, queryClient, router }: AppProps) {
  const [resolved] = useState(() => {
    const resolvedPlatform = platform ?? createWebPlatform()
    return {
      platform: resolvedPlatform,
      session: session ?? new AuthSession({ platform: resolvedPlatform }),
      queryClient: queryClient ?? new QueryClient({ defaultOptions: { queries: { retry: 1, refetchOnWindowFocus: false } } }),
      router: router ?? createBrowserRouter(routes),
    }
  })

  // The desktop app's sign-in comes back from the system browser as trekky://auth/callback?... (ADR-0019): finish it on
  // the same callback page the web app uses.
  useEffect(
    () =>
      resolved.platform.login.onCallback?.((callbackUrl) => {
        const { search, searchParams } = new URL(callbackUrl)
        // Only the answer to this app's own request; a stray trekky:// link changes nothing.
        if (answersPendingAuthorization(searchParams, resolved.platform)) void resolved.router.navigate(`${CALLBACK_PATH}${search}`)
      }),
    [resolved],
  )

  return (
    <PlatformContext.Provider value={resolved.platform}>
      <QueryClientProvider client={resolved.queryClient}>
        <AuthProvider session={resolved.session}>
          <RouterProvider router={resolved.router} />
        </AuthProvider>
      </QueryClientProvider>
    </PlatformContext.Provider>
  )
}
