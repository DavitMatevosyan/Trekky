import { useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import type { components } from '../api/schema.d.ts'
import { asProblem, ProblemError } from '../api/problem.ts'
import { useAuth } from '../auth/AuthContext.ts'

export type Project = components['schemas']['Project']
export type CreateProjectInput = { name: string; key: string }

export const projectsQueryKey = ['projects'] as const

/** Projects of the current organization by name, a page at a time (GET /api/v1/projects). */
export function useProjects() {
  const { api } = useAuth()
  return useInfiniteQuery({
    queryKey: projectsQueryKey,
    initialPageParam: undefined as string | undefined,
    queryFn: async ({ pageParam }) => {
      const { data, error } = await api.GET('/api/v1/projects', { params: { query: pageParam ? { cursor: pageParam } : {} } })
      if (!data) throw new ProblemError(asProblem(error))
      return data
    },
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
  })
}

/** POST /api/v1/projects; refreshes the list on success. Throws ProblemError (400 fields, 409 key taken). */
export function useCreateProject() {
  const { api } = useAuth()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: CreateProjectInput) => {
      const { data, error } = await api.POST('/api/v1/projects', { body: input })
      if (!data) throw new ProblemError(asProblem(error))
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: projectsQueryKey }),
  })
}
