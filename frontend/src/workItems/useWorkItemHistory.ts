import { keepPreviousData, useInfiniteQuery } from '@tanstack/react-query'
import { asProblem, ProblemError } from '../api/problem.ts'
import type { components } from '../api/schema.d.ts'
import { useAuth } from '../auth/AuthContext.ts'

export type HistoryEvent = components['schemas']['HistoryEvent']

// Events reach the history through the outbox, a moment after the change is saved (the dispatcher runs every 500 ms).
const CATCH_UP_INTERVAL_MS = 500
const CATCH_UP_ATTEMPTS = 10

/** Every change to an item stamps the item and its event with the same time, so an older newest event means one is on its way. */
function isBehind(newestEventAt: string | undefined, updatedAt: string): boolean {
  return newestEventAt === undefined || Date.parse(newestEventAt) < Date.parse(updatedAt)
}

/**
 * An item's history, newest first, 50 at a time (US-09 AC2). Each version of the item (`updatedAt`) is its own query, so
 * a change starts again from the newest page and the attempts count from that change. Until the change's event has
 * arrived the query checks again, a bounded number of times: a change may have no event, and must not poll forever.
 */
export function useWorkItemHistory(workItemId: string, updatedAt: string) {
  const { api } = useAuth()
  return useInfiniteQuery({
    queryKey: ['work-items', 'history', workItemId, updatedAt] as const,
    initialPageParam: undefined as string | undefined,
    placeholderData: keepPreviousData,
    queryFn: async ({ pageParam }) => {
      const { data, error } = await api.GET('/api/v1/work-items/{workItemId}/history', {
        params: { path: { workItemId }, query: pageParam ? { cursor: pageParam } : {} },
      })
      if (!data) throw new ProblemError(asProblem(error))
      return data
    },
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
    refetchInterval: ({ state }) => {
      if (state.dataUpdateCount + state.errorUpdateCount >= CATCH_UP_ATTEMPTS) return false
      // No data yet counts as behind, so a read that failed is tried again within the same attempts.
      return isBehind(state.data?.pages[0]?.items[0]?.occurredAt, updatedAt) ? CATCH_UP_INTERVAL_MS : false
    },
  })
}
