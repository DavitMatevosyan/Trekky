import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { fakeApi, jsonResponse, renderApp, validTokens } from '../test/renderApp.tsx'
import { typeOverlay, WORK_ITEM_TYPES } from '../workItems/workItemTypes.ts'

// TREK-58: the type identity (TREK-57) on the list, the board, the item page and the form.

const project = { id: '0190a000-0000-7000-8000-0000000000b1', key: 'TRK', name: 'Trekky', itemCount: 3, createdAt: '2026-10-26T10:00:00Z' }
const summary = (n: number, type: string, title: string, status = 'todo', parentKey: string | null = null) => ({
  id: `0190a000-0000-7000-8000-${String(n).padStart(12, '0')}`,
  key: `TRK-${n}`,
  type,
  title,
  status,
  parentKey,
  updatedAt: '2026-10-26T10:00:00Z',
  assignee: null,
})
const epic = summary(1, 'epic', 'v1.3 Themes')
const subtask = summary(2, 'subtask', 'Pick the hues', 'todo', 'TRK-3')
const task = summary(3, 'task', 'Type identity', 'todo', 'TRK-1')
const iconOf = (element: Element | null) => element?.querySelector('svg[data-type-icon]')?.getAttribute('data-type-icon')

function render(path: string) {
  const fetchFn = fakeApi({
    'GET /api/v1/projects/by-key/TRK': () => jsonResponse(project),
    [`GET /api/v1/projects/${project.id}/work-items`]: (request) =>
      new URL(request.url).searchParams.getAll('type').includes('epic')
        ? jsonResponse({ items: [epic], nextCursor: null, totalCount: 1 })
        : jsonResponse({ items: [task, subtask, epic], nextCursor: null, totalCount: 3 }),
    [`GET /api/v1/projects/${project.id}/board`]: () =>
      jsonResponse({
        columns: [
          { status: 'todo', count: 3, items: [task, subtask, epic].map((item) => ({ ...item, etag: '"1"' })) },
          { status: 'in_progress', count: 0, items: [] },
          { status: 'in_review', count: 0, items: [] },
          { status: 'done', count: 0, items: [] },
        ],
      }),
    'GET /api/v1/work-items/by-key/TRK-3': () =>
      jsonResponse({
        ...task,
        projectId: project.id,
        descriptionMd: null,
        parent: epic,
        children: [subtask],
        allowedTransitions: ['in_progress'],
        createdAt: task.updatedAt,
        createdBy: null,
      }),
    [`GET /api/v1/work-items/${task.id}/history`]: () => jsonResponse({ items: [], nextCursor: null }),
    [`GET /api/v1/work-items/${task.id}/comments`]: () => jsonResponse({ items: [], nextCursor: null }),
  })
  return renderApp({ path, tokens: validTokens, fetchFn })
}

describe('type identity on the pages', () => {
  it('list: a type badge per row, the type stripe on the first cell, and epics on a faint tint', async () => {
    render('/TRK')

    const table = await screen.findByRole('table', { name: 'Work items' })
    const [, taskRow, subtaskRow, epicRow] = within(table).getAllByRole('row')
    const typeCell = (row: HTMLElement | undefined) => within(row as HTMLElement).getAllByRole('cell')[1] ?? null
    expect(typeCell(taskRow)).toHaveTextContent('Task')
    expect(iconOf(typeCell(taskRow))).toBe('task')
    expect(iconOf(typeCell(epicRow))).toBe('epic')
    expect(within(taskRow as HTMLElement).getAllByRole('cell')[0]).toHaveClass('border-l-[3px]', 'border-l-type-task-accent')
    expect(within(subtaskRow as HTMLElement).getAllByRole('cell')[0]).toHaveClass('border-l-type-subtask-accent')
    expect(epicRow).toHaveClass('bg-type-epic-tint/50', 'hover:bg-type-epic-tint')
    expect(taskRow).toHaveClass('hover:bg-muted')
    expect(taskRow).not.toHaveClass('bg-type-epic-tint/50')
  })

  it('board: a compact badge next to the key, the stripe, an Epic tinted and a SubTask flat', async () => {
    render('/TRK/board')

    const cards = within(await screen.findByRole('list', { name: 'To Do' })).getAllByRole('listitem')
    const [taskCard, subtaskCard, epicCard] = cards
    // The type comes before the key, for sighted users and screen readers alike.
    expect(taskCard).toHaveTextContent(/^TaskTRK-3/)
    expect(within(taskCard as HTMLElement).getByTitle('Task')).toBeInTheDocument()
    expect(iconOf(subtaskCard ?? null)).toBe('subtask')
    expect(taskCard).toHaveClass('bg-surface-raised', 'border-l-type-task-accent')
    expect(subtaskCard).toHaveClass('bg-surface', 'border-l-type-subtask-accent')
    expect(epicCard).toHaveClass('bg-type-epic-tint/70', 'border-l-type-epic-accent')
    // The stripe colour survives the card's all-sides border colour, and an Epic shows a ring on hover.
    expect(epicCard).toHaveClass('border-border', 'border-l-type-epic-accent', 'hover:ring-type-epic-accent')
    // Keyboard users hear the type with the key, from the card's one tab stop.
    const link = within(taskCard as HTMLElement).getByRole('link', { name: 'TRK-3' })
    expect(link).toHaveAccessibleDescription(/^Task Press Enter to open this card/)
  })

  it('item page: the badge in the header, compact badges for children and the parent, which still reads "TRK-1 Epic"', async () => {
    render('/TRK/items/TRK-3')

    const heading = await screen.findByRole('heading', { level: 1 })
    expect(heading).toHaveTextContent('TaskType identity')
    expect(iconOf(heading)).toBe('task')
    const parent = screen.getByRole('link', { name: 'TRK-1 Epic' })
    expect(iconOf(parent)).toBe('epic')
    const children = within(screen.getByRole('list', { name: 'Children' })).getAllByRole('listitem')
    expect(children[0]).toHaveTextContent('TRK-2SubTaskPick the hues')
    expect(iconOf(children[0] ?? null)).toBe('subtask')
  })

  it('form: the chosen type icon in the type select, and the type icon in each parent option', async () => {
    render('/TRK')
    await userEvent.click(await screen.findByRole('button', { name: 'New item' }))
    const form = screen.getByRole('form', { name: 'New item' })
    const select = within(form).getByLabelText('Type')
    const field = select.parentElement

    expect(iconOf(field)).toBe('task')
    await userEvent.selectOptions(select, 'bug')
    expect(iconOf(field)).toBe('bug')

    await userEvent.click(within(form).getByRole('combobox', { name: 'Parent' }))
    const option = await within(form).findByRole('option', { name: /TRK-1/ })
    await waitFor(() => expect(iconOf(option)).toBe('epic'))
    expect(option).toHaveTextContent('TRK-1Epicv1.3 Themes')
  })

  it('keeps the dragged card opaque and without hover, for every type', () => {
    // It floats above other cards: nothing may show through, and it looks the same by mouse, touch or keyboard.
    for (const type of [...WORK_ITEM_TYPES, 'initiative']) {
      const classes = typeOverlay(type)
      expect(classes, type).toMatch(/^bg-[\w-]+$/)
      expect(classes, type).not.toContain('/')
      expect(classes, type).not.toContain('hover:')
    }
  })
})
