import { expect, test, type Page } from '@playwright/test'

async function createItem(page: Page, type: string, title: string, parentKey?: string) {
  await page.getByRole('button', { name: 'New item' }).click()
  await page.getByLabel('Type').selectOption(type)
  await page.getByLabel('Title').fill(title)
  if (parentKey) {
    await page.getByRole('combobox', { name: 'Parent' }).fill(parentKey)
    await page.getByRole('listbox', { name: 'Parent' }).getByRole('option', { name: new RegExp(parentKey) }).click()
  }
  await page.getByRole('button', { name: 'Create item' }).click()
  await expect(page.getByRole('status')).toHaveText(new RegExp(`^Created TRK-\\d+ · ${title}\\.$`))
}

test('the list is filtered from the filter bar, keeps its filters on reload and hides completed epics', async ({ page }) => {
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

  await createItem(page, 'epic', 'Release one')
  await createItem(page, 'task', 'Build login', 'TRK-1')
  await createItem(page, 'task', 'Write docs')

  // TRK-2 is mine.
  await page.getByRole('link', { name: 'TRK-2' }).click()
  await page.getByRole('combobox', { name: 'Assignee' }).selectOption({ label: name })
  await expect(page.getByRole('status')).toHaveText(`TRK-2 assigned to ${name}.`)
  await page.getByRole('navigation', { name: 'Breadcrumb' }).getByRole('link', { name: 'TRK' }).click()

  const rows = page.getByRole('table', { name: 'Work items' }).getByRole('row')
  const keys = page.getByRole('table', { name: 'Work items' }).getByRole('link')
  // Newest update first: TRK-2 was just assigned.
  await expect(keys).toHaveText(['TRK-2', 'TRK-3', 'TRK-1'])
  await expect(page.getByText('3 items match')).toBeVisible()

  // Open + Mine: only TRK-2, in the URL, and the same after a reload.
  const quick = page.getByRole('group', { name: 'Quick filters' })
  await quick.getByRole('button', { name: 'Open' }).click()
  await quick.getByRole('button', { name: 'Mine' }).click()
  await expect(page).toHaveURL(/\/TRK\?open=true&assignee=me$/)
  await expect(keys).toHaveText(['TRK-2'])
  await expect(page.getByText('1 item matches')).toBeVisible()
  await page.reload()
  await expect(keys).toHaveText(['TRK-2'])
  await expect(quick.getByRole('button', { name: 'Open' })).toHaveAttribute('aria-pressed', 'true')
  await expect(quick.getByRole('button', { name: 'Mine' })).toHaveAttribute('aria-pressed', 'true')
  await expect(page.getByRole('list', { name: 'Active filters' }).getByRole('button')).toHaveText(['Open×', 'Assignee: Me×'])

  // `/` jumps to the search box.
  await page.getByRole('button', { name: 'Clear all' }).click()
  await expect(page).toHaveURL(/\/TRK$/)
  // "Clear all" leaves the focus in the search box; from anywhere else, `/` comes back to it without typing a "/".
  await page.getByRole('heading', { level: 1 }).click()
  await page.keyboard.press('/')
  await expect(page.getByRole('searchbox', { name: 'Search' })).toBeFocused()
  await page.keyboard.type('docs')
  await expect(page).toHaveURL(/\/TRK\?q=docs$/)
  await expect(keys).toHaveText(['TRK-3'])
  await page.getByRole('button', { name: 'Remove filter Search: “docs”' }).click()
  await expect(rows).toHaveCount(4)

  // The epic goes to Done: it and everything under it leave the list; TRK-3 has no epic and stays.
  await page.getByRole('link', { name: 'TRK-1' }).click()
  const status = page.getByRole('combobox', { name: 'Status' })
  for (const [value, label] of [['in_progress', 'In Progress'], ['in_review', 'In Review'], ['done', 'Done']] as const) {
    await status.selectOption(value)
    await expect(page.getByRole('status')).toHaveText(`TRK-1 moved to ${label}.`)
  }
  await page.getByRole('navigation', { name: 'Breadcrumb' }).getByRole('link', { name: 'TRK' }).click()
  await expect(keys).toHaveText(['TRK-3'])
  await expect(page.getByText('1 item matches')).toBeVisible()

  // "Show completed epics" brings them back, and only that is recorded in the URL.
  // Checked once the URL has changed (a router navigation), so click and wait rather than `check()`.
  const showCompleted = page.getByRole('checkbox', { name: 'Show completed epics' })
  await showCompleted.click()
  await expect(showCompleted).toBeChecked()
  await expect(page).toHaveURL(/\/TRK\?completedEpics=show$/)
  await expect(rows).toHaveCount(4)
  await expect(page.getByText('3 items match')).toBeVisible()
})
