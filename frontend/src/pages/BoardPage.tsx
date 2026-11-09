import { useId, useRef, useState } from 'react'
import { Link, useParams } from 'react-router'
import { Button } from '../components/ui/Button.tsx'
import { messages } from '../i18n/messages.ts'
import { ProjectHeader } from '../projects/ProjectHeader.tsx'
import { ProjectLoader } from '../projects/ProjectLoader.tsx'
import { useBoard, type BoardColumn as BoardColumnData } from '../workItems/useWorkItems.ts'
import { statusLabel, typeLabel } from '../workItems/workItemTypes.ts'

/** A project's board, read-only (US-11 AC1, wireframes: Board); dragging arrives with TRK-16. */
export function BoardPage() {
  const { projectKey = '' } = useParams()
  return (
    <ProjectLoader projectKey={projectKey}>
      {/* Keyed by project, so "Show all" and the cards never carry over to another project's board. */}
      {(project) => <Board key={project.id} projectId={project.id} projectKey={project.key} projectName={project.name} />}
    </ProjectLoader>
  )
}

function Board({ projectId, projectKey, projectName }: { projectId: string; projectKey: string; projectName: string }) {
  const [wantsAllDone, setWantsAllDone] = useState(false)
  const latest = useBoard(projectId, false)
  // A separate query, so the board stays on screen while every Done card loads and if that fails.
  const allDone = useBoard(projectId, true, wantsAllDone)
  const board = (wantsAllDone ? allDone.data : undefined) ?? latest.data

  function showAll() {
    if (wantsAllDone) void allDone.refetch()
    else setWantsAllDone(true)
  }

  return (
    <section className="grid gap-6">
      <ProjectHeader projectKey={projectKey} projectName={projectName} />

      {latest.isPending && <p className="text-muted-foreground">{messages.board.loading}</p>}
      {latest.isError && !board && <p role="alert">{messages.common.unexpectedError}</p>}

      {board && (
        <div className="grid items-start gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {board.columns.map((column) => (
            <BoardColumn
              key={column.status}
              column={column}
              projectKey={projectKey}
              loadingAll={allDone.isFetching}
              showAllFailed={wantsAllDone && allDone.isError && allDone.data === undefined}
              onShowAll={column.status === 'done' ? showAll : undefined}
            />
          ))}
        </div>
      )}
    </section>
  )
}

interface BoardColumnProps {
  column: BoardColumnData
  projectKey: string
  /** Every Done card has been asked for and is still loading. */
  loadingAll: boolean
  showAllFailed: boolean
  onShowAll: (() => void) | undefined
}

function BoardColumn({ column, projectKey, loadingAll, showAllFailed, onShowAll }: BoardColumnProps) {
  const headingId = useId()
  const heading = useRef<HTMLHeadingElement>(null)
  const label = statusLabel(column.status)
  const count = Number(column.count)
  const canShowAll = onShowAll !== undefined && count > column.items.length

  function showAll() {
    onShowAll?.()
    // The button goes away once every card is here; keep focus in the column (WCAG 2.4.3).
    heading.current?.focus()
  }

  return (
    <section aria-labelledby={headingId} className="grid gap-2 rounded-xl border border-border p-3">
      <h2 id={headingId} ref={heading} tabIndex={-1} className="text-sm font-semibold">
        {messages.board.columnHeading(label, count)}
      </h2>

      {/* Under the heading, where "Show all" moves the focus and the scroll position. */}
      {canShowAll && showAllFailed && (
        <p role="alert" className="text-sm">
          {messages.board.showAllFailed}
        </p>
      )}

      {column.items.length === 0 ? (
        <p className="text-sm text-muted-foreground">{messages.board.emptyColumn}</p>
      ) : (
        <ul aria-label={label} className="grid gap-2">
          {column.items.map((item) => (
            <li key={item.id} className="relative grid gap-1 rounded-lg border border-border p-2 text-sm hover:bg-muted">
              <div className="flex items-baseline justify-between gap-2">
                {/* The key is the link; it stretches over the card so the whole card opens the item, with one tab stop. */}
                <Link
                  to={`/${projectKey}/items/${item.key}`}
                  className="font-mono font-semibold underline-offset-4 after:absolute after:inset-0 hover:underline"
                >
                  {item.key}
                </Link>
                <span className="text-muted-foreground">{typeLabel(item.type)}</span>
              </div>
              <p className="line-clamp-3 break-words">{item.title}</p>
            </li>
          ))}
        </ul>
      )}

      {canShowAll && (
        <Button variant="outline" size="sm" onClick={showAll} disabled={loadingAll}>
          {messages.board.showAll}
        </Button>
      )}
    </section>
  )
}
