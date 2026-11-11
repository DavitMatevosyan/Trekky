import type { KeyboardCoordinateGetter } from '@dnd-kit/core'
import { ProblemError } from '../api/problem.ts'
import { applyMove, columnKeyboardCoordinates, dropTarget, isBelow, moveErrorMessage } from './boardMoves.ts'

const card = { id: 'a', key: 'TRK-1', type: 'task', title: 'One', status: 'todo', parentKey: null, updatedAt: '2026-11-09T10:00:00Z', etag: '"1"' }
const other = { ...card, id: 'b', key: 'TRK-2', status: 'in_progress' }
const board = {
  columns: [
    { status: 'todo', count: 1, items: [card] },
    { status: 'in_progress', count: 1, items: [other] },
    { status: 'done', count: 25, items: [] },
  ],
}

const top = { afterId: null, beforeId: null }

describe('applyMove', () => {
  it('puts the card on top of the new column and changes both counts', () => {
    const moved = applyMove(board, { card, to: 'in_progress', ...top })

    expect(moved.columns[0]).toEqual({ status: 'todo', count: 0, items: [] })
    expect(moved.columns[1]?.count).toBe(2)
    expect(moved.columns[1]?.items.map((item) => [item.key, item.status])).toEqual([
      ['TRK-1', 'in_progress'],
      ['TRK-2', 'in_progress'],
    ])
  })

  it('puts the card below the card it was dropped after', () => {
    const moved = applyMove(board, { card, to: 'in_progress', afterId: 'b', beforeId: null })

    expect(moved.columns[1]?.items.map((item) => item.key)).toEqual(['TRK-2', 'TRK-1'])
  })

  it('reorders a column without changing its count', () => {
    const third = { ...card, id: 'c', key: 'TRK-3' }
    const column = { columns: [{ status: 'todo', count: 3, items: [card, other, third].map((item) => ({ ...item, status: 'todo' })) }] }

    const moved = applyMove(column, { card, to: 'todo', afterId: 'b', beforeId: 'c' })

    expect(moved.columns[0]?.count).toBe(3)
    expect(moved.columns[0]?.items.map((item) => item.key)).toEqual(['TRK-2', 'TRK-1', 'TRK-3'])
  })

  it('counts every card of a column, not only the ones shown', () => {
    expect(applyMove(board, { card, to: 'done', ...top }).columns[2]?.count).toBe(26)
  })

  it('does not count the card twice when the re-read already has it in the new column', () => {
    const reread = { columns: [{ status: 'todo', count: 0, items: [] }, { status: 'in_progress', count: 2, items: [{ ...card, status: 'in_progress' }, other] }] }

    expect(applyMove(reread, { card, to: 'in_progress', ...top })).toEqual(reread)
  })

  it('leaves other columns as they are', () => {
    expect(applyMove(board, { card, to: 'done', ...top }).columns[1]).toBe(board.columns[1])
  })
})

describe('dropTarget', () => {
  const todo = (n: number) => ({ ...card, id: `t${n}`, key: `TRK-${n}` })
  const doing = (n: number) => ({ ...card, id: `p${n}`, key: `TRK-${n}`, status: 'in_progress' })
  const finished = { ...card, id: 'd1', key: 'TRK-9', status: 'done' }
  const full = {
    columns: [
      { status: 'todo', count: 3, items: [todo(1), todo(2), todo(3)] },
      { status: 'in_progress', count: 2, items: [doing(4), doing(5)] },
      { status: 'in_review', count: 0, items: [] },
      { status: 'done', count: 1, items: [finished] },
    ],
  }
  const at = (target: ReturnType<typeof dropTarget>) =>
    target && [target.to, target.afterId, target.beforeId, target.position, target.total]

  it('takes the place of the card it is over in its own column, down or up', () => {
    expect(at(dropTarget(full, todo(1), 't2'))).toEqual(['todo', 't2', 't3', 2, 3])
    expect(at(dropTarget(full, todo(1), 't3'))).toEqual(['todo', 't3', null, 3, 3])
    expect(at(dropTarget(full, todo(3), 't1'))).toEqual(['todo', null, 't1', 1, 3])
  })

  it('does not move a card dropped on itself or on its own column', () => {
    expect(dropTarget(full, todo(2), 't2')).toBeNull()
    expect(dropTarget(full, todo(2), 'todo')).toBeNull()
  })

  it('goes above or below a card of another column, and to the bottom of its free space', () => {
    expect(at(dropTarget(full, todo(1), 'p5'))).toEqual(['in_progress', 'p4', 'p5', 2, 3])
    expect(at(dropTarget(full, todo(1), 'p5', true))).toEqual(['in_progress', 'p5', null, 3, 3])
    expect(at(dropTarget(full, todo(1), 'in_progress'))).toEqual(['in_progress', 'p5', null, 3, 3])
    expect(at(dropTarget(full, todo(1), 'in_review'))).toEqual(['in_review', null, null, 1, 1])
  })

  it('takes no place in Done, and a Done card stays where it is', () => {
    expect(at(dropTarget(full, doing(4), 'done'))).toEqual(['done', null, null, 1, 2])
    expect(at(dropTarget(full, doing(4), 'd1'))).toEqual(['done', null, null, 1, 2])
    expect(dropTarget(full, finished, 'done')).toBeNull()
  })

  it('ignores something that is not on the board', () => {
    expect(dropTarget(full, todo(1), 'nowhere')).toBeNull()
  })
})

describe('isBelow', () => {
  const rect = (top: number, height = 40) => ({ top, height, left: 0, width: 100, right: 100, bottom: top + height })

  it('compares the middles of the two cards', () => {
    expect(isBelow(rect(130), rect(100))).toBe(true)
    expect(isBelow(rect(90), rect(100))).toBe(false)
    expect(isBelow(null, rect(100))).toBe(false)
  })
})

describe('moveErrorMessage', () => {
  const move = { card, to: 'done', ...top }

  it('says when the cards around the drop moved meanwhile', () => {
    expect(moveErrorMessage(new ProblemError({ status: 409 }), move)).toBe(
      'The cards around TRK-1 were moved by someone else. The board is up to date now; move it again.',
    )
  })

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
    const context = { over: over === null ? null : { id: over, data: { current: undefined } }, droppableRects, droppableContainers, collisionRect: { top, height: 80 } }
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

  it('uses the column of the card it is over', () => {
    const event = new KeyboardEvent('keydown', { code: 'ArrowRight' })
    const context = { over: { id: 'some-card', data: { current: { status: 'todo' } } }, droppableRects, droppableContainers, collisionRect: { top: 100, height: 80 } }
    expect(columnKeyboardCoordinates(event, { context } as unknown as Parameters<KeyboardCoordinateGetter>[1])).toEqual({ x: 300, y: 100 })
  })

  it('ignores other keys and a card over no column', () => {
    expect(press('ArrowDown', 'todo')).toBeUndefined()
    expect(press('ArrowRight', null)).toBeUndefined()
  })
})
