import { DEFAULT_FILTERS, filterQuery, isDefaultFilters, readFilters, toggle, writeFilters } from './workItemFilters.ts'

const userId = '0190a000-0000-7000-8000-0000000000aa'

describe('workItemFilters', () => {
  it('reads every filter from the URL', () => {
    const params = new URLSearchParams(
      `open=true&status=todo&status=in_review&type=bug&assignee=me&assignee=${userId}&parent=trk-1&q=login&completedEpics=show`,
    )

    expect(readFilters(params)).toEqual({
      open: true,
      statuses: ['todo', 'in_review'],
      types: ['bug'],
      assignees: ['me', userId],
      parent: 'TRK-1',
      q: 'login',
      showCompletedEpics: true,
    })
  })

  it('drops values the API would refuse, so a mistyped link still shows a list', () => {
    const params = new URLSearchParams('open=yes&status=doing&status=done&status=done&type=feature&assignee=someone&parent=not a key&completedEpics=maybe')

    expect(readFilters(params)).toEqual({ ...DEFAULT_FILTERS, statuses: ['done'] })
    // Keys as the API reads them, and its limits on the search and the assignees.
    expect(readFilters(new URLSearchParams('parent=T-1')).parent).toBeNull()
    expect(readFilters(new URLSearchParams('parent=TRK-0')).parent).toBeNull()
    expect(readFilters(new URLSearchParams(`q=${'a'.repeat(250)}`)).q).toHaveLength(200)
    const many = Array.from({ length: 60 }, (_, i) => `assignee=0190a000-0000-7000-8000-${String(i).padStart(12, '0')}`).join('&')
    expect(readFilters(new URLSearchParams(many)).assignees).toHaveLength(50)
  })

  it('writes only what differs from the default and keeps other parameters', () => {
    const written = writeFilters(new URLSearchParams('done=all&status=todo&q=old'), {
      ...DEFAULT_FILTERS,
      open: true,
      assignees: ['none'],
      q: '  ',
    })

    expect(written.toString()).toBe('done=all&open=true&assignee=none')
    expect(writeFilters(new URLSearchParams(), DEFAULT_FILTERS).toString()).toBe('')
  })

  it('round-trips through the URL', () => {
    const filters = { open: false, statuses: ['done'], types: ['epic', 'story'], assignees: ['none'], parent: 'TRK-7', q: 'x', showCompletedEpics: true }

    expect(readFilters(writeFilters(new URLSearchParams(), filters))).toEqual(filters)
  })

  it('builds the API query with the search trimmed and nothing for defaults', () => {
    expect(filterQuery(DEFAULT_FILTERS)).toEqual({})
    expect(filterQuery({ ...DEFAULT_FILTERS, open: true, types: ['task'], parent: 'TRK-1', q: ' log ', showCompletedEpics: true })).toEqual({
      open: 'true',
      type: ['task'],
      parent: 'TRK-1',
      q: 'log',
      completedEpics: 'show',
    })
  })

  it('treats only the default view as unfiltered', () => {
    expect(isDefaultFilters(DEFAULT_FILTERS)).toBe(true)
    expect(isDefaultFilters({ ...DEFAULT_FILTERS, q: '   ' })).toBe(true)
    expect(isDefaultFilters({ ...DEFAULT_FILTERS, showCompletedEpics: true })).toBe(false)
  })

  it('toggles a value in and out', () => {
    expect(toggle(['a'], 'b')).toEqual(['a', 'b'])
    expect(toggle(['a', 'b'], 'a')).toEqual(['b'])
  })
})
