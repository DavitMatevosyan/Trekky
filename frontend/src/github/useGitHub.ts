import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { components } from '../api/schema.d.ts'
import { asProblem, ProblemError } from '../api/problem.ts'
import { useAuth } from '../auth/AuthContext.ts'

export type GitHubSettings = components['schemas']['GitHubSettings']
export type GitHubConnection = components['schemas']['GitHubConnection']
export type GitHubRepository = components['schemas']['GitHubRepository']
export type ProjectRepositoryLink = components['schemas']['ProjectRepositoryLink']

/** A project's link with the ETag to send back in If-Match when changing it. */
export interface VersionedProjectRepository {
  link: ProjectRepositoryLink
  etag: string
}

export interface LinkProjectRepositoryInput {
  installationId: string
  repository: string
  docsPath: string
  branch: string
}

export const gitHubSettingsQueryKey = ['github'] as const

export const projectRepositoryQueryKey = (projectId: string) => ['projects', projectId, 'github'] as const

/** The organization's GitHub connections and install page (GET /api/v1/github; Admin and Owner). */
export function useGitHubSettings(enabled = true) {
  const { api } = useAuth()
  return useQuery({
    queryKey: gitHubSettingsQueryKey,
    enabled,
    queryFn: async () => {
      const { data, error } = await api.GET('/api/v1/github')
      if (!data) throw new ProblemError(asProblem(error))
      return data
    },
  })
}

/** POST /api/v1/github/installations with what GitHub's redirect brought back. Throws ProblemError (409, 422, 503). */
export function useConnectGitHub() {
  const { api } = useAuth()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: { code: string; installationId: number }) => {
      const { data, error } = await api.POST('/api/v1/github/installations', { body: input })
      if (!data) throw new ProblemError(asProblem(error))
      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: gitHubSettingsQueryKey }),
  })
}

/** DELETE /api/v1/github/installations/{id}; its projects lose their link, so every project link is refetched. */
export function useDisconnectGitHub() {
  const { api } = useAuth()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (connectionId: string) => {
      const { error, response } = await api.DELETE('/api/v1/github/installations/{connectionId}', {
        params: { path: { connectionId } },
      })
      if (!response.ok) throw new ProblemError(asProblem(error))
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: gitHubSettingsQueryKey })
      await queryClient.invalidateQueries({ predicate: (query) => query.queryKey[0] === 'projects' && query.queryKey[2] === 'github' })
    },
  })
}

/** The repositories a connection can reach, by name; off until a connection is chosen. */
export function useGitHubRepositories(connectionId: string | null) {
  const { api } = useAuth()
  return useQuery({
    queryKey: ['github', connectionId, 'repositories'],
    enabled: connectionId !== null,
    queryFn: async () => {
      const { data, error } = await api.GET('/api/v1/github/installations/{connectionId}/repositories', {
        params: { path: { connectionId: connectionId ?? '' } },
      })
      if (!data) throw new ProblemError(asProblem(error))
      return data
    },
  })
}

/** The project's docs repository with its ETag, or null when it has none (404). Every role may read it. */
export function useProjectRepository(projectId: string) {
  const { api } = useAuth()
  return useQuery({
    queryKey: projectRepositoryQueryKey(projectId),
    queryFn: async (): Promise<VersionedProjectRepository | null> => {
      const { data, error, response } = await api.GET('/api/v1/projects/{projectId}/github', { params: { path: { projectId } } })
      if (response.status === 404) return null
      if (!data) throw new ProblemError(asProblem(error))
      return { link: data, etag: response.headers.get('ETag') ?? '' }
    },
  })
}

/**
 * PUT the project's docs repository, with If-Match when it already has one. Throws ProblemError: 422 with `errors` on
 * the field GitHub refused, 412 when someone else changed it meanwhile (the latest is refetched).
 */
export function useLinkProjectRepository(projectId: string, current: VersionedProjectRepository | null) {
  const { api } = useAuth()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: LinkProjectRepositoryInput): Promise<VersionedProjectRepository> => {
      const { data, error, response } = await api.PUT('/api/v1/projects/{projectId}/github', {
        params: { path: { projectId }, header: current ? { 'If-Match': current.etag } : {} },
        body: input,
      })
      if (!data) throw new ProblemError(asProblem(error))
      return { link: data, etag: response.headers.get('ETag') ?? '' }
    },
    onSuccess: (saved) => queryClient.setQueryData(projectRepositoryQueryKey(projectId), saved),
    onError: (error) => {
      if (error instanceof ProblemError && error.problem.status === 412) {
        void queryClient.invalidateQueries({ queryKey: projectRepositoryQueryKey(projectId) })
      }
    },
  })
}

/** DELETE the project's docs repository link. */
export function useUnlinkProjectRepository(projectId: string) {
  const { api } = useAuth()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async () => {
      const { error, response } = await api.DELETE('/api/v1/projects/{projectId}/github', { params: { path: { projectId } } })
      if (!response.ok) throw new ProblemError(asProblem(error))
    },
    onSuccess: () => queryClient.setQueryData(projectRepositoryQueryKey(projectId), null),
  })
}
