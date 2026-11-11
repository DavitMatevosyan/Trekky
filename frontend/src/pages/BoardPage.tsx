import {
  closestCorners,
  DndContext,
  DragOverlay,
  KeyboardSensor,
  MouseSensor,
  pointerWithin,
  rectIntersection,
  TouchSensor,
  useDroppable,
  useSensor,
  useSensors,
  type Active,
  type Announcements,
  type CollisionDetection,
  type DragEndEvent,
  type DragStartEvent,
  type Over,
  type UniqueIdentifier,
} from '@dnd-kit/core'
import { SortableContext, useSortable, verticalListSortingStrategy, type SortingStrategy } from '@dnd-kit/sortable'
import { useEffect, useId, useRef, useState, type ReactNode, type RefObject } from 'react'
import { Link, useLocation, useParams, useSearchParams } from 'react-router'
import { Button } from '../components/ui/Button.tsx'
import { messages } from '../i18n/messages.ts'
import { cn } from '../lib/cn.ts'
import { ProjectLoader } from '../projects/ProjectLoader.tsx'
import { ProjectView } from '../projects/ProjectView.tsx'
import { applyMove, boardKeyboardCoordinates, DONE, dropTarget, isBelow, moveErrorMessage } from '../workItems/boardMoves.ts'
import {
  useBoard,
  useMoveBoardCard,
  type Board as BoardData,
  type BoardCard as BoardCardData,
  type BoardColumn as BoardColumnData,
} from '../workItems/useWorkItems.ts'
import { statusLabel, typeLabel, WORK_ITEM_STATUSES } from '../workItems/workItemTypes.ts'

const DONE_PARAM = 'done'
const DONE_ALL = 'all'

const isColumn = (id: UniqueIdentifier) => WORK_ITEM_STATUSES.includes(String(id))

/**
 * A pointer drop goes to the card under the pointer, else the column; a keyboard move to the nearest card or column,
 * like the sortable list's own arrow keys.
 */
const collisionDetection: CollisionDetection = (args) => {
  if (!args.pointerCoordinates) return closestCorners(args)
  const within = pointerWithin(args)
  const card = within.find((hit) => !isColumn(hit.id))
  if (card) return [card]
  return within.length > 0 ? within : rectIntersection(args)
}

/** Done is last finished first: its cards make no room for a card dragged over them. */
const keepInPlace: SortingStrategy = () => null

/** A project's board (US-11, wireframes: Board): cards move between columns and within one by mouse, touch or keyboard. */
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
  // In the URL, so "Show all" survives opening a card and coming back.
  const [searchParams, setSearchParams] = useSearchParams()
  const { pathname, search } = useLocation()
  const wantsAllDone = searchParams.get(DONE_PARAM) === DONE_ALL
  const latest = useBoard(projectId, false)
  // A separate query, so the board stays on screen while every Done card loads and if that fails.
  const allDone = useBoard(projectId, true, wantsAllDone)
  const shown = wantsAllDone && allDone.data ? allDone : latest
  const move = useMoveBoardCard(projectId)
  const [draggingId, setDraggingId] = useState<string | null>(null)
  // A card remounts when it changes column, also when it goes back; the keyboard user's focus has to go with it.
  const focusAfterMove = useRef<string | null>(null)
  useEffect(() => {
    if (!move.isPending) focusAfterMove.current = null
  }, [move.isPending])

  // While saving, show the card where it is going; if the save fails it goes back by itself (US-11 AC3).
  const board = shown.data && move.isPending ? applyMove(shown.data, move.variables) : shown.data
  const cards = new Map((board?.columns ?? []).flatMap((column) => column.items.map((card) => [card.id, card] as const)))
  const dragging = draggingId === null ? undefined : cards.get(draggingId)

  const sensors = useSensors(
    // A small movement or a long press starts a drag, so clicks still open the card and touch still scrolls.
    useSensor(MouseSensor, { activationConstraint: { distance: 8 } }),
    useSensor(TouchSensor, { activationConstraint: { delay: 200, tolerance: 8 } }),
    // Space picks up, because Enter on the card's link must still open it. Mid-drag, Enter and Tab drop as usual.
    useSensor(KeyboardSensor, {
      keyboardCodes: { start: ['Space'], cancel: ['Escape'], end: ['Space', 'Enter', 'Tab'] },
      coordinateGetter: boardKeyboardCoordinates,
    }),
  )

  const keyOf = (id: UniqueIdentifier) => cards.get(String(id))?.key ?? ''
  /** What a drop here would do, and the column's name, for screen readers (US-11 AC5). */
  const placeOf = (active: Active, over: Over) => {
    const card = cards.get(String(active.id))
    const target = board && card ? targetOf(board, card, active, over) : null
    const column = target?.to ?? (isColumn(over.id) ? String(over.id) : cards.get(String(over.id))?.status) ?? ''
    return { key: keyOf(active.id), status: statusLabel(column), target }
  }
  const announcements: Announcements = {
    onDragStart: ({ active }) => messages.board.pickedUp(keyOf(active.id)),
    onDragOver: ({ active, over }) => {
      if (!over) return undefined
      const { key, status, target } = placeOf(active, over)
      return target && target.to !== DONE && target.total > 1
        ? messages.board.overPlace(key, status, target.position, target.total)
        : messages.board.overColumn(key, status)
    },
    onDragEnd: ({ active, over }) => {
      if (!over) return messages.board.dropCancelled(keyOf(active.id))
      const { key, status, target } = placeOf(active, over)
      return target && target.to !== DONE && target.total > 1
        ? messages.board.droppedAt(key, status, target.position, target.total)
        : messages.board.droppedOn(key, status)
    },
    onDragCancel: ({ active }) => messages.board.dropCancelled(keyOf(active.id)),
  }

  function startDrag({ active }: DragStartEvent) {
    move.reset()
    setDraggingId(String(active.id))
  }

  function drop({ active, over, activatorEvent }: DragEndEvent) {
    setDraggingId(null)
    const card = cards.get(String(active.id))
    const target = board && card && over ? targetOf(board, card, active, over) : null
    if (!card || !target) return
    if (activatorEvent instanceof KeyboardEvent) focusAfterMove.current = card.id
    move.mutate({ card, to: target.to, afterId: target.afterId, beforeId: target.beforeId })
  }

  function showAll() {
    if (wantsAllDone) void allDone.refetch()
    else setSearchParams({ [DONE_PARAM]: DONE_ALL }, { replace: true })
  }

  return (
    <ProjectView projectId={projectId} projectKey={projectKey} projectName={projectName}>
      {!board && latest.isPending && <p className="text-muted-foreground">{messages.board.loading}</p>}
      {shown.isError && !board && <p role="alert">{messages.common.unexpectedError}</p>}
      {shown.isError && board && (
        <p role="alert" className="text-sm">
          {messages.board.refreshFailed}
        </p>
      )}
      {move.isError && (
        <p role="alert" className="text-sm text-destructive">
          {moveErrorMessage(move.error, move.variables)}
        </p>
      )}

      {board && (
        <DndContext
          sensors={sensors}
          collisionDetection={collisionDetection}
          accessibility={{ announcements, screenReaderInstructions: { draggable: messages.board.dragInstructions } }}
          onDragStart={startDrag}
          onDragEnd={drop}
          onDragCancel={() => setDraggingId(null)}
        >
          <div aria-busy={move.isPending || undefined} className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {board.columns.map((column) => (
              <BoardColumn
                key={column.status}
                column={column}
                projectKey={projectKey}
                boardUrl={pathname + search}
                moving={move.isPending}
                focusAfterMove={focusAfterMove}
                loadingAll={allDone.isFetching}
                showAllFailed={wantsAllDone && allDone.isError && allDone.data === undefined}
                onShowAll={column.status === 'done' ? showAll : undefined}
              />
            ))}
          </div>
          <DragOverlay dropAnimation={null}>
            {dragging && (
              <div className="grid cursor-grabbing gap-1 rounded-lg border border-border bg-background p-2 text-sm shadow-lg">
                <CardHeader keyLabel={<span className="font-mono font-semibold">{dragging.key}</span>} type={dragging.type} />
                <p className="line-clamp-3 break-words">{dragging.title}</p>
              </div>
            )}
          </DragOverlay>
        </DndContext>
      )}
    </ProjectView>
  )
}

interface BoardColumnProps {
  column: BoardColumnData
  projectKey: string
  /** Where the item page's breadcrumb returns to. */
  boardUrl: string
  /** A card is being saved; no other can be picked up until it is. */
  moving: boolean
  /** The card that takes keyboard focus when it appears in a column. */
  focusAfterMove: RefObject<string | null>
  /** Every Done card has been asked for and is still loading. */
  loadingAll: boolean
  showAllFailed: boolean
  onShowAll: (() => void) | undefined
}

function BoardColumn({ column, projectKey, boardUrl, moving, focusAfterMove, loadingAll, showAllFailed, onShowAll }: BoardColumnProps) {
  const headingId = useId()
  const heading = useRef<HTMLHeadingElement>(null)
  const { setNodeRef, isOver } = useDroppable({ id: column.status })
  const label = statusLabel(column.status)
  const count = Number(column.count)
  const canShowAll = onShowAll !== undefined && count > column.items.length

  function showAll() {
    onShowAll?.()
    // The button goes away once every card is here; keep focus in the column (WCAG 2.4.3).
    heading.current?.focus()
  }

  return (
    <section
      ref={setNodeRef}
      aria-labelledby={headingId}
      className={cn('grid content-start gap-2 rounded-xl border border-border p-3', isOver && 'ring-2 ring-ring')}
    >
      <h2 id={headingId} ref={heading} tabIndex={-1} className="text-sm font-semibold">
        {messages.board.columnHeading(label, count)}
      </h2>

      {/* Under the heading, where "Show all" moves the focus and the scroll position, so retrying is one Tab away. */}
      {canShowAll && showAllFailed && (
        <>
          <p role="alert" className="text-sm">
            {messages.board.showAllFailed}
          </p>
          <Button variant="outline" size="sm" onClick={showAll} disabled={loadingAll}>
            {messages.board.tryAgain}
          </Button>
        </>
      )}

      {column.items.length === 0 ? (
        <p className="text-sm text-muted-foreground">{messages.board.emptyColumn}</p>
      ) : (
        <SortableContext items={column.items.map((item) => item.id)} strategy={column.status === DONE ? keepInPlace : verticalListSortingStrategy}>
          <ul aria-label={label} className="grid gap-2">
            {column.items.map((item) => (
              <BoardCard key={item.id} card={item} projectKey={projectKey} boardUrl={boardUrl} moving={moving} focusAfterMove={focusAfterMove} />
            ))}
          </ul>
        </SortableContext>
      )}

      {canShowAll && !showAllFailed && (
        <Button variant="outline" size="sm" onClick={showAll} disabled={loadingAll}>
          {messages.board.showAll}
        </Button>
      )}
    </section>
  )
}

interface BoardCardProps {
  card: BoardCardData
  projectKey: string
  boardUrl: string
  moving: boolean
  focusAfterMove: RefObject<string | null>
}

function BoardCard({ card, projectKey, boardUrl, moving, focusAfterMove }: BoardCardProps) {
  const { attributes, listeners, setNodeRef, setActivatorNodeRef, isDragging, transform, transition } = useSortable({
    id: card.id,
    // The column, for the arrow keys that change column.
    data: { status: card.status },
    // Done is last finished first, so its cards are no drop places of their own: a card dropped there goes to the column.
    disabled: { draggable: moving, droppable: card.status === DONE },
  })
  const link = useRef<HTMLAnchorElement | null>(null)
  useEffect(() => {
    // Only when the card's own remount dropped the focus: not if the user has moved on while it saved.
    if (focusAfterMove.current === card.id && document.activeElement === document.body) link.current?.focus()
  }, [card.id, focusAfterMove])

  return (
    <li
      ref={setNodeRef}
      {...listeners}
      style={{ transform: transform ? `translate3d(${transform.x}px, ${transform.y}px, 0)` : undefined, transition }}
      className={cn(
        'relative grid cursor-grab touch-manipulation gap-1 rounded-lg border border-border p-2 text-sm select-none [-webkit-touch-callout:none] hover:bg-muted',
        isDragging && 'opacity-40',
      )}
    >
      <CardHeader
        keyLabel={
          // The key is the link and the keyboard handle; it stretches over the card, so the card has one tab stop.
          <Link
            ref={(node) => {
              setActivatorNodeRef(node)
              link.current = node
            }}
            to={`/${projectKey}/items/${card.key}`}
            state={{ backTo: boardUrl }}
            draggable={false}
            aria-describedby={attributes['aria-describedby']}
            aria-roledescription={messages.board.draggableCard}
            className="font-mono font-semibold underline-offset-4 after:absolute after:inset-0 hover:underline"
          >
            {card.key}
          </Link>
        }
        type={card.type}
      />
      <p className="line-clamp-3 break-words">{card.title}</p>
    </li>
  )
}

/** Where a drop over `over` puts the card: below the card it is over when its middle is past that card's middle. */
function targetOf(board: BoardData, card: BoardCardData, active: Active, over: Over) {
  return dropTarget(board, card, over.id, isBelow(active.rect.current.translated, over.rect))
}

function CardHeader({ keyLabel, type }: { keyLabel: ReactNode; type: string }) {
  return (
    <div className="flex items-baseline justify-between gap-2">
      {keyLabel}
      <span className="text-muted-foreground">{typeLabel(type)}</span>
    </div>
  )
}
