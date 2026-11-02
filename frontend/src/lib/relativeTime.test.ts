import { relativeTime } from './relativeTime.ts'

describe('relativeTime', () => {
  const now = new Date('2026-11-02T12:00:00Z')

  it.each([
    ['2026-11-02T11:59:50Z', 'now'],
    ['2026-11-02T11:55:00Z', '5 minutes ago'],
    ['2026-11-02T10:00:00Z', '2 hours ago'],
    ['2026-11-01T12:00:00Z', 'yesterday'],
    ['2026-10-20T12:00:00Z', '13 days ago'],
    ['2026-08-01T12:00:00Z', '3 months ago'],
    ['2024-11-01T12:00:00Z', '2 years ago'],
  ])('%s → %s', (iso, expected) => {
    expect(relativeTime(iso, now)).toBe(expected)
  })
})
