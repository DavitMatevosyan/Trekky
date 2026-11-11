import { useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { asProblem, ProblemError } from '../api/problem.ts'
import type { components } from '../api/schema.d.ts'
import { useAuth } from '../auth/AuthContext.ts'

export type Comment = components['schemas']['Comment']

export const commentsQueryKey = (workItemId: string) => ['work-items', 'comments', workItemId] as const

/** An item's comments, oldest first, 50 at a time (US-12 AC1). */
export function useComments(workItemId: string) {
  const { api } = useAuth()
  return useInfiniteQuery({
    queryKey: commentsQueryKey(workItemId),
    initialPageParam: undefined as string | undefined,
    queryFn: async ({ pageParam }) => {
      const { data, error } = await api.GET('/api/v1/work-items/{workItemId}/comments', {
        params: { path: { workItemId }, query: pageParam ? { cursor: pageParam } : {} },
      })
      if (!data) throw new ProblemError(asProblem(error))
      return data
    },
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
  })
}

/** Posts a Markdown comment (US-12 AC1). Throws ProblemError: 400 with field errors for an empty or too long body. */
export function useAddComment(workItemId: string) {
  const { api } = useAuth()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (bodyMd: string) => {
      const { data, error } = await api.POST('/api/v1/work-items/{workItemId}/comments', {
        params: { path: { workItemId } },
        body: { bodyMd },
      })
      if (!data) throw new ProblemError(asProblem(error))
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: commentsQueryKey(workItemId) }),
  })
}
