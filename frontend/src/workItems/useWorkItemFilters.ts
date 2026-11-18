import { useCallback, useMemo } from 'react'
import { useSearchParams } from 'react-router'
import { readFilters, writeFilters, type WorkItemFilters } from './workItemFilters.ts'

export type SetWorkItemFilters = (next: WorkItemFilters, options?: { replace?: boolean }) => void

/**
 * The filters kept in the page's URL (TREK-53), so a filtered view can be shared and survives reload. Each change is a
 * history entry, so back and forward step through them; `replace` is for typing, which would otherwise add one per key.
 */
export function useWorkItemFilters(): [WorkItemFilters, SetWorkItemFilters] {
  const [searchParams, setSearchParams] = useSearchParams()
  const search = searchParams.toString()
  // Read again only when the URL changes, so the object stays the same between renders.
  const filters = useMemo(() => readFilters(new URLSearchParams(search)), [search])
  const setFilters = useCallback<SetWorkItemFilters>(
    (next, options) => setSearchParams((current) => writeFilters(current, next), { replace: options?.replace ?? false }),
    [setSearchParams],
  )
  return [filters, setFilters]
}
