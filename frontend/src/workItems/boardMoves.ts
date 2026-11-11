import type { ClientRect, KeyboardCoordinateGetter, UniqueIdentifier } from '@dnd-kit/core'
import { sortableKeyboardCoordinates } from '@dnd-kit/sortable'
import { ProblemError } from '../api/problem.ts'
import { messages } from '../i18n/messages.ts'
import type { Board, BoardCard, BoardMove } from './useWorkItems.ts'
import { statusLabel, WORK_ITEM_STATUSES } from './workItemTypes.ts'

/** Done is always last finished first (TRK-15.1), so a card dropped there takes no place of its own. */
export const DONE = 'done'

/** Where a dropped card goes: its column, its neighbours there, and its place for announcements (1-based). */
export interface DropTarget extends BoardMove {
  position: number
  /** Cards in the column once the card is in it. */
  total: number
}

function columnOf(board: Board, id: string) {
  return board.columns.find((column) => column.status === id || column.items.some((item) => item.id === id))
}

/**
 * Where `card` lands when dropped over `overId` (a column or a card), or null when it would not move (US-11 AC3, AC4).
 * Over a card of its own column it takes that card's place; over a card of another column it goes above that card, or
 * below it when `below` (the dragged card's middle is past the other card's middle); over a column's free space it goes
 * to the bottom. Done takes no position.
 */
export function dropTarget(board: Board, card: BoardCard, overId: UniqueIdentifier, below = false): DropTarget | null {
  const id = String(overId)
  const column = columnOf(board, id)
  if (!column || id === card.id) return null
  const to = column.status
  const others = column.items.filter((item) => item.id !== card.id)
  const sameColumn = others.length !== column.items.length

  if (to === DONE) {
    return sameColumn ? null : { card, to, afterId: null, beforeId: null, position: 1, total: others.length + 1 }
  }

  let index: number
  if (id === to) {
    if (sameColumn) return null
    index = others.length
  } else if (sameColumn) {
    // Taking the place of the card it is over: arrayMove, as the sortable list shows while dragging.
    index = column.items.findIndex((item) => item.id === id)
  } else {
    index = others.findIndex((item) => item.id === id) + (below ? 1 : 0)
  }

  if (sameColumn && index === column.items.findIndex((item) => item.id === card.id)) return null
  return {
    card,
    to,
    afterId: others[index - 1]?.id ?? null,
    beforeId: others[index]?.id ?? null,
    position: index + 1,
    total: others.length + 1,
  }
}

/** Whether the dragged card's middle is below the middle of the card it is over. */
export function isBelow(dragged: ClientRect | null | undefined, over: ClientRect | null | undefined): boolean {
  if (!dragged || !over) return false
  return dragged.top + dragged.height / 2 > over.top + over.height / 2
}

/** The board as it will look once `move` succeeds: the card in its new place, and the counts changed. */
export function applyMove(board: Board, { card, to, afterId }: BoardMove): Board {
  // The re-read after a save can land first; the board then already has the card there, counted.
  const from = board.columns.find((column) => column.items.some((item) => item.id === card.id))?.status
  return {
    columns: board.columns.map((column) => {
      const others = column.items.filter((item) => item.id !== card.id)
      if (column.status === to) {
        const at = afterId === null ? 0 : others.findIndex((item) => item.id === afterId) + 1
        return {
          ...column,
          count: from === to ? column.count : Number(column.count) + 1,
          items: [...others.slice(0, at), { ...card, status: to }, ...others.slice(at)],
        }
      }
      if (others.length !== column.items.length) return { ...column, count: Number(column.count) - 1, items: others }
      return column
    }),
  }
}

/** Why a card went back, in words the user can act on (US-11 AC3). */
export function moveErrorMessage(error: Error, { card }: BoardMove): string {
  if (!(error instanceof ProblemError)) return messages.common.unexpectedError
  const { status, allowedTransitions } = error.problem
  if (status === 409) return messages.board.moveConflict(card.key)
  if (status === 412) return messages.board.moveStale(card.key)
  if (status === 422 && allowedTransitions && allowedTransitions.length > 0) {
    return messages.board.moveNotAllowed(card.key, allowedTransitions.map(statusLabel).join(messages.workItemForm.or))
  }
  return messages.common.unexpectedError
}

/**
 * Keyboard moves on the board (US-11 AC5): left and right arrows jump a picked-up card to the neighbouring column in
 * workflow order; up and down move it past the next card, as the sortable list does.
 */
export const boardKeyboardCoordinates: KeyboardCoordinateGetter = (event, args) =>
  event.code === 'ArrowRight' || event.code === 'ArrowLeft'
    ? columnKeyboardCoordinates(event, args)
    : sortableKeyboardCoordinates(event, args)

/** Left and right arrows jump a picked-up card to the neighbouring column in workflow order (US-11 AC5). */
export const columnKeyboardCoordinates: KeyboardCoordinateGetter = (event, { context }) => {
  if (event.code !== 'ArrowRight' && event.code !== 'ArrowLeft') return undefined
  event.preventDefault()
  const { over, droppableRects, droppableContainers, collisionRect } = context
  // Over a card, its column is the card's status.
  const current = WORK_ITEM_STATUSES.indexOf(String(over?.data.current?.status ?? over?.id))
  if (current === -1 || !collisionRect) return undefined
  const next = WORK_ITEM_STATUSES[current + (event.code === 'ArrowRight' ? 1 : -1)]
  const rect = next === undefined ? undefined : droppableRects.get(next)
  if (next === undefined || !rect) return undefined
  // Stacked columns on a narrow screen can be off screen; the card stays at its height when it still fits the column.
  droppableContainers.get(next)?.node.current?.scrollIntoView({ block: 'nearest' })
  const lowest = Math.max(rect.top, rect.bottom - collisionRect.height)
  return { x: rect.left, y: Math.min(Math.max(collisionRect.top, rect.top), lowest) }
}
