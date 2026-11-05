import { expect, test } from '@playwright/test'

test('a user creates work items with a parent, opens one, edits it and moves it', async ({ page }) => {
  await page.goto('/sign-up')
  await page.getByLabel('Email').fill(`e2e-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.com`)
  await page.getByLabel('Password').fill('correct-horse-battery')
  await page.getByLabel('Organization').fill('E2E Org')
  await page.getByRole('button', { name: 'Create account' }).click()

  await page.getByRole('button', { name: 'New project' }).click()
  await page.getByLabel('Name').fill('Trekky')
  await page.getByRole('button', { name: 'Create project' }).click()
  await page.getByRole('list', { name: 'Projects' }).getByRole('link', { name: 'Trekky' }).click()
  await expect(page).toHaveURL(/\/TRK$/)
  await expect(page.getByText('No items yet. Create one to start tracking work.')).toBeVisible()

  // US-06: an Epic, then a Story under it by key.
  await page.getByRole('button', { name: 'New item' }).click()
  await page.getByLabel('Type').selectOption('epic')
  await page.getByLabel('Title').fill('v1.0 Dogfood')
  await page.getByRole('button', { name: 'Create item' }).click()
  await expect(page.getByRole('status')).toHaveText('Created TRK-1 · v1.0 Dogfood.')

  // A rule violation names the allowed parents (US-06 AC5).
  await page.getByRole('button', { name: 'New item' }).click()
  await page.getByLabel('Type').selectOption('subtask')
  await page.getByLabel('Title').fill('Orphan')
  await page.getByRole('button', { name: 'Create item' }).click()
  await expect(page.getByText('A SubTask needs a parent: Story or Task.')).toBeVisible()

  await page.getByLabel('Type').selectOption('story')
  await page.getByLabel('Title').fill('Comments on items')
  await page.getByLabel('Description').fill('Members can **discuss** an item.')
  // TRK-11.1: pick the parent from the candidates of the allowed types.
  const parent = page.getByRole('combobox', { name: 'Parent' })
  await parent.fill('dog')
  await page.getByRole('listbox', { name: 'Parent' }).getByRole('option', { name: /TRK-1/ }).click()
  await expect(parent).toHaveValue('TRK-1 · v1.0 Dogfood')
  await page.getByRole('button', { name: 'Create item' }).click()
  await expect(page.getByRole('status')).toHaveText('Created TRK-2 · Comments on items.')

  // US-08: newest update first, with the parent key.
  const rows = page.getByRole('table', { name: 'Work items' }).getByRole('row')
  await expect(rows.nth(1)).toContainText('TRK-2StoryComments on itemsTo DoTRK-1')
  await expect(rows.nth(2)).toContainText('TRK-1Epicv1.0 DogfoodTo Do—')
  await page.reload()
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('TRK · Trekky')

  // US-09: the detail page lives at the item's key.
  await page.getByText('Comments on items').click()
  await expect(page).toHaveURL(/\/TRK\/items\/TRK-2$/)
  await expect(page.locator('strong', { hasText: 'discuss' })).toBeVisible()
  await expect(page.getByRole('link', { name: 'TRK-1 Epic' })).toBeVisible()

  // US-07: edit the title.
  await page.getByRole('button', { name: 'Edit' }).click()
  await page.getByLabel('Title').fill('Comments on work items')
  await page.getByRole('button', { name: 'Save' }).click()
  await expect(page.getByRole('heading', { level: 1 })).toContainText('Comments on work items')

  // US-10: the status select offers only allowed moves; a move is saved and survives a reload.
  const status = page.getByRole('combobox', { name: 'Status' })
  await expect(status.getByRole('option')).toHaveText(['To Do', 'In Progress'])
  await status.selectOption('in_progress')
  await expect(page.getByRole('status')).toHaveText('TRK-2 moved to In Progress.')
  await expect(status.getByRole('option')).toHaveText(['To Do', 'In Progress', 'In Review'])
  await page.reload()
  await expect(status).toHaveValue('in_progress')

  // The parent lists its child, and the projects page counts both items.
  await page.getByRole('link', { name: 'TRK-1 Epic' }).click()
  await expect(page.getByRole('list', { name: 'Children' })).toContainText('TRK-2StoryComments on work itemsIn Progress')
  await page.getByRole('link', { name: 'Trekky', exact: true }).click()
  await expect(page.getByRole('list', { name: 'Projects' }).getByRole('listitem')).toHaveText(['TRKTrekky2 items'])
})
