import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { describe, it } from 'node:test'
import { existingMatchKey, itemDescription, itemTitle, matchKey, notionKeyOf, stepsTo, validate, type Snapshot } from './plan.ts'

const snapshot = JSON.parse(readFileSync(new URL('./tasks.json', import.meta.url), 'utf8')) as Snapshot

describe('the committed snapshot', () => {
  it('is valid: keys, titles and statuses Trekky accepts, each title once', () => {
    assert.deepEqual(validate(snapshot), [])
  })

  // TRK-29 is the last key the Notion list had; raise it when the snapshot is refreshed with newer TRKs.
  it('has every Notion key from TRK-0 to TRK-29 once', () => {
    const keys = snapshot.epics.flatMap((epic) => epic.items.map((item) => item.notionKey)).filter((key) => key !== undefined)
    assert.equal(new Set(keys).size, keys.length)
    for (let n = 0; n <= 29; n++) assert.ok(keys.includes(`TRK-${n}`), `TRK-${n} is missing`)
  })
})

describe('itemTitle', () => {
  it('puts the Notion key first, so the old key still finds the item', () => {
    assert.equal(itemTitle({ notionKey: 'TRK-6.1', title: 'Remove MinIO', description: '', status: 'done' }), 'TRK-6.1 · Remove MinIO')
    assert.equal(itemTitle({ title: 'A bug', description: '', status: 'todo' }), 'A bug')
  })
})

describe('matching existing items', () => {
  it('reads the Notion key back from a title, also after the rest of it was renamed', () => {
    assert.equal(notionKeyOf('TRK-6.1 · Remove MinIO'), 'TRK-6.1')
    assert.equal(notionKeyOf('TRK-6.1 · Renamed in Trekky'), 'TRK-6.1')
    assert.equal(notionKeyOf('Groundwork'), null)
    assert.equal(notionKeyOf('TRK-6 without the separator'), null)
  })

  it('matches items with a Notion key by key, and the rest by title', () => {
    assert.equal(matchKey({ notionKey: 'TRK-7', title: 'Login API', description: '', status: 'done' }), 'TRK-7')
    assert.equal(existingMatchKey('TRK-7 · Sign-up and login API, renamed'), 'TRK-7')
    assert.equal(matchKey({ title: 'A bug', description: '', status: 'todo' }), 'A bug')
    assert.equal(existingMatchKey('A bug'), 'A bug')
  })
})

describe('itemDescription', () => {
  it('says where the item came from', () => {
    assert.equal(itemDescription({ notionKey: 'TRK-7', description: 'Login API.' }, 'Notion, 2026-11-12'), 'Login API.\n\n_Imported from Notion (TRK-7): Notion, 2026-11-12._')
  })
})

describe('stepsTo', () => {
  it('walks the workflow one step at a time', () => {
    assert.deepEqual(stepsTo('todo', 'done'), ['in_progress', 'in_review', 'done'])
    assert.deepEqual(stepsTo('in_progress', 'in_review'), ['in_review'])
  })

  it('never moves work back, and does nothing when it is already there', () => {
    assert.deepEqual(stepsTo('done', 'in_progress'), [])
    assert.deepEqual(stepsTo('in_review', 'in_review'), [])
    assert.deepEqual(stepsTo('blocked', 'done'), [])
  })
})

describe('validate', () => {
  const base = (): Snapshot => ({
    source: 'test',
    project: { key: 'TREK', name: 'Trekky' },
    epics: [{ title: 'Epic', description: '', items: [{ notionKey: 'TRK-1', title: 'One', description: '', status: 'todo' }] }],
  })

  it('refuses the TRK project key, whose numbers stay Notion\'s (ADR-0016)', () => {
    const trk = base()
    trk.project.key = 'TRK'
    assert.deepEqual(validate(trk), ['Project key "TRK" would give Notion\'s keys a second meaning (ADR-0016); use TREK.'])
  })

  it('refuses a repeated Notion key even under another title', () => {
    const twice = base()
    twice.epics[0]?.items.push({ notionKey: 'TRK-1', title: 'Renamed', description: '', status: 'todo' })
    assert.deepEqual(validate(twice), ['Notion key TRK-1 appears twice.'])
  })

  it('refuses a bad project key, a duplicate title, a long title and an unknown status', () => {
    const bad = base()
    bad.project.key = 'trk'
    bad.epics[0]?.items.push({ notionKey: 'TRK-1', title: 'One', description: '', status: 'todo' })
    bad.epics[0]?.items.push({ title: 'x'.repeat(201), description: '', status: 'todo' })
    bad.epics[0]?.items.push({ title: 'Odd', description: '', status: 'blocked' as never })

    assert.deepEqual(validate(bad), [
      'Project key "trk" is not valid.',
      '"TRK-1 · One" appears twice.',
      'Notion key TRK-1 appears twice.',
      `"${'x'.repeat(40)}…" is longer than 200 characters.`,
      '"Odd" has an unknown status "blocked".',
    ])
  })
})
