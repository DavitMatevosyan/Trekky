import type { RouteObject } from 'react-router'
import { RequireAuth } from './auth/RequireAuth.tsx'
import { CALLBACK_PATH } from './platform/webPlatform.ts'
import { AppShell } from './components/AppShell.tsx'
import { AuthCallbackPage } from './pages/AuthCallbackPage.tsx'
import { BoardPage } from './pages/BoardPage.tsx'
import { GitHubCallbackPage } from './pages/GitHubCallbackPage.tsx'
import { ProjectsPage } from './pages/ProjectsPage.tsx'
import { LogInPage } from './pages/LogInPage.tsx'
import { NotFoundPage } from './pages/NotFoundPage.tsx'
import { ProjectItemsPage } from './pages/ProjectItemsPage.tsx'
import { ProjectSettingsPage } from './pages/ProjectSettingsPage.tsx'
import { SettingsPage } from './pages/SettingsPage.tsx'
import { SignUpPage } from './pages/SignUpPage.tsx'
import { ProjectKeyRoute } from './projects/ProjectKeyRoute.tsx'
import { WorkItemPage } from './pages/WorkItemPage.tsx'

export const routes: RouteObject[] = [
  { path: '/login', element: <LogInPage /> },
  { path: '/sign-up', element: <SignUpPage /> },
  { path: CALLBACK_PATH, element: <AuthCallbackPage /> },
  {
    element: <RequireAuth />,
    children: [
      {
        element: <AppShell />,
        children: [
          { index: true, element: <ProjectsPage /> },
          // Organization settings (TREK-74). Case-sensitive, so /SETTINGS stays a project key's URL.
          { path: 'settings', element: <SettingsPage />, caseSensitive: true },
          { path: 'settings/github/callback', element: <GitHubCallbackPage />, caseSensitive: true },
        ],
      },
    ],
  },
  {
    // Project and item keys in the URL (US-09 AC3): /TRK and /TRK/items/TRK-42.
    path: ':projectKey',
    element: <ProjectKeyRoute />,
    children: [
      {
        element: <RequireAuth />,
        children: [
          {
            element: <AppShell />,
            children: [
              { index: true, element: <ProjectItemsPage /> },
              { path: 'board', element: <BoardPage /> },
              { path: 'settings', element: <ProjectSettingsPage /> },
              { path: 'items/:itemKey', element: <WorkItemPage /> },
            ],
          },
        ],
      },
    ],
  },
  { path: '*', element: <NotFoundPage /> },
]
