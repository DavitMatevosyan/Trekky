import type { KeyboardCoordinateGetter } from '@dnd-kit/core'
import { ProblemError } from '../api/problem.ts'
import { messages } from '../i18n/messages.ts'
import type { Board, BoardMove } from './useWorkItems.ts'
import { statusLabel, WORK_ITEM_STATUSES } from './workItemTypes.ts'

/** The board as it will look once `move` succeeds: the card on top of its new column, and both counts changed. */
export function applyMove(board: Board, { card, to }: BoardMove): Board {
  // The re-read after a save can land first; the board then already has the card there, counted.
  if (board.columns.some((column) => column.status === to && column.items.some((item) => item.id === card.id))) return board
  return {
    columns: board.columns.map((column) => {
      const others = column.items.filter((item) => item.id !== card.id)
      if (column.status === to) return { ...column, count: Number(column.count) + 1, items: [{ ...card, status: to }, ...others] }
      if (others.length !== column.items.length) return { ...column, count: Number(column.count) - 1, items: others }
      return column
    }),
  }
}

/** Why a card went back, in words the user can act on (US-11 AC3). */
export function moveErrorMessage(error: Error, { card }: BoardMove): string {
  if (!(error instanceof ProblemError)) return messages.common.unexpectedError
  const { status, allowedTransitions } = error.problem
  if (status === 412) return messages.board.moveStale(card.key)
  if (status === 422 && allowedTransitions && allowedTransitions.length > 0) {
    return messages.board.moveNotAllowed(card.key, allowedTransitions.map(statusLabel).join(messages.workItemForm.or))
  }
  return messages.common.unexpectedError
}

/** Left and right arrows jump a picked-up card to the neighbouring column in workflow order (US-11 AC5). */
export const columnKeyboardCoordinates: KeyboardCoordinateGetter = (event, { context }) => {
  if (event.code !== 'ArrowRight' && event.code !== 'ArrowLeft') return undefined
  event.preventDefault()
  const { over, droppableRects, droppableContainers, collisionRect } = context
  const current = WORK_ITEM_STATUSES.indexOf(String(over?.id))
  if (current === -1 || !collisionRect) return undefined
  const next = WORK_ITEM_STATUSES[current + (event.code === 'ArrowRight' ? 1 : -1)]
  const rect = next === undefined ? undefined : droppableRects.get(next)
  if (next === undefined || !rect) return undefined
  // Stacked columns on a narrow screen can be off screen; the card stays at its height when it still fits the column.
  droppableContainers.get(next)?.node.current?.scrollIntoView({ block: 'nearest' })
  const lowest = Math.max(rect.top, rect.bottom - collisionRect.height)
  return { x: rect.left, y: Math.min(Math.max(collisionRect.top, rect.top), lowest) }
}
