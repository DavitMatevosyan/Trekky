import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useState } from 'react'
import { createBrowserRouter, RouterProvider, type DataRouter } from 'react-router'
import { AuthProvider } from './auth/AuthProvider.tsx'
import { AuthSession } from './auth/AuthSession.ts'
import type { Platform } from './platform/Platform.ts'
import { PlatformContext } from './platform/PlatformContext.ts'
import { createWebPlatform } from './platform/webPlatform.ts'
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
