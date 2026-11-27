import { expect, test } from '@playwright/test'
import { inviteLink } from './inviteLink.ts'

test('an owner changes a member’s role and removes them; they are signed out and their work keeps their name', async ({ page, browser, request }) => {
  test.slow()
  const name = `e2e-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
  const organization = `E2E Members ${name}`
  const memberEmail = `${name}-member@example.com`

  // Arrange: an Owner with a project, and a Member who joined by invite and created an item.
  await page.goto('/sign-up')
  await page.getByLabel('Email').fill(`${name}@example.com`)
  await page.getByLabel('Password').fill('correct-horse-battery')
  await page.getByLabel('Organization').fill(organization)
  await page.getByRole('button', { name: 'Create account' }).click()
  await page.getByRole('button', { name: 'New project' }).click()
  await page.getByLabel('Name').fill('Trekky')
  await page.getByRole('button', { name: 'Create project' }).click()
  await expect(page.getByRole('list', { name: 'Projects' }).getByRole('link', { name: 'Trekky' })).toBeVisible()

  await page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Members' }).click()
  const form = page.getByRole('form', { name: 'Invite people' })
  await form.getByLabel('Email').fill(memberEmail)
  await form.getByLabel('Role').selectOption('member')
  await form.getByRole('button', { name: 'Send invite' }).click()
  const link = await inviteLink(page, request, memberEmail)

  const memberContext = await browser.newContext()
  const member = await memberContext.newPage()
  await member.goto(link)
  await member.getByLabel('Your name').fill('Mia Member')
  await member.getByLabel('Password').fill('correct-horse-battery')
  await member.getByRole('button', { name: 'Join' }).click()
  await expect(member.getByTestId('org-summary')).toHaveText(`You are the Member of ${organization}.`)
  await member.getByRole('list', { name: 'Projects' }).getByRole('link', { name: 'Trekky' }).click()
  await member.getByRole('button', { name: 'New item' }).click()
  await member.getByLabel('Title').fill('Written before leaving')
  await member.getByRole('button', { name: 'Create item' }).click()
  await expect(member.getByRole('status')).toHaveText('Created TRK-1 · Written before leaving.')

  // Everyone sees name, email, person or bot, and role; the only Owner has no controls on their own row.
  await page.reload()
  const members = page.getByRole('list', { name: 'Members of your organization' })
  const miaRow = members.getByRole('listitem').filter({ hasText: memberEmail })
  await expect(miaRow).toContainText('Mia Member')
  await expect(miaRow).toContainText('Person')
  await expect(page.getByRole('combobox', { name: 'Role of Mia Member' })).toHaveValue('member')
  await expect(members.getByRole('listitem').filter({ hasText: '(you)' }).getByRole('combobox')).toHaveCount(0)

  // A role change: the member's next page load signs in again silently and brings the new role.
  await page.getByRole('combobox', { name: 'Role of Mia Member' }).selectOption('viewer')
  await page.getByRole('button', { name: 'Save role of Mia Member' }).click()
  await expect(page.getByRole('status')).toHaveText('Mia Member is now Viewer.')
  await member.goto('/')
  await expect(member.getByTestId('org-summary')).toHaveText(`You are the Viewer of ${organization}.`)
  await expect(member.getByRole('button', { name: 'New project' })).toHaveCount(0)

  // Removing: after confirming, the member is gone from the list.
  await page.getByRole('button', { name: 'Remove Mia Member' }).click()
  await page.getByRole('group', { name: /^Remove Mia Member from your organization\?/ }).getByRole('button', { name: 'Remove' }).click()
  await expect(page.getByRole('status')).toHaveText('Mia Member was removed from your organization.')
  await expect(members).not.toContainText(memberEmail)

  // Their open tab is signed out on its next request, and they cannot log in again.
  await member.reload()
  await expect(member).toHaveURL(/\/login/)
  await member.getByLabel('Email').fill(memberEmail)
  await member.getByLabel('Password').fill('correct-horse-battery')
  await member.getByRole('button', { name: 'Log in' }).click()
  await expect(member.getByRole('alert')).toContainText('This account no longer belongs to an organization.')
  await memberContext.close()

  // Their past work keeps their name.
  await page.goto('/TRK/items/TRK-1')
  await expect(page.getByRole('list', { name: 'History' }).getByRole('listitem').last()).toContainText('Mia Member created this Task')
})
