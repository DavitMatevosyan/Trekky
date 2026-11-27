import { expect, test } from '@playwright/test'
import { inviteLink } from './inviteLink.ts'

test('an owner invites a viewer by email, who opens the link, signs up and lands in the owner’s organization', async ({ page, browser, request }) => {
  const name = `e2e-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
  const organization = `E2E Invites ${name}`
  const inviteeEmail = `${name}-invitee@example.com`

  await page.goto('/sign-up')
  await page.getByLabel('Email').fill(`${name}@example.com`)
  await page.getByLabel('Password').fill('correct-horse-battery')
  await page.getByLabel('Organization').fill(organization)
  await page.getByRole('button', { name: 'Create account' }).click()

  await page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Members' }).click()
  await expect(page).toHaveURL(/\/members$/)
  const form = page.getByRole('form', { name: 'Invite people' })
  await form.getByLabel('Email').fill(inviteeEmail)
  await form.getByLabel('Role').selectOption('viewer')
  await form.getByRole('button', { name: 'Send invite' }).click()

  const link = await inviteLink(page, request, inviteeEmail)
  const pending = page.getByRole('list', { name: 'Pending invites' })
  await expect(pending).toContainText(inviteeEmail)
  await expect(pending).toContainText('Viewer')

  // The invitee: a fresh browser with no session.
  const inviteeContext = await browser.newContext()
  const invitee = await inviteeContext.newPage()
  await invitee.goto(link)
  await expect(invitee.getByText(`${name} invited you to join ${organization} as a Viewer.`)).toBeVisible()
  await expect(invitee.getByLabel('Email')).toHaveValue(inviteeEmail)
  await invitee.getByLabel('Your name').fill('Ines Invitee')
  await invitee.getByLabel('Password').fill('correct-horse-battery')
  await invitee.getByRole('button', { name: 'Join' }).click()

  // Signed in, in the owner's organization, with the invited role: no new organization, nothing to create.
  await expect(invitee.getByTestId('org-summary')).toHaveText(`You are the Viewer of ${organization}.`)
  await expect(invitee.getByRole('button', { name: 'New project' })).toHaveCount(0)
  await invitee.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Members' }).click()
  await expect(invitee.getByRole('form', { name: 'Invite people' })).toHaveCount(0)
  await expect(invitee.getByRole('list', { name: 'Members of your organization' })).toContainText(`${name}@example.com`)
  await inviteeContext.close()

  // The owner sees the new member, and the invite is no longer pending.
  await page.reload()
  const members = page.getByRole('list', { name: 'Members of your organization' })
  await expect(members.getByRole('listitem').filter({ hasText: inviteeEmail })).toContainText('Viewer')
  await expect(page.getByText('No pending invites.')).toBeVisible()

  // The link works once.
  const againContext = await browser.newContext()
  const again = await againContext.newPage()
  await again.goto(link)
  await expect(again.getByRole('alert')).toHaveText('This invite has already been used. Log in instead.')
  await againContext.close()
})

test('a withdrawn invite’s link says so', async ({ page, browser, request }) => {
  const name = `e2e-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
  const inviteeEmail = `${name}-withdrawn@example.com`

  await page.goto('/sign-up')
  await page.getByLabel('Email').fill(`${name}@example.com`)
  await page.getByLabel('Password').fill('correct-horse-battery')
  await page.getByLabel('Organization').fill('E2E Withdrawn')
  await page.getByRole('button', { name: 'Create account' }).click()
  await page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: 'Members' }).click()

  const form = page.getByRole('form', { name: 'Invite people' })
  await form.getByLabel('Email').fill(inviteeEmail)
  await form.getByRole('button', { name: 'Send invite' }).click()
  const link = await inviteLink(page, request, inviteeEmail)

  page.once('dialog', (dialog) => void dialog.accept())
  await page.getByRole('button', { name: `Withdraw the invite to ${inviteeEmail}` }).click()
  await expect(page.getByText('No pending invites.')).toBeVisible()

  const context = await browser.newContext()
  const invitee = await context.newPage()
  await invitee.goto(link)
  await expect(invitee.getByRole('heading', { name: 'This invite can’t be used' })).toBeVisible()
  await expect(invitee.getByRole('alert')).toContainText('This invite was withdrawn.')
  await context.close()
})
