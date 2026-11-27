import { expect, type APIRequestContext, type Page } from '@playwright/test'

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
export async function inviteLink(page: Page, request: APIRequestContext, to: string): Promise<string> {
  const notice = page.getByRole('status').filter({ hasText: to })
  await expect(notice).toBeVisible()
  if ((await notice.textContent())?.includes('no email went out')) return page.getByLabel('Invite link').inputValue()
  return linkFromMailpit(request, to)
}
