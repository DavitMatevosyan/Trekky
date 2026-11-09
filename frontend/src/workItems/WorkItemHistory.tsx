import { useId } from 'react'
import { RelativeTime } from '../components/RelativeTime.tsx'
import { Button } from '../components/ui/Button.tsx'
import { messages } from '../i18n/messages.ts'
import { actorName, historySentence } from './historySentence.ts'
import { useWorkItemHistory } from './useWorkItemHistory.ts'

/** The item's history, newest first: who, when and what changed, one sentence per event (US-09 AC2). */
export function WorkItemHistory({ workItemId, updatedAt }: { workItemId: string; updatedAt: string }) {
  const headingId = useId()
  const history = useWorkItemHistory(workItemId, updatedAt)
  // Each retry of a query with no data goes back to pending. Counting settled reads keeps one steady state until a read
  // succeeds, so the alert is not removed and announced again by every retry.
  const loadFailed = history.errorUpdateCount > 0 && history.dataUpdatedAt === 0
  const events = loadFailed ? [] : (history.data?.pages.flatMap((page) => page.items) ?? [])

  return (
    <section aria-labelledby={headingId} className="grid gap-2">
      <h2 id={headingId} className="font-semibold">
        {messages.workItemHistory.title}
      </h2>

      {history.isPending && !loadFailed && <p className="text-sm text-muted-foreground">{messages.workItemHistory.loading}</p>}
      {loadFailed && <p role="alert">{messages.workItemHistory.loadFailed}</p>}
      {history.isSuccess && !loadFailed && events.length === 0 && <p className="text-sm text-muted-foreground">{messages.workItemHistory.empty}</p>}

      {events.length > 0 && (
        <ul aria-labelledby={headingId} className="divide-y divide-border rounded-xl border border-border text-sm">
          {events.map((event) => (
            <li key={event.eventId} className="flex flex-wrap items-baseline justify-between gap-x-4 px-3 py-2">
              <span className="min-w-0 break-words">
                <span className="font-medium">{actorName(event.actor)}</span> {historySentence(event)}
              </span>
              <span className="whitespace-nowrap text-muted-foreground">
                <RelativeTime value={event.occurredAt} />
              </span>
            </li>
          ))}
        </ul>
      )}

      {history.isFetchNextPageError && <p role="alert">{messages.workItemHistory.loadFailed}</p>}
      {history.hasNextPage && !loadFailed && (
        <Button variant="outline" className="justify-self-center" onClick={() => void history.fetchNextPage()} disabled={history.isFetchingNextPage}>
          {messages.workItemHistory.loadMore}
        </Button>
      )}
    </section>
  )
}
