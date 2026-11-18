import { expect, test, type Locator, type Page } from '@playwright/test'

/** Drags `card` until its middle is a little past the top of `target`, so it takes `target`'s place. */
async function dragOnto(page: Page, card: Locator, target: Locator) {
  const from = await card.boundingBox()
  const to = await target.boundingBox()
  if (!from || !to) throw new Error('The cards are not on screen.')
  await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2)
  await page.mouse.down()
  // A first small move starts the drag; the rest carries the card onto the target.
  await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2 - 12, { steps: 3 })
  await page.mouse.move(to.x + to.width / 2, to.y + to.height / 3, { steps: 12 })
  await page.mouse.up()
}

test('the board is filtered by epic, a reorder there holds for everyone, and a completed epic leaves with its cards', async ({ page }) => {
  // A long journey (four items, three keyboard moves): more time when the machine is busy with the other specs.
  test.slow()
  const name = `e2e-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
  await page.goto('/sign-up')
  await page.getByLabel('Email').fill(`${name}@example.com`)
  await page.getByLabel('Password').fill('correct-horse-battery')
  await page.getByLabel('Organization').fill('E2E Org')
  await page.getByRole('button', { name: 'Create account' }).click()

  await page.getByRole('button', { name: 'New project' }).click()
  await page.getByLabel('Name').fill('Trekky')
  await page.getByRole('button', { name: 'Create project' }).click()
  await page.getByRole('list', { name: 'Projects' }).getByRole('link', { name: 'Trekky' }).click()
  await page.getByRole('navigation', { name: 'Project views' }).getByRole('link', { name: 'Board' }).click()
  await expect(page).toHaveURL(/\/TRK\/board$/)

  const items: [string, string, string?][] = [['epic', 'Release one'], ['task', 'Alpha', 'TRK-1'], ['task', 'Beta', 'TRK-1'], ['task', 'Gamma']]
  for (const [index, [type, title, parent]] of items.entries()) {
    await page.getByRole('button', { name: 'New item' }).click()
    await page.getByLabel('Type').selectOption(type)
    await page.getByLabel('Title').fill(title)
    if (parent) {
      await page.getByRole('combobox', { name: 'Parent' }).fill(parent)
      await page.getByRole('listbox', { name: 'Parent' }).getByRole('option', { name: new RegExp(`${parent} `) }).click()
    }
    await page.getByRole('button', { name: 'Create item' }).click()
    await expect(page.getByRole('heading', { level: 2 }).first()).toHaveText(`To Do (${index + 1})`)
  }

  const todo = page.getByRole('list', { name: 'To Do' })
  const keys = todo.getByRole('listitem').getByRole('link')
  const card = (key: string) => todo.getByRole('listitem').filter({ has: page.getByRole('link', { name: key, exact: true }) })
  const saved = () => expect(page.locator('[aria-busy="true"]')).toHaveCount(0)
  const announced = (text: string) => page.getByRole('status').filter({ hasText: text })
  // New items go on top.
  await expect(keys).toHaveText(['TRK-4', 'TRK-3', 'TRK-2', 'TRK-1'])

  // Epic TRK-1: only its two tasks, counted against the whole column.
  await page.getByRole('combobox', { name: 'Epic' }).fill('Release')
  await page.getByRole('listbox', { name: 'Epic' }).getByRole('option', { name: /TRK-1/ }).click()
  await expect(page).toHaveURL(/\/TRK\/board\?parent=TRK-1$/)
  await expect(keys).toHaveText(['TRK-3', 'TRK-2'])
  await expect(page.getByRole('heading', { name: 'To Do (2 of 4)' })).toBeVisible()

  // Reordered among the visible cards; the same after a reload, and for the unfiltered board.
  await dragOnto(page, card('TRK-2'), card('TRK-3'))
  await expect(keys).toHaveText(['TRK-2', 'TRK-3'])
  await saved()
  await page.reload()
  await expect(keys).toHaveText(['TRK-2', 'TRK-3'])
  await page.getByRole('button', { name: 'Clear all' }).click()
  await expect(keys).toHaveText(['TRK-4', 'TRK-2', 'TRK-3', 'TRK-1'])

  // Switching to the list and back keeps the filters.
  await page.getByRole('group', { name: 'Quick filters' }).getByRole('button', { name: 'Open' }).click()
  await expect(page.getByRole('heading', { level: 2 })).toHaveText(['To Do (4 of 4)', 'In Progress (0 of 0)', 'In Review (0 of 0)'])
  await page.getByRole('navigation', { name: 'Project views' }).getByRole('link', { name: 'List' }).click()
  await expect(page).toHaveURL(/\/TRK\?open=true$/)
  await page.getByRole('navigation', { name: 'Project views' }).getByRole('link', { name: 'Board' }).click()
  await expect(page).toHaveURL(/\/TRK\/board\?open=true$/)
  await page.getByRole('button', { name: 'Clear all' }).click()

  // The epic moves to Done by keyboard: it and its tasks leave the board, with a notice; Gamma stays.
  for (const [from, to] of [['To Do', 'In Progress'], ['In Progress', 'In Review'], ['In Review', 'Done']]) {
    await page.getByRole('list', { name: from }).getByRole('link', { name: 'TRK-1', exact: true }).focus()
    await page.keyboard.press('Space')
    await expect(announced(`TRK-1 is over ${from}.`)).toHaveCount(1)
    await page.keyboard.press('ArrowRight')
    await expect(announced(`TRK-1 is over ${to}.`)).toHaveCount(1)
    await page.keyboard.press('Space')
    await saved()
  }
  await expect(page.getByText('TRK-1 is a completed epic now: it and the items under it left the board.')).toBeVisible()
  await expect(keys).toHaveText(['TRK-4'])
  await expect(page.getByRole('list', { name: 'Done' })).toHaveCount(0)

  // "Show completed epics" brings them back.
  await page.getByRole('checkbox', { name: 'Show completed epics' }).click()
  await expect(keys).toHaveText(['TRK-4', 'TRK-2', 'TRK-3'])
  await expect(page.getByRole('list', { name: 'Done' }).getByRole('link')).toHaveText(['TRK-1'])
})
