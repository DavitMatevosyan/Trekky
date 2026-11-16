import { createContext, useContext, useSyncExternalStore } from 'react'
import type { ApiClient } from '../api/createApiClient.ts'
import type { AuthSession, AuthStatus } from './AuthSession.ts'

export interface AuthContextValue {
  session: AuthSession
  api: ApiClient
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function useAuth(): AuthContextValue {
  const value = useContext(AuthContext)
  if (!value) throw new Error('useAuth must be used inside <AuthProvider>')
  return value
}

export function useAuthStatus(): AuthStatus {
  const { session } = useAuth()
  return useSyncExternalStore(session.subscribe, session.getSnapshot)
}
