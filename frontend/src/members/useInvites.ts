import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { asProblem, ProblemError } from '../api/problem.ts'
import type { components } from '../api/schema.d.ts'
import { useAuth } from '../auth/AuthContext.ts'

export type Invite = components['schemas']['Invite']
export type SentInvite = components['schemas']['SentInvite']
export type InvitableRole = 'admin' | 'member' | 'viewer'

export const invitesQueryKey = ['invites'] as const

/** The organization's open invites, newest first (TREK-47); only Admins and Owners may ask. */
export function useInvites({ enabled }: { enabled: boolean }) {
  const { api } = useAuth()
  return useQuery({
    queryKey: invitesQueryKey,
    enabled,
    queryFn: async () => {
      const { data, error } = await api.GET('/api/v1/invites')
      if (!data) throw new ProblemError(asProblem(error))
      return data.items
    },
  })
}

/** Sends an invite; the answer carries the link, shown to copy when no email went out. */
export function useCreateInvite() {
  const { api } = useAuth()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (body: { email: string; role: InvitableRole }): Promise<SentInvite> => {
      const { data, error } = await api.POST('/api/v1/invites', { body })
      if (!data) throw new ProblemError(asProblem(error))
      return data
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: invitesQueryKey }),
  })
}

/** Sends an open invite again with a new link (the old one stops working). */
export function useResendInvite() {
  const { api } = useAuth()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (inviteId: string): Promise<SentInvite> => {
      const { data, error } = await api.POST('/api/v1/invites/{inviteId}/resend', { params: { path: { inviteId } } })
      if (!data) throw new ProblemError(asProblem(error))
      return data
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: invitesQueryKey }),
  })
}

/** Withdraws an open invite. */
export function useRevokeInvite() {
  const { api } = useAuth()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (inviteId: string) => {
      const { error, response } = await api.DELETE('/api/v1/invites/{inviteId}', { params: { path: { inviteId } } })
      if (!response.ok) throw new ProblemError(asProblem(error))
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: invitesQueryKey }),
  })
}
