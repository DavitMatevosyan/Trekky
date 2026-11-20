import { expect, test, type APIRequestContext, type Page } from '@playwright/test'

/** Mailpit's API (docker compose, port 8025); the local and CI API send invite email there. */
const mailpitUrl = process.env.MAILPIT_URL ?? 'http://localhost:8025'

/** The invite link from the email in Mailpit, waiting for it to arrive. */
async function linkFromMailpit(request: APIRequestContext, to: string): Promise<string> {
  let link = ''
  await expect(async () => {
    const search = await request.get(`${mailpitUrl}/api/v1/search`, { params: { query: `to:"${to}"` } })
    expect(search.ok()).toBe(true)
    const { messages } = (await search.json()) as { messages: { ID: string }[] }
    const first = messages[0]
    expect(first).toBeDefined()
    const message = await request.get(`${mailpitUrl}/api/v1/message/${first?.ID ?? ''}`)
    const { Text } = (await message.json()) as { Text: string }
    link = /https?:\/\/\S+\/invite#[\w-]+/.exec(Text)?.[0] ?? ''
    expect(link).not.toBe('')
  }).toPass({ timeout: 15_000 })
  return link
}

/**
 * The link the invite went out with: from the email when one was sent (Mailpit), otherwise from the page, which shows
 * it to copy when no mail server is configured (e.g. a deployed stack without SMTP, ADR-0027).
 */
async function inviteLink(page: Page, request: APIRequestContext, to: string): Promise<string> {
  const notice = page.getByRole('status').filter({ hasText: to })
  await expect(notice).toBeVisible()
  if ((await notice.textContent())?.includes('no email went out')) return page.getByLabel('Invite link').inputValue()
  return linkFromMailpit(request, to)
}

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
