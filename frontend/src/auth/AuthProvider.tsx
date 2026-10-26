import { useMemo, type ReactNode } from 'react'
import { createApiClient } from '../api/createApiClient.ts'
import { AuthContext } from './AuthContext.ts'
import type { AuthSession } from './AuthSession.ts'

export function AuthProvider({ session, children }: { session: AuthSession; children: ReactNode }) {
  const value = useMemo(() => ({ session, api: createApiClient(session) }), [session])
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
