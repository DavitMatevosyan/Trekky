import { Outlet, useParams } from 'react-router'
import { NotFoundPage } from '../pages/NotFoundPage.tsx'
import { PROJECT_KEY_PATTERN } from './suggestProjectKey.ts'

/** Lets only URLs that start with a valid project key (`/TRK`) through; anything else, like `/nope`, is not found. */
export function ProjectKeyRoute() {
  const { projectKey = '' } = useParams()
  return PROJECT_KEY_PATTERN.test(projectKey) ? <Outlet /> : <NotFoundPage />
}
