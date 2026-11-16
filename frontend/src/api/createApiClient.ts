import createClient from 'openapi-fetch'
import type { AuthSession } from '../auth/AuthSession.ts'
import type { paths } from './schema.d.ts'

/**
 * Typed client for /api/v1, generated from the API's OpenAPI document (`npm run generate:api`). `baseUrl` is the
 * platform's API origin (TRK-23): the page's origin on the web.
 */
export function createApiClient(session: AuthSession, baseUrl: string) {
  // An absolute base URL: openapi-fetch builds Request objects, which need one outside the browser.
  return createClient<paths>({ baseUrl, fetch: session.fetch })
}

export type ApiClient = ReturnType<typeof createApiClient>
