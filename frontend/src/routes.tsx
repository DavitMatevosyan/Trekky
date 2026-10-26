import type { RouteObject } from 'react-router'
import { RequireAuth } from './auth/RequireAuth.tsx'
import { CALLBACK_PATH } from './auth/oauthClient.ts'
import { AppShell } from './components/AppShell.tsx'
import { AuthCallbackPage } from './pages/AuthCallbackPage.tsx'
import { HomePage } from './pages/HomePage.tsx'
import { LogInPage } from './pages/LogInPage.tsx'
import { NotFoundPage } from './pages/NotFoundPage.tsx'
import { SignUpPage } from './pages/SignUpPage.tsx'

export const routes: RouteObject[] = [
  { path: '/login', element: <LogInPage /> },
  { path: '/sign-up', element: <SignUpPage /> },
  { path: CALLBACK_PATH, element: <AuthCallbackPage /> },
  {
    element: <RequireAuth />,
    children: [{ element: <AppShell />, children: [{ index: true, element: <HomePage /> }] }],
  },
  { path: '*', element: <NotFoundPage /> },
]
