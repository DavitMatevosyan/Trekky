import { useQuery } from '@tanstack/react-query'
import { useAuth } from './AuthContext.ts'

export const meQueryKey = ['me'] as const

/** The current user, organization and role (GET /api/v1/me). */
export function useMe() {
  const { api } = useAuth()
  return useQuery({
    queryKey: meQueryKey,
    queryFn: async () => {
      const { data, error } = await api.GET('/api/v1/me')
      if (!data) throw new Error(`GET /api/v1/me failed: ${JSON.stringify(error ?? null)}`)
      return data
    },
  })
}
