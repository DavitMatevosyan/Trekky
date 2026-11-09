import { expect, test } from '@playwright/test'

test('the board shows an item in the column of its status', async ({ page }) => {
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

  await page.getByRole('button', { name: 'New item' }).click()
  await page.getByLabel('Type').selectOption('task')
  await page.getByLabel('Title').fill('Build the board')
  await page.getByRole('button', { name: 'Create item' }).click()
  await expect(page.getByRole('status')).toHaveText('Created TRK-1 · Build the board.')

  const views = page.getByRole('navigation', { name: 'Project views' })
  await views.getByRole('link', { name: 'Board' }).click()
  await expect(page).toHaveURL(/\/TRK\/board$/)
  await expect(page.getByRole('heading', { level: 2 })).toHaveText(['To Do (1)', 'In Progress (0)', 'In Review (0)', 'Done (0)'])
  const toDoCard = page.getByRole('list', { name: 'To Do' }).getByRole('listitem')
  await expect(toDoCard).toContainText('TRK-1')
  await expect(toDoCard).toContainText('Task')
  await expect(toDoCard).toContainText('Build the board')

  await toDoCard.getByRole('link', { name: 'TRK-1' }).click()
  await expect(page).toHaveURL(/\/TRK\/items\/TRK-1$/)
  await page.getByRole('combobox', { name: 'Status' }).selectOption('in_progress')
  await expect(page.getByRole('status')).toHaveText('TRK-1 moved to In Progress.')

  await page.goto('/TRK/board')
  await expect(page.getByRole('heading', { level: 2 })).toHaveText(['To Do (0)', 'In Progress (1)', 'In Review (0)', 'Done (0)'])
  await expect(page.getByRole('list', { name: 'In Progress' }).getByRole('listitem')).toContainText('Build the board')
})
