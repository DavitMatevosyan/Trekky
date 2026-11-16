import { expect, test } from '@playwright/test'

test('an item is assigned to a member, shown on the detail, list and board, and cleared again', async ({ page }) => {
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
  await page.getByLabel('Title').fill('Assignee on items')
  await page.getByRole('button', { name: 'Create item' }).click()
  const row = page.getByRole('row', { name: /Assignee on items/ })
  await expect(row).toContainText('Nobody')

  await page.getByRole('cell', { name: 'Assignee on items' }).click()
  await expect(page).toHaveURL(/\/TRK\/items\/TRK-1$/)
  const assignee = page.getByRole('combobox', { name: 'Assignee' })
  await expect(assignee).toHaveValue('')

  // The only member is the one who signed up (invites come in v1.1).
  await assignee.selectOption({ label: name })
  await expect(page.getByRole('status')).toHaveText(`TRK-1 assigned to ${name}.`)
  const events = page.getByRole('list', { name: 'History' }).getByRole('listitem')
  await expect(events.first()).toContainText(`${name} assigned this to ${name}`)

  // Shown on the list and the board too.
  await page.getByRole('link', { name: 'TRK', exact: true }).click()
  await expect(row).toContainText(name)
  await page.getByRole('navigation', { name: 'Project views' }).getByRole('link', { name: 'Board' }).click()
  await expect(page.getByRole('list', { name: 'To Do' }).getByRole('listitem')).toContainText(`Assignee: ${name}`)

  // Back to nobody.
  await page.getByRole('list', { name: 'To Do' }).getByRole('link', { name: 'TRK-1' }).click()
  await assignee.selectOption({ label: 'Nobody' })
  await expect(page.getByRole('status')).toHaveText('TRK-1 is assigned to nobody.')
  await expect(events.first()).toContainText(`${name} unassigned ${name}`)
  await page.reload()
  await expect(assignee).toHaveValue('')
})
