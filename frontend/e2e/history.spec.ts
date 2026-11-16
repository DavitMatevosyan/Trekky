import { expect, test } from '@playwright/test'

test('a status change shows up in the item history, newest first', async ({ page }) => {
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
  await page.getByLabel('Title').fill('Write the history tab')
  await page.getByRole('button', { name: 'Create item' }).click()
  await expect(page.getByRole('status')).toHaveText('Created TRK-1 · Write the history tab.')

  await page.getByRole('cell', { name: 'Write the history tab' }).click()
  await expect(page).toHaveURL(/\/TRK\/items\/TRK-1$/)
  const events = page.getByRole('list', { name: 'History' }).getByRole('listitem')
  await expect(events).toHaveCount(1)
  await expect(events.first()).toContainText(`${name} created this Task`)

  await page.getByRole('combobox', { name: 'Status' }).selectOption('in_progress')
  await expect(page.getByRole('status')).toHaveText('TRK-1 moved to In Progress.')

  // The event arrives through the outbox a moment after the save; the history catches up by itself.
  await expect(events).toHaveCount(2)
  await expect(events.first()).toContainText(`${name} changed status To Do → In Progress`)
  await expect(events.last()).toContainText(`${name} created this Task`)

  await page.reload()
  await expect(events.first()).toContainText(`${name} changed status To Do → In Progress`)
})
