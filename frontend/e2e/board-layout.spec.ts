import { expect, test, type Locator, type Page } from '@playwright/test'

// TREK-44: the board's status columns stay one horizontal row at every width; a narrow screen scrolls it sideways.

async function boardWithOneCard(page: Page) {
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
  await page.getByRole('button', { name: 'New item' }).click()
  await page.getByLabel('Type').selectOption('task')
  await page.getByLabel('Title').fill('Fit the board on a phone')
  await page.getByRole('button', { name: 'Create item' }).click()
  await expect(page.getByRole('status')).toHaveText('Created TRK-1 · Fit the board on a phone.')
  await page.getByRole('navigation', { name: 'Project views' }).getByRole('link', { name: 'Board' }).click()
  await expect(page.getByRole('heading', { level: 2 })).toHaveText(['To Do (1)', 'In Progress (0)', 'In Review (0)', 'Done (0)'])
}

/** Fully on screen from left to right. */
async function isInViewport(page: Page, locator: Locator): Promise<boolean> {
  const box = await locator.boundingBox()
  const width = page.viewportSize()?.width ?? 0
  return box !== null && box.x >= 0 && box.x + box.width <= width
}

/** Equal tops for all four column headings mean the columns sit side by side. */
async function inOneRow(page: Page): Promise<boolean> {
  const tops = await page.getByRole('heading', { level: 2 }).evaluateAll((headings) => headings.map((h) => Math.round(h.getBoundingClientRect().top)))
  return tops.length === 4 && new Set(tops).size === 1
}

test('the board columns stay in one row on narrow and wide screens, and the row scrolls on a phone', async ({ page }) => {
  await boardWithOneCard(page)
  const row = page.getByRole('group', { name: 'Board columns' })
  const scroll = () => row.evaluate((el) => ({ left: el.scrollLeft, width: el.scrollWidth, visible: el.clientWidth }))

  // A phone: still one row, wider than the screen.
  await page.setViewportSize({ width: 390, height: 844 })
  expect(await inOneRow(page)).toBe(true)
  const narrow = await scroll()
  expect(narrow.width).toBeGreaterThan(narrow.visible)
  expect(narrow.left).toBe(0)

  // The keyboard scrolls the focused row, as far as the last column.
  await row.focus()
  await page.keyboard.press('ArrowRight')
  await expect.poll(async () => (await scroll()).left).toBeGreaterThan(0)
  const done = page.getByRole('heading', { name: 'Done (0)' })
  for (let press = 0; press < 40 && !(await isInViewport(page, done)); press++) await page.keyboard.press('ArrowRight')
  await expect(done).toBeInViewport()

  // A desktop: one row that fits, nothing to scroll.
  await page.setViewportSize({ width: 1280, height: 900 })
  expect(await inOneRow(page)).toBe(true)
  const wide = await scroll()
  expect(wide.width).toBeLessThanOrEqual(wide.visible)
})

for (const viewport of [
  { width: 390, height: 844 },
  { width: 700, height: 800 },
]) {
  test(`a card moves by keyboard to a column off screen at ${viewport.width}px`, async ({ page }) => {
    await boardWithOneCard(page)
    await page.setViewportSize(viewport)
    const headings = page.getByRole('heading', { level: 2 })
    const announced = (text: string) => page.getByRole('status').filter({ hasText: text })
    const saved = () => expect(page.locator('[aria-busy="true"]')).toHaveCount(0)
    const link = page.getByRole('link', { name: 'TRK-1' })

    // One column at a time, as the workflow allows; the later targets start off screen.
    const steps = [
      ['To Do', 'In Progress', ['To Do (0)', 'In Progress (1)', 'In Review (0)', 'Done (0)']],
      ['In Progress', 'In Review', ['To Do (0)', 'In Progress (0)', 'In Review (1)', 'Done (0)']],
      ['In Review', 'Done', ['To Do (0)', 'In Progress (0)', 'In Review (0)', 'Done (1)']],
    ] as const
    for (const [from, to, counts] of steps) {
      await link.focus()
      await page.keyboard.press('Space')
      await expect(announced(`TRK-1 is over ${from}.`)).toHaveCount(1)
      await page.keyboard.press('ArrowRight')
      await expect(announced(`TRK-1 is over ${to}.`)).toHaveCount(1)
      await page.keyboard.press('Space')
      await expect(headings).toHaveText([...counts])
      await saved()
    }

    // The card is in Done, keeps the focus and is on screen.
    await expect(page.getByRole('list', { name: 'Done' }).getByRole('link', { name: 'TRK-1' })).toBeFocused()
    await expect(link).toBeInViewport()
  })
}
