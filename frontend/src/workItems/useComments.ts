import { useInfiniteQuery, useMutation, useQueryClient, type InfiniteData } from '@tanstack/react-query'
import { asProblem, ProblemError } from '../api/problem.ts'
import type { components } from '../api/schema.d.ts'
import { useAuth } from '../auth/AuthContext.ts'

export type Comment = components['schemas']['Comment']
type CommentPage = components['schemas']['CommentPage']

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

/** Replaces a comment's body with its ETag (US-12 AC2). Throws ProblemError: 400 field errors, 403 not the author, 412 changed meanwhile. */
export function useEditComment(workItemId: string) {
  const { api } = useAuth()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ comment, bodyMd }: { comment: Comment; bodyMd: string }) => {
      const { data, error } = await api.PATCH('/api/v1/comments/{commentId}', {
        params: { path: { commentId: comment.id }, header: { 'If-Match': comment.etag } },
        body: { bodyMd },
        headers: { 'Content-Type': 'application/merge-patch+json' },
      })
      if (!data) throw new ProblemError(asProblem(error))
      return data
    },
    // The saved version at once, so a second edit right away sends its new etag.
    onSuccess: (saved) =>
      queryClient.setQueryData<InfiniteData<CommentPage, string | undefined>>(commentsQueryKey(workItemId), (data) =>
        data && {
          ...data,
          pages: data.pages.map((page) => ({ ...page, items: page.items.map((item) => (item.id === saved.id ? saved : item)) })),
        },
      ),
    // Also after a 412, so the latest etag is there for the next save.
    onSettled: () => queryClient.invalidateQueries({ queryKey: commentsQueryKey(workItemId) }),
  })
}

/** Deletes one's own comment (US-12 AC2) and gives back when (server time). Throws ProblemError: 403 not the author, 404 already gone. */
export function useDeleteComment(workItemId: string) {
  const { api } = useAuth()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (comment: Comment) => {
      const { data, error } = await api.DELETE('/api/v1/comments/{commentId}', { params: { path: { commentId: comment.id } } })
      if (!data) throw new ProblemError(asProblem(error))
      return data
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: commentsQueryKey(workItemId) }),
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
