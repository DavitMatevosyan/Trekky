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

test('cards are reordered within a column by mouse and keyboard, and the order is kept', async ({ page }) => {
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

  for (const title of ['First', 'Second', 'Third']) {
    await page.getByRole('button', { name: 'New item' }).click()
    await page.getByLabel('Type').selectOption('task')
    await page.getByLabel('Title').fill(title)
    await page.getByRole('button', { name: 'Create item' }).click()
    await expect(page.getByRole('heading', { level: 2 }).first()).toContainText(`(${['First', 'Second', 'Third'].indexOf(title) + 1})`)
  }

  const todo = page.getByRole('list', { name: 'To Do' })
  const keys = todo.getByRole('listitem').getByRole('link')
  const card = (key: string) => todo.getByRole('listitem').filter({ has: page.getByRole('link', { name: key }) })
  const saved = () => expect(page.locator('[aria-busy="true"]')).toHaveCount(0)
  const announced = (text: string) => page.getByRole('status').filter({ hasText: text })

  // New items go on top.
  await expect(keys).toHaveText(['TRK-3', 'TRK-2', 'TRK-1'])

  // Mouse: the bottom card goes to the top, and stays there after a reload.
  await dragOnto(page, card('TRK-1'), card('TRK-3'))
  await expect(keys).toHaveText(['TRK-1', 'TRK-3', 'TRK-2'])
  await saved()
  await page.reload()
  await expect(keys).toHaveText(['TRK-1', 'TRK-3', 'TRK-2'])

  // Keyboard: Space picks up, the down arrow moves it one place, Space drops; the focus stays on the card.
  await card('TRK-1').getByRole('link', { name: 'TRK-1' }).focus()
  await page.keyboard.press('Space')
  await expect(announced('TRK-1 is over To Do.')).toHaveCount(1)
  await page.keyboard.press('ArrowDown')
  await expect(announced('TRK-1 is over To Do, place 2 of 3.')).toHaveCount(1)
  await page.keyboard.press('Space')
  await expect(announced('Dropped TRK-1 on To Do, place 2 of 3.')).toHaveCount(1)
  await expect(keys).toHaveText(['TRK-3', 'TRK-1', 'TRK-2'])
  await saved()
  await expect(page.getByRole('alert')).toHaveCount(0)
  await expect(card('TRK-1').getByRole('link', { name: 'TRK-1' })).toBeFocused()

  // A reorder is not a change of the item: its history only has the creation.
  await page.reload()
  await expect(keys).toHaveText(['TRK-3', 'TRK-1', 'TRK-2'])
  await card('TRK-1').getByRole('link', { name: 'TRK-1' }).click()
  await expect(page).toHaveURL(/\/TRK\/items\/TRK-1$/)
  await expect(page.getByRole('list', { name: 'History' }).getByRole('listitem')).toHaveCount(1)
})
