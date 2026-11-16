import { expect, test } from '@playwright/test'

test('a new user creates a project and sees it in the list', async ({ page }) => {
  await page.goto('/sign-up')
  await page.getByLabel('Email').fill(`e2e-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.com`)
  await page.getByLabel('Password').fill('correct-horse-battery')
  await page.getByLabel('Organization').fill('E2E Org')
  await page.getByRole('button', { name: 'Create account' }).click()

  // US-05: the start page lists projects; a new organization has none.
  await expect(page.getByText('No projects yet. Create one to start tracking work.')).toBeVisible()

  await page.getByRole('button', { name: 'New project' }).click()
  await page.getByLabel('Name').fill('Trekky')
  await expect(page.getByLabel('Key')).toHaveValue('TRK')
  await page.getByRole('button', { name: 'Create project' }).click()

  const list = page.getByRole('list', { name: 'Projects' })
  await expect(list.getByRole('listitem')).toHaveText(['TRKTrekky0 items'])

  // The key is unique in the organization.
  await page.getByRole('button', { name: 'New project' }).click()
  await page.getByLabel('Name').fill('Trekky again')
  await page.getByLabel('Key').fill('TRK')
  await page.getByRole('button', { name: 'Create project' }).click()
  await expect(page.getByText('A project with this key already exists in your organization.')).toBeVisible()

  // Still there after a reload (silent sign-in, then the list loads again).
  await page.reload()
  await expect(list.getByRole('listitem')).toHaveText(['TRKTrekky0 items'])
})
