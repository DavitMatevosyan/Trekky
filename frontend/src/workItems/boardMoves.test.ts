import type { KeyboardCoordinateGetter } from '@dnd-kit/core'
import { ProblemError } from '../api/problem.ts'
import { applyMove, columnKeyboardCoordinates, moveErrorMessage } from './boardMoves.ts'

const card = { id: 'a', key: 'TRK-1', type: 'task', title: 'One', status: 'todo', parentKey: null, updatedAt: '2026-11-09T10:00:00Z', etag: '"1"' }
const other = { ...card, id: 'b', key: 'TRK-2', status: 'in_progress' }
const board = {
  columns: [
    { status: 'todo', count: 1, items: [card] },
    { status: 'in_progress', count: 1, items: [other] },
    { status: 'done', count: 25, items: [] },
  ],
}

describe('applyMove', () => {
  it('puts the card on top of the new column and changes both counts', () => {
    const moved = applyMove(board, { card, to: 'in_progress' })

    expect(moved.columns[0]).toEqual({ status: 'todo', count: 0, items: [] })
    expect(moved.columns[1]?.count).toBe(2)
    expect(moved.columns[1]?.items.map((item) => [item.key, item.status])).toEqual([
      ['TRK-1', 'in_progress'],
      ['TRK-2', 'in_progress'],
    ])
  })

  it('counts every card of a column, not only the ones shown', () => {
    expect(applyMove(board, { card, to: 'done' }).columns[2]?.count).toBe(26)
  })

  it('does not count the card twice when the re-read already has it in the new column', () => {
    const reread = { columns: [{ status: 'todo', count: 0, items: [] }, { status: 'in_progress', count: 2, items: [{ ...card, status: 'in_progress' }, other] }] }

    expect(applyMove(reread, { card, to: 'in_progress' })).toBe(reread)
  })

  it('leaves other columns as they are', () => {
    expect(applyMove(board, { card, to: 'done' }).columns[1]).toBe(board.columns[1])
  })
})

describe('moveErrorMessage', () => {
  const move = { card, to: 'done' }

  it('names where the card can go when the workflow refuses', () => {
    const error = new ProblemError({ status: 422, allowedTransitions: ['in_progress'] })
    expect(moveErrorMessage(error, move)).toBe('TRK-1 can only move to In Progress.')
  })

  it('joins several allowed columns', () => {
    const error = new ProblemError({ status: 422, allowedTransitions: ['todo', 'in_review'] })
    expect(moveErrorMessage(error, move)).toBe('TRK-1 can only move to To Do or In Review.')
  })

  it('says when someone else changed the card', () => {
    expect(moveErrorMessage(new ProblemError({ status: 412 }), move)).toBe(
      'TRK-1 was changed by someone else. Its latest state is shown; move it again.',
    )
  })

  it('falls back to the general message for anything else', () => {
    expect(moveErrorMessage(new Error('network'), move)).toBe('Something went wrong. Please try again.')
    expect(moveErrorMessage(new ProblemError({ status: 500 }), move)).toBe('Something went wrong. Please try again.')
  })
})

describe('columnKeyboardCoordinates', () => {
  const rect = (left: number) => ({ left, top: 100, bottom: 900 })
  const droppableRects = new Map([
    ['todo', rect(0)],
    ['in_progress', rect(300)],
    ['in_review', rect(600)],
    ['done', rect(900)],
  ])
  const scrollIntoView = vi.fn()
  const droppableContainers = new Map(['todo', 'in_progress', 'in_review', 'done'].map((id) => [id, { node: { current: { scrollIntoView } } }]))
  const press = (code: string, over: string | null, top = 100) => {
    const event = new KeyboardEvent('keydown', { code })
    const context = { over: over === null ? null : { id: over }, droppableRects, droppableContainers, collisionRect: { top, height: 80 } }
    return columnKeyboardCoordinates(event, { context } as unknown as Parameters<KeyboardCoordinateGetter>[1])
  }

  it('moves to the next and previous column and brings it into view', () => {
    scrollIntoView.mockClear()

    expect(press('ArrowRight', 'in_progress')).toEqual({ x: 600, y: 100 })
    expect(press('ArrowLeft', 'in_progress')).toEqual({ x: 0, y: 100 })
    expect(scrollIntoView).toHaveBeenCalledTimes(2)
  })

  it('keeps the card at its height, inside the column', () => {
    expect(press('ArrowRight', 'todo', 500)).toEqual({ x: 300, y: 500 })
    expect(press('ArrowRight', 'todo', 40)).toEqual({ x: 300, y: 100 })
    expect(press('ArrowRight', 'todo', 880)).toEqual({ x: 300, y: 820 })
  })

  it('stays put at either end of the workflow', () => {
    expect(press('ArrowLeft', 'todo')).toBeUndefined()
    expect(press('ArrowRight', 'done')).toBeUndefined()
  })

  it('ignores other keys and a card over no column', () => {
    expect(press('ArrowDown', 'todo')).toBeUndefined()
    expect(press('ArrowRight', null)).toBeUndefined()
  })
})
