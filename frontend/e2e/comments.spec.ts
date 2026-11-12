import { expect, test } from '@playwright/test'

test('comments are posted in Markdown, shown oldest first, edited and deleted by their author, and appear in the history', async ({ page }) => {
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
  await page.getByLabel('Type').selectOption('story')
  await page.getByLabel('Title').fill('Comments on items')
  await page.getByRole('button', { name: 'Create item' }).click()
  await page.getByRole('cell', { name: 'Comments on items' }).click()
  await expect(page).toHaveURL(/\/TRK\/items\/TRK-1$/)
  await expect(page.getByText('No comments yet.')).toBeVisible()

  const box = page.getByLabel('Add a comment')
  const comments = page.getByRole('list', { name: 'Comments' }).getByRole('listitem')

  // An empty comment is not sent.
  await page.getByRole('button', { name: 'Post comment' }).click()
  await expect(page.getByText('Write something to comment.')).toBeVisible()
  await expect(box).toBeFocused()

  await box.fill("Let's keep comments **flat**, no threads in v1.0.")
  await page.getByRole('button', { name: 'Post comment' }).click()
  await expect(page.getByRole('status')).toHaveText('Comment posted.')
  await expect(box).toHaveValue('')
  await expect(comments).toHaveCount(1)
  await expect(comments.first()).toContainText(name)
  await expect(comments.first()).toContainText('now')
  await expect(comments.first().locator('strong')).toHaveText('flat')

  await box.fill('Agreed.')
  await page.getByRole('button', { name: 'Post comment' }).click()
  await expect(comments).toHaveCount(2)

  // Oldest first, and still there after a reload.
  await page.reload()
  await expect(comments).toHaveCount(2)
  await expect(comments.first()).toContainText('no threads in v1.0.')
  await expect(comments.last()).toContainText('Agreed.')

  // Each comment is in the history, newest first.
  const events = page.getByRole('list', { name: 'History' }).getByRole('listitem')
  await expect(events).toHaveCount(3)
  await expect(events.first()).toContainText(`${name} commented “Agreed.”`)
  await expect(events.nth(1)).toContainText(`${name} commented “Let's keep comments **flat**, no threads in v1.0.”`)

  // The author edits a comment: it shows "edited", and the edit is in the history.
  const agreed = comments.nth(1)
  await agreed.getByRole('button', { name: 'Edit' }).click()
  const editBox = agreed.getByLabel('Edit comment')
  await expect(editBox).toBeFocused()
  await editBox.fill('Agreed, ship it.')
  await agreed.getByRole('button', { name: 'Save' }).click()
  await expect(page.getByRole('status')).toHaveText('Comment saved.')
  const shipped = comments.filter({ hasText: 'Agreed, ship it.' })
  await expect(shipped).toContainText('edited')
  await expect(events.first()).toContainText(`${name} edited a comment: “Agreed, ship it.”`)

  // Deleting asks first; the comment is then gone, and the history says so, also for an older comment.
  const older = comments.first()
  await older.getByRole('button', { name: 'Delete' }).click()
  await older.getByRole('button', { name: 'Delete comment' }).click()
  await expect(page.getByRole('status')).toHaveText('Comment deleted.')
  await expect(comments).toHaveCount(1)
  await expect(page.getByRole('heading', { name: 'Comments', exact: true })).toBeFocused()
  await expect(events.first()).toContainText(`${name} deleted a comment`)

  await page.reload()
  await expect(comments).toHaveCount(1)
  await expect(comments.first()).toContainText('Agreed, ship it.')
})
