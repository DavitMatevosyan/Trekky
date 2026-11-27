import { useMutation, useQueryClient } from '@tanstack/react-query'
import { asProblem, ProblemError } from '../api/problem.ts'
import { useAuth } from '../auth/AuthContext.ts'
import { membersQueryKey, type Member } from '../workItems/useMembers.ts'

export type MemberRole = 'owner' | 'admin' | 'member' | 'viewer'

/**
 * Changes a member's role with If-Match (TREK-48). Throws ProblemError: 403 when the caller's role may not, 409 for the
 * last Owner, 412 when the member changed meanwhile (the list then reloads, showing their latest role).
 */
export function useChangeMemberRole() {
  const { api } = useAuth()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ member, role }: { member: Member; role: MemberRole }): Promise<Member> => {
      const { data, error } = await api.PATCH('/api/v1/members/{userId}', {
        params: { path: { userId: member.user.id }, header: { 'If-Match': member.etag } },
        body: { role },
      })
      if (!data) throw new ProblemError(asProblem(error))
      return data
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: membersQueryKey }),
  })
}

/** Removes a member (TREK-48): they are signed out at once, and their past work keeps their name. */
export function useRemoveMember() {
  const { api } = useAuth()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (member: Member) => {
      const { error, response } = await api.DELETE('/api/v1/members/{userId}', { params: { path: { userId: member.user.id } } })
      if (!response.ok) throw new ProblemError(asProblem(error))
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: membersQueryKey }),
  })
}
