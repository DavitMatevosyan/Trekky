import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useState } from 'react'
import { createBrowserRouter, RouterProvider, type DataRouter } from 'react-router'
import { AuthProvider } from './auth/AuthProvider.tsx'
import { AuthSession } from './auth/AuthSession.ts'
import { routes } from './routes.tsx'

export interface AppProps {
  session?: AuthSession
  queryClient?: QueryClient
  router?: DataRouter
}

/** Providers and routes; tests pass their own session, query client or memory router. */
export function App({ session, queryClient, router }: AppProps) {
  const [resolved] = useState(() => ({
    session: session ?? new AuthSession(),
    queryClient: queryClient ?? new QueryClient({ defaultOptions: { queries: { retry: 1, refetchOnWindowFocus: false } } }),
    router: router ?? createBrowserRouter(routes),
  }))

  return (
    <QueryClientProvider client={resolved.queryClient}>
      <AuthProvider session={resolved.session}>
        <RouterProvider router={resolved.router} />
      </AuthProvider>
    </QueryClientProvider>
  )
}
