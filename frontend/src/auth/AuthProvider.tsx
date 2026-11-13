import { useMemo, type ReactNode } from 'react'
import { createApiClient } from '../api/createApiClient.ts'
import { usePlatform } from '../platform/PlatformContext.ts'
import { AuthContext } from './AuthContext.ts'
import type { AuthSession } from './AuthSession.ts'

export function AuthProvider({ session, children }: { session: AuthSession; children: ReactNode }) {
  const { apiBaseUrl } = usePlatform()
  const value = useMemo(() => ({ session, api: createApiClient(session, apiBaseUrl) }), [session, apiBaseUrl])
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
