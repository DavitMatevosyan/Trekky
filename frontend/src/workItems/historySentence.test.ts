import { actorName, historySentence } from './historySentence.ts'

const sentence = (eventType: string, payload: unknown, schemaVersion: number | string = 1) => historySentence({ eventType, schemaVersion, payload })

describe('historySentence', () => {
  it('names the type of a created item', () => {
    expect(sentence('WorkItemCreated', { type: 'tech_debt', title: 'Old code', status: 'todo' })).toBe('created this TechDebt')
  })

  it('shows a status change with the workflow labels', () => {
    expect(sentence('WorkItemStatusChanged', { from: 'in_progress', to: 'in_review' })).toBe('changed status In Progress → In Review')
  })

  it('lists every field an edit changed in one sentence', () => {
    const payload = {
      changes: {
        title: { from: 'Comments', to: 'Comments on items' },
        type: { from: 'task', to: 'bug' },
        parent: { from: null, to: { id: '0190a000-0000-7000-8000-000000000002', key: 'TRK-2' } },
        descriptionMd: { changed: true },
      },
    }

    expect(sentence('WorkItemUpdated', payload)).toBe('changed title “Comments” → “Comments on items”, type Task → Bug, parent — → TRK-2, description')
  })

  it('quotes the start of a comment', () => {
    expect(historySentence({ eventType: 'CommentAdded', schemaVersion: 1, payload: { commentId: 'c1', excerpt: 'Ship it.' } })).toBe(
      'commented “Ship it.”',
    )
    expect(historySentence({ eventType: 'CommentAdded', schemaVersion: 1, payload: { commentId: 'c1' } })).toBe('made a change')
  })

  it('shows a removed parent', () => {
    const payload = { changes: { parent: { from: { id: '0190a000-0000-7000-8000-000000000002', key: 'TRK-2' }, to: null } } }

    expect(sentence('WorkItemUpdated', payload)).toBe('changed parent TRK-2 → —')
  })

  it('reads a schema version sent as a string', () => {
    expect(sentence('WorkItemStatusChanged', { from: 'todo', to: 'done' }, '1')).toBe('changed status To Do → Done')
  })

  it.each([
    ['an event type it does not know', 'WorkItemArchived', {}, 1],
    ['a schema version it does not know', 'WorkItemStatusChanged', { from: 'todo', to: 'done' }, 2],
    ['a payload that is not an object', 'WorkItemCreated', null, 1],
    ['a status change without its values', 'WorkItemStatusChanged', { to: 'done' }, 1],
    ['an edit with no field it can show', 'WorkItemUpdated', { changes: { rank: { from: 1, to: 2 } } }, 1],
  ])('still shows a change for %s', (_, eventType, payload, schemaVersion) => {
    expect(sentence(eventType, payload, schemaVersion)).toBe('made a change')
  })
})

describe('actorName', () => {
  it('uses the current name', () => {
    expect(actorName({ type: 'user', id: '0190a000-0000-7000-8000-0000000000aa', displayName: 'Davit' })).toBe('Davit')
  })

  it('says a former member when a user has left the organization', () => {
    expect(actorName({ type: 'user', id: '0190a000-0000-7000-8000-0000000000aa', displayName: null })).toBe('A former member')
  })

  it('credits the app for a change no person made', () => {
    expect(actorName({ type: 'system', id: null, displayName: null })).toBe('Trekky')
  })
})
