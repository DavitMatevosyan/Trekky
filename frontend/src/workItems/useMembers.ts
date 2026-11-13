import { useQuery } from '@tanstack/react-query'
import { asProblem, ProblemError } from '../api/problem.ts'
import type { components } from '../api/schema.d.ts'
import { useAuth } from '../auth/AuthContext.ts'

export type Member = components['schemas']['Member']

export const membersQueryKey = ['members'] as const

/** The organization's members by display name (US-13: who an item can be assigned to). */
export function useMembers() {
  const { api } = useAuth()
  return useQuery({
    queryKey: membersQueryKey,
    queryFn: async () => {
      const { data, error } = await api.GET('/api/v1/members')
      if (!data) throw new ProblemError(asProblem(error))
      return data.items
    },
  })
}
