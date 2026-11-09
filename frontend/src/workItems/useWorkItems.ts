import { keepPreviousData, useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { asProblem, ProblemError } from '../api/problem.ts'
import type { components } from '../api/schema.d.ts'
import { useAuth } from '../auth/AuthContext.ts'
import { projectsQueryKey } from '../projects/useProjects.ts'

export type WorkItemSummary = components['schemas']['WorkItemSummary']
export type WorkItemDetail = components['schemas']['WorkItemDetail']
export type CreateWorkItemInput = components['schemas']['CreateWorkItemRequest']
export type UpdateWorkItemInput = components['schemas']['UpdateWorkItemRequest']
export type Board = components['schemas']['Board']
export type BoardColumn = components['schemas']['BoardColumn']
export type BoardCard = components['schemas']['BoardCard']

/** A card going to another column of the board. */
export interface BoardMove {
  card: BoardCard
  to: string
}

/** A work item with the ETag to send back in If-Match when editing it (US-07 AC1). */
export interface VersionedWorkItem {
  item: WorkItemDetail
  etag: string
}

export const projectByKeyQueryKey = (key: string) => ['projects', 'by-key', key] as const
export const workItemsQueryKey = (projectId: string) => ['work-items', 'list', projectId] as const
export const workItemQueryKey = (key: string) => ['work-items', 'by-key', key] as const
export const boardQueryKey = (projectId: string) => ['work-items', 'board', projectId] as const

/** The project behind a URL like /TRK (GET /api/v1/projects/by-key/{key}). */
export function useProjectByKey(key: string) {
  const { api } = useAuth()
  return useQuery({
    queryKey: projectByKeyQueryKey(key),
    queryFn: async () => {
      const { data, error } = await api.GET('/api/v1/projects/by-key/{key}', { params: { path: { key } } })
      if (!data) throw new ProblemError(asProblem(error))
      return data
    },
  })
}

/** A project's items, newest update first, 50 at a time (US-08). */
export function useWorkItems(projectId: string | undefined) {
  const { api } = useAuth()
  return useInfiniteQuery({
    queryKey: workItemsQueryKey(projectId ?? ''),
    enabled: projectId !== undefined,
    initialPageParam: undefined as string | undefined,
    queryFn: async ({ pageParam }) => {
      const { data, error } = await api.GET('/api/v1/projects/{projectId}/work-items', {
        params: { path: { projectId: projectId ?? '' }, query: pageParam ? { cursor: pageParam } : {} },
      })
      if (!data) throw new ProblemError(asProblem(error))
      return data
    },
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
  })
}

/** The project's board (US-11); `allDone` asks for every Done card instead of the latest 20. */
export function useBoard(projectId: string, allDone: boolean, enabled = true) {
  const { api } = useAuth()
  return useQuery({
    queryKey: [...boardQueryKey(projectId), allDone] as const,
    enabled,
    // Dropped when the board closes: an old full board must not paint over newer cards on the next visit.
    ...(allDone ? { gcTime: 0 } : {}),
    queryFn: async () => {
      const { data, error } = await api.GET('/api/v1/projects/{projectId}/board', {
        params: { path: { projectId }, query: allDone ? { allDone } : {} },
      })
      if (!data) throw new ProblemError(asProblem(error))
      return data
    },
  })
}

/**
 * Moves a board card to another status with the card's ETag (US-11 AC3). Throws ProblemError: 412 when someone else
 * changed it, 422 when the workflow does not allow the move. Stays pending until the board has been read again.
 */
export function useMoveBoardCard(projectId: string) {
  const { api } = useAuth()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ card, to }: BoardMove) => {
      const { data, error } = await api.POST('/api/v1/work-items/{workItemId}/status', {
        params: { path: { workItemId: card.id }, header: { 'If-Match': card.etag } },
        body: { to },
      })
      if (!data) throw new ProblemError(asProblem(error))
      return data
    },
    onSettled: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: boardQueryKey(projectId) }),
        queryClient.invalidateQueries({ queryKey: workItemsQueryKey(projectId) }),
        queryClient.invalidateQueries({ queryKey: ['work-items', 'by-key'] }),
      ]),
  })
}

/** One item by its key, with its ETag (US-09 AC3: URLs carry the key). */
export function useWorkItem(key: string, enabled = true) {
  const { api } = useAuth()
  return useQuery({
    queryKey: workItemQueryKey(key),
    enabled,
    queryFn: async (): Promise<VersionedWorkItem> => {
      const { data, error, response } = await api.GET('/api/v1/work-items/by-key/{key}', { params: { path: { key } } })
      if (!data) throw new ProblemError(asProblem(error))
      return { item: data, etag: response.headers.get('ETag') ?? '' }
    },
  })
}

/** Looks up an item by the key a user typed; `null` when there is no such item. */
export function useFindWorkItem() {
  const { api } = useAuth()
  return async (key: string): Promise<WorkItemDetail | null> => {
    const { data, response } = await api.GET('/api/v1/work-items/by-key/{key}', { params: { path: { key } } })
    if (data) return data
    if (response.status === 404) return null
    throw new ProblemError({ status: response.status })
  }
}

const CANDIDATE_LIMIT = 20

/**
 * Items that could be the parent (TRK-11.1): the project's items of the allowed `types` whose key starts with or whose
 * title contains `search`, newest update first. Keeps showing the last results while the next search loads.
 */
export function useParentCandidates(projectId: string, types: readonly string[], search: string, enabled: boolean) {
  const { api } = useAuth()
  return useQuery({
    queryKey: ['work-items', 'candidates', projectId, types, search] as const,
    enabled: enabled && types.length > 0,
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const { data, error } = await api.GET('/api/v1/projects/{projectId}/work-items', {
        params: { path: { projectId }, query: { type: [...types], q: search || undefined, limit: CANDIDATE_LIMIT } },
      })
      if (!data) throw new ProblemError(asProblem(error))
      return data.items
    },
  })
}

/** POST a new item; refreshes the list and the project's item count. Throws ProblemError (400, 404, 422). */
export function useCreateWorkItem(projectId: string) {
  const { api } = useAuth()
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: CreateWorkItemInput) => {
      const { data, error } = await api.POST('/api/v1/projects/{projectId}/work-items', {
        params: { path: { projectId } },
        body: input,
      })
      if (!data) throw new ProblemError(asProblem(error))
      return data
    },
    onSuccess: async (created) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: workItemsQueryKey(projectId) }),
        queryClient.invalidateQueries({ queryKey: boardQueryKey(projectId) }),
        queryClient.invalidateQueries({ queryKey: projectsQueryKey }),
        created.parentKey ? queryClient.invalidateQueries({ queryKey: workItemQueryKey(created.parentKey) }) : undefined,
      ])
    },
  })
}

/**
 * PATCH an item as a JSON merge patch with If-Match (US-07). Throws ProblemError: 412 when someone else changed it
 * (the item is then reloaded so the user sees the latest version), 422 for a parent rule, 400 for a field.
 */
export function useUpdateWorkItem(current: VersionedWorkItem) {
  const { api } = useAuth()
  const { item, etag } = current
  return useSaveWorkItem(current, async (changes: UpdateWorkItemInput) => {
    const { data, error, response } = await api.PATCH('/api/v1/work-items/{workItemId}', {
      params: { path: { workItemId: item.id }, header: { 'If-Match': etag } },
      body: changes,
      headers: { 'Content-Type': 'application/merge-patch+json' },
    })
    if (!data) throw new ProblemError(asProblem(error))
    return { item: data, etag: response.headers.get('ETag') ?? '' }
  })
}

/**
 * Moves an item to another status with If-Match (US-10). Throws ProblemError: 412 when someone else changed it (the
 * item is then reloaded), 422 when the workflow does not allow the move (`allowedTransitions` lists what it does).
 */
export function useChangeWorkItemStatus(current: VersionedWorkItem) {
  const { api } = useAuth()
  const { item, etag } = current
  return useSaveWorkItem(current, async (to: string) => {
    const { data, error, response } = await api.POST('/api/v1/work-items/{workItemId}/status', {
      params: { path: { workItemId: item.id }, header: { 'If-Match': etag } },
      body: { to },
    })
    if (!data) throw new ProblemError(asProblem(error))
    return { item: data, etag: response.headers.get('ETag') ?? '' }
  })
}

/** Saves a change to `current` and keeps the caches in step: the item itself, the list, the board, and items that show it. */
function useSaveWorkItem<TInput>(current: VersionedWorkItem, save: (input: TInput) => Promise<VersionedWorkItem>) {
  const queryClient = useQueryClient()
  const { item } = current
  return useMutation({
    mutationFn: save,
    onSuccess: (updated) => queryClient.setQueryData(workItemQueryKey(item.key), updated),
    onError: (error) => {
      if (error instanceof ProblemError && error.problem.status === 412) {
        void queryClient.invalidateQueries({ queryKey: workItemQueryKey(item.key) })
      }
    },
    onSettled: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: workItemsQueryKey(item.projectId) }),
        queryClient.invalidateQueries({ queryKey: boardQueryKey(item.projectId) }),
        // Parent and children show this item's title, type and key.
        queryClient.invalidateQueries({ queryKey: ['work-items', 'by-key'], predicate: (query) => query.queryKey[2] !== item.key }),
      ]),
  })
}
