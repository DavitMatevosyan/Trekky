import { PROJECT_KEY_PATTERN, suggestProjectKey } from './suggestProjectKey.ts'

describe('suggestProjectKey', () => {
  it.each([
    ['Trekky', 'TRK'],
    ['Website', 'WBS'],
    ['Website Redesign', 'WR'],
    ['trekky docs', 'TD'],
    ['Mobile App v2', 'MAV'],
    ['Ab', 'AB'],
    ['Aaa', 'AAA'],
    ['Éclair', 'ECL'],
    ['2026 Roadmap', 'RDM'],
    ['A B C D E F G H I J K L', 'ABCDEFGHIJ'],
  ])('suggests a key for %j → %s', (name, key) => {
    expect(suggestProjectKey(name)).toBe(key)
  })

  it.each(['', '   ', '1234', '!!!', 'A'])('returns no suggestion when it cannot make a valid key from %j', (name) => {
    expect(suggestProjectKey(name)).toBe('')
  })

  it('only ever suggests keys the API accepts', () => {
    for (const name of ['Trekky', 'Website Redesign', 'x', 'Q1 2026 plan', 'Über Team', 'a-b', 'Yyy']) {
      const key = suggestProjectKey(name)
      if (key) expect(key).toMatch(PROJECT_KEY_PATTERN)
    }
  })
})
