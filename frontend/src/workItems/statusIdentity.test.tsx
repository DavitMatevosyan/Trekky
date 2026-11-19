import { render, screen, within } from '@testing-library/react'
import css from '../index.css?raw'
import { parseOklch } from '../lib/contrast.ts'
import { fakeApi, jsonResponse, me, renderApp, validTokens } from '../test/renderApp.tsx'
import { StatusChip } from './StatusChip.tsx'
import { StatusIcon } from './StatusIcon.tsx'
import { statusLabel, statusStyle, typeStyle, WORK_ITEM_STATUSES, WORK_ITEM_TYPES } from './workItemTypes.ts'

/** The chroma of every `--<prefix>-…-<shade>` token in index.css (both themes). */
function chromas(prefix: string, shade: string, skip: readonly string[] = []): number[] {
  return [...css.matchAll(new RegExp(`--${prefix}-([\\w-]+)-${shade}: (oklch\\([^)]+\\))`, 'g'))]
    .filter(([, name]) => !skip.includes(name ?? ''))
    .map(([, , value]) => parseOklch(value ?? '')?.c ?? Number.NaN)
}

describe('status identity', () => {
  it.each(WORK_ITEM_STATUSES)('%s has a label, its own tokens in both themes and its own dot', (status) => {
    const style = statusStyle(status)
    expect(style.token).not.toBeNull()
    expect(statusLabel(status)).not.toBe(status)
    for (const shade of ['tint', 'accent', 'text']) expect(css.split(`--status-${style.token}-${shade}: oklch(`)).toHaveLength(3)
    for (const name of [...style.chip.split(' '), style.icon]) expect(name).toMatch(new RegExp(`^(bg|text)-status-${style.token}-`))
    expect(render(<StatusIcon status={status} />).container.querySelector('svg')).toHaveAttribute('data-status-icon', status)
  })

  it('gives every status a different shape, so colour is never the only cue', () => {
    const shapes = WORK_ITEM_STATUSES.map((status) => render(<StatusIcon status={status} />).container.querySelector('svg')?.innerHTML)
    expect(new Set(shapes).size).toBe(WORK_ITEM_STATUSES.length)
  })

  it('keeps statuses quieter than the types: less chroma in every shade than any main type', () => {
    // SubTask is Task's deliberately quiet sibling (TREK-57); the main types set the bar.
    const skip = [typeStyle('subtask').token ?? 'subtask']
    expect(WORK_ITEM_TYPES).toContain('subtask')
    for (const shade of ['tint', 'accent', 'text']) {
      expect(Math.max(...chromas('status', shade)), shade).toBeLessThan(Math.min(...chromas('type', shade, skip)))
    }
  })

  it('shows an unknown status as sent, neutral, with a dashed ring', () => {
    const { container } = render(<StatusChip status="blocked" />)

    expect(screen.getByText('blocked')).toHaveClass('bg-muted', 'text-muted-foreground')
    expect(container.querySelector('svg')).toHaveAttribute('data-status-icon', 'unknown')
  })

  it('shows the dot and the label on the status tint', () => {
    render(<StatusChip status="in_review" />)

    const chip = screen.getByText('In Review')
    expect(chip).toHaveClass('bg-status-in-review-tint', 'text-status-in-review-text')
    const icon = chip.querySelector('svg')
    expect(icon).toHaveAttribute('aria-hidden', 'true')
    expect(icon).toHaveClass('text-status-in-review-accent')
  })
})

describe('status identity on the pages', () => {
  const project = { id: '0190a000-0000-7000-8000-0000000000b1', key: 'TRK', name: 'Trekky', itemCount: 2, createdAt: '2026-10-26T10:00:00Z' }
  const item = (n: number, status: string) => ({
    id: `0190a000-0000-7000-8000-${String(n).padStart(12, '0')}`,
    key: `TRK-${n}`,
    type: 'task',
    title: `Item ${n}`,
    status,
    parentKey: null,
    updatedAt: '2026-10-26T10:00:00Z',
    assignee: null,
  })
  const detail = (role = 'owner') =>
    fakeApi({
      'GET /api/v1/me': () => jsonResponse({ ...me, role }),
      'GET /api/v1/projects/by-key/TRK': () => jsonResponse(project),
      [`GET /api/v1/projects/${project.id}/work-items`]: () => jsonResponse({ items: [item(1, 'in_progress'), item(2, 'done')], nextCursor: null, totalCount: 2 }),
      [`GET /api/v1/projects/${project.id}/board`]: () =>
        jsonResponse({
          columns: ['todo', 'in_progress', 'in_review', 'done'].map((status) => ({ status, count: 0, items: [] })),
        }),
      'GET /api/v1/work-items/by-key/TRK-1': () =>
        jsonResponse({
          ...item(1, 'in_progress'),
          projectId: project.id,
          descriptionMd: null,
          parent: null,
          children: [item(2, 'done')],
          allowedTransitions: ['todo', 'in_review'],
          createdAt: '2026-10-26T10:00:00Z',
          createdBy: null,
        }),
      [`GET /api/v1/work-items/${item(1, 'x').id}/history`]: () => jsonResponse({ items: [], nextCursor: null }),
      [`GET /api/v1/work-items/${item(1, 'x').id}/comments`]: () => jsonResponse({ items: [], nextCursor: null }),
    })
  const iconOf = (element: Element | null | undefined) => element?.querySelector('svg[data-status-icon]')?.getAttribute('data-status-icon')

  it('list: the Status column shows a chip', async () => {
    renderApp({ path: '/TRK', tokens: validTokens, fetchFn: detail() })

    const rows = within(await screen.findByRole('table', { name: 'Work items' })).getAllByRole('row')
    const statusCell = within(rows[1] as HTMLElement).getAllByRole('cell')[3]
    expect(statusCell).toHaveTextContent('In Progress')
    expect(iconOf(statusCell)).toBe('in_progress')
  })

  it('board: each column heading has its status dot and still names the status', async () => {
    renderApp({ path: '/TRK/board', tokens: validTokens, fetchFn: detail() })

    const heading = await screen.findByRole('heading', { name: 'In Review (0)' })
    expect(iconOf(heading)).toBe('in_review')
  })

  it('item page: the dot inside the status select, chips for children, and a chip for a Viewer', async () => {
    const { unmount } = renderApp({ path: '/TRK/items/TRK-1', tokens: validTokens, fetchFn: detail() })

    const select = await screen.findByRole('combobox', { name: 'Status' })
    expect(iconOf(select.parentElement)).toBe('in_progress')
    const children = within(screen.getByRole('list', { name: 'Children' })).getAllByRole('listitem')
    expect(iconOf(children[0])).toBe('done')
    expect(children[0]).toHaveTextContent('Done')
    unmount()

    renderApp({ path: '/TRK/items/TRK-1', tokens: validTokens, fetchFn: detail('viewer') })
    await screen.findByText(me.user.email)
    const details = await screen.findByRole('region', { name: 'Details' })
    expect(within(details).queryByRole('combobox', { name: 'Status' })).not.toBeInTheDocument()
    expect(iconOf(within(details).getByText('In Progress'))).toBe('in_progress')
  })
})
