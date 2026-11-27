import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { components } from '../api/schema.d.ts'
import { asProblem, ProblemError } from '../api/problem.ts'
import { useAuth } from '../auth/AuthContext.ts'

export type DocsTree = components['schemas']['DocsTree']
export type DocListing = components['schemas']['DocListing']
export type DocContent = components['schemas']['DocContent']
export type DocCommit = components['schemas']['DocCommit']
export type DocSearchResults = components['schemas']['DocSearchResults']
export type DocSnippetPart = components['schemas']['DocSnippetPart']

export const docsQueryKey = (projectId: string) => ['projects', projectId, 'docs'] as const

/** The project's docs folder (TREK-75). The API refreshes its cache from GitHub when it is older than 5 minutes. */
export function useDocsTree(projectId: string) {
  const { api } = useAuth()
  return useQuery({
    queryKey: [...docsQueryKey(projectId), 'tree'],
    queryFn: async () => {
      const { data, error } = await api.GET('/api/v1/projects/{projectId}/docs', { params: { path: { projectId } } })
      if (!data) throw new ProblemError(asProblem(error))
      return data
    },
  })
}

/** Reads the folder from GitHub now; the tree, open docs and searches are then read again. */
export function useRefreshDocs(projectId: string) {
  const { api } = useAuth()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async () => {
      const { data, error } = await api.GET('/api/v1/projects/{projectId}/docs', {
        params: { path: { projectId }, query: { refresh: true } },
      })
      if (!data) throw new ProblemError(asProblem(error))
      return data
    },
    onSuccess: async (tree) => {
      queryClient.setQueryData([...docsQueryKey(projectId), 'tree'], tree)
      await queryClient.invalidateQueries({
        predicate: (query) => query.queryKey[0] === 'projects' && query.queryKey[1] === projectId && query.queryKey[2] === 'docs' && query.queryKey[3] !== 'tree',
      })
    },
  })
}

/** One doc's Markdown and last commit; off while no doc is chosen. */
export function useDoc(projectId: string, path: string | null) {
  const { api } = useAuth()
  return useQuery({
    queryKey: [...docsQueryKey(projectId), 'file', path],
    enabled: path !== null,
    queryFn: async () => {
      const { data, error } = await api.GET('/api/v1/projects/{projectId}/docs/file', {
        params: { path: { projectId }, query: { path: path ?? '' } },
      })
      if (!data) throw new ProblemError(asProblem(error))
      return data
    },
  })
}

/** Full-text search across the project's docs; off for an empty query. Keeps the last results while typing. */
export function useDocSearch(projectId: string, query: string) {
  const { api } = useAuth()
  const q = query.trim()
  return useQuery({
    queryKey: [...docsQueryKey(projectId), 'search', q],
    enabled: q.length > 0,
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const { data, error } = await api.GET('/api/v1/projects/{projectId}/docs/search', {
        params: { path: { projectId }, query: { q } },
      })
      if (!data) throw new ProblemError(asProblem(error))
      return data
    },
  })
}

/**
 * An image from the docs folder as a `data:` URL. Images need the user's token, which an `<img src>` cannot send, and
 * a `data:` URL fits the desktop app's content security policy and cannot be opened as a page of its own.
 */
export function useDocImage(projectId: string, path: string) {
  const { api } = useAuth()
  return useQuery({
    queryKey: [...docsQueryKey(projectId), 'image', path],
    staleTime: 5 * 60_000,
    queryFn: async () => {
      const { data, error, response } = await api.GET('/api/v1/projects/{projectId}/docs/image', {
        params: { path: { projectId }, query: { path } },
        parseAs: 'arrayBuffer',
      })
      if (!data) throw new ProblemError(asProblem(error))
      // The media type alone: parameters such as "; charset=utf-8" don't belong in a data: URL written this way.
      const contentType = response.headers.get('Content-Type')?.split(';')[0]?.trim() || 'application/octet-stream'
      return toDataUrl(new Uint8Array(data), contentType)
    },
  })
}

/**
 * Bytes as a base64 `data:` URL. Read as an `ArrayBuffer`, not a `Blob`: `FileReader` and `Blob` methods only work on
 * their own environment's `Blob` class, and the one `fetch` returns differs between browsers, Node and jsdom.
 */
function toDataUrl(bytes: Uint8Array, contentType: string): string {
  let binary = ''
  // In chunks: spreading a whole image into String.fromCharCode would overflow the call stack.
  for (let start = 0; start < bytes.length; start += 0x8000) {
    binary += String.fromCharCode(...bytes.subarray(start, start + 0x8000))
  }
  return `data:${contentType};base64,${btoa(binary)}`
}
