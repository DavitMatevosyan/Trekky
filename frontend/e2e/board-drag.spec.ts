import { expect, test, type Locator, type Page } from '@playwright/test'

async function dragCard(page: Page, card: Locator, column: Locator) {
  const from = await card.boundingBox()
  const to = await column.boundingBox()
  if (!from || !to) throw new Error('The card or column is not on screen.')
  await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2)
  await page.mouse.down()
  // A first small move starts the drag; the rest carries the card over the column.
  await page.mouse.move(from.x + from.width / 2 + 12, from.y + from.height / 2 + 12, { steps: 3 })
  await page.mouse.move(to.x + to.width / 2, to.y + 60, { steps: 12 })
  await page.mouse.up()
}

test('cards move between columns by mouse and keyboard, and a disallowed move goes back with the reason', async ({ page }) => {
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
  await expect(page).toHaveURL(/\/TRK$/)
  await page.getByRole('navigation', { name: 'Project views' }).getByRole('link', { name: 'Board' }).click()
  await expect(page).toHaveURL(/\/TRK\/board$/)

  await page.getByRole('button', { name: 'New item' }).click()
  await page.getByLabel('Type').selectOption('task')
  await page.getByLabel('Title').fill('Drag me')
  await page.getByRole('button', { name: 'Create item' }).click()

  const headings = page.getByRole('heading', { level: 2 })
  await expect(headings).toHaveText(['To Do (1)', 'In Progress (0)', 'In Review (0)', 'Done (0)'])
  const card = (status: string) => page.getByRole('list', { name: status }).getByRole('listitem')
  const column = (status: string) => page.getByRole('region', { name: new RegExp(`^${status} \\(`) })

  // A card shows in its new column while saving; the board is busy until the save and the re-read are done.
  const saved = () => expect(page.locator('[aria-busy="true"]')).toHaveCount(0)
  // What a screen reader hears; a key press only counts once the previous one has taken effect.
  const announced = (text: string) => page.getByRole('status').filter({ hasText: text })

  // Mouse: an allowed move saves, and is still there after a reload.
  await dragCard(page, card('To Do'), column('In Progress'))
  await expect(headings).toHaveText(['To Do (0)', 'In Progress (1)', 'In Review (0)', 'Done (0)'])
  await saved()
  await page.reload()
  await expect(headings).toHaveText(['To Do (0)', 'In Progress (1)', 'In Review (0)', 'Done (0)'])

  // A disallowed move goes back and names where the card can go.
  await dragCard(page, card('In Progress'), column('Done'))
  await expect(page.getByRole('alert')).toHaveText('TRK-1 can only move to To Do or In Review.')
  await expect(headings).toHaveText(['To Do (0)', 'In Progress (1)', 'In Review (0)', 'Done (0)'])
  await saved()

  // A move refused from the keyboard goes back and keeps the focus on the card.
  await card('In Progress').getByRole('link', { name: 'TRK-1' }).focus()
  await page.keyboard.press('Space')
  await expect(announced('TRK-1 is over In Progress.')).toHaveCount(1)
  await page.keyboard.press('ArrowRight')
  await expect(announced('TRK-1 is over In Review.')).toHaveCount(1)
  await page.keyboard.press('ArrowRight')
  await expect(announced('TRK-1 is over Done.')).toHaveCount(1)
  await page.keyboard.press('Space')
  await expect(page.getByRole('alert')).toHaveText('TRK-1 can only move to To Do or In Review.')
  await saved()
  await expect(card('In Progress').getByRole('link', { name: 'TRK-1' })).toBeFocused()

  // Keyboard: Space picks up, an arrow changes column, Enter drops without opening the item.
  await page.keyboard.press('Space')
  await expect(announced('TRK-1 is over In Progress.')).toHaveCount(1)
  await page.keyboard.press('ArrowRight')
  await expect(announced('TRK-1 is over In Review.')).toHaveCount(1)
  await page.keyboard.press('Enter')
  await expect(page).toHaveURL(/\/TRK\/board$/)
  await expect(headings).toHaveText(['To Do (0)', 'In Progress (0)', 'In Review (1)', 'Done (0)'])
  await saved()
  await expect(page.getByRole('alert')).toHaveCount(0)
  await expect(card('In Review').getByRole('link', { name: 'TRK-1' })).toBeFocused()

  // Escape cancels a keyboard move.
  await page.keyboard.press('Space')
  await expect(announced('TRK-1 is over In Review.')).toHaveCount(1)
  await page.keyboard.press('ArrowRight')
  await expect(announced('TRK-1 is over Done.')).toHaveCount(1)
  await page.keyboard.press('Escape')
  await expect(announced('Move of TRK-1 cancelled.')).toHaveCount(1)
  await expect(headings).toHaveText(['To Do (0)', 'In Progress (0)', 'In Review (1)', 'Done (0)'])

  // Enter on the link still opens the item, and the move is in its history.
  await card('In Review').getByRole('link', { name: 'TRK-1' }).focus()
  await page.keyboard.press('Enter')
  await expect(page).toHaveURL(/\/TRK\/items\/TRK-1$/)
  await expect(page.getByRole('list', { name: 'History' }).getByRole('listitem').first()).toContainText('changed status In Progress → In Review')
})
