import type { Server } from 'node:http'
import { expect, test } from '@playwright/test'
import { REPOSITORY, startFakeGitHub } from './support/fakeGitHub.ts'

// The fake GitHub only exists for the stack Playwright starts (playwright.config.ts), not for a deployed one.
test.skip(!!process.env.E2E_BASE_URL, 'Needs the fake GitHub that the locally started API is configured with.')

let fakeGitHub: Server

test.beforeAll(async ({ baseURL }) => {
  fakeGitHub = await startFakeGitHub(baseURL ?? 'http://localhost:5173')
})

test.afterAll(async () => {
  // The API keeps its connections to GitHub open; close them, or close() waits for them to time out.
  fakeGitHub.closeAllConnections()
  await new Promise((resolve) => fakeGitHub.close(resolve))
})

test('an owner connects GitHub, links a project and reads, follows and searches its docs', async ({ page }) => {
  await page.goto('/sign-up')
  await page.getByLabel('Email').fill(`e2e-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.com`)
  await page.getByLabel('Password').fill('correct-horse-battery')
  await page.getByLabel('Organization').fill('E2E Docs')
  await page.getByRole('button', { name: 'Create account' }).click()
  await expect(page.getByText('No projects yet. Create one to start tracking work.')).toBeVisible()

  // TREK-74: install the app on GitHub (the fake sends the browser straight back) and connect it.
  await page.getByRole('link', { name: 'Settings' }).click()
  await page.getByRole('button', { name: 'Connect GitHub' }).click()
  await expect(page.getByText('Connected acme.')).toBeVisible()

  await page.goto('/')
  await page.getByRole('button', { name: 'New project' }).click()
  await page.getByLabel('Name').fill('Handbook')
  await page.getByLabel('Key').fill('DOC')
  await page.getByRole('button', { name: 'Create project' }).click()
  await page.getByRole('link', { name: /Handbook/ }).click()

  await page.getByRole('navigation', { name: 'Project views' }).getByRole('link', { name: 'Settings' }).click()
  await page.getByRole('combobox', { name: 'Repository' }).selectOption(REPOSITORY)
  await page.getByRole('button', { name: 'Save' }).click()
  await expect(page.getByText(/Saved\. Trekky checked the repository/)).toBeVisible()

  // TREK-75: the docs tab opens the README, rendered as on GitHub.
  await page.getByRole('navigation', { name: 'Project views' }).getByRole('link', { name: 'Docs' }).click()
  const article = page.getByRole('article')
  await expect(article.getByRole('heading', { level: 1, name: 'Handbook' })).toBeVisible()
  await expect(article.getByRole('cell', { name: '5080' })).toBeVisible()
  await expect(article.getByRole('checkbox')).toBeChecked()
  await expect(article.getByText(/Last changed by Ada Lovelace/)).toBeVisible()
  await expect(article.getByRole('figure', { name: 'Diagram' }).locator('svg')).toBeVisible()
  const image = article.getByRole('img', { name: 'Flow chart' })
  await expect(image).toBeVisible()
  // Loaded and decoded, not just placed: a 1×1 PNG.
  await expect(image).toHaveJSProperty('naturalWidth', 1)

  // A relative link opens the other doc inside Trekky, at its heading.
  await article.getByRole('link', { name: 'setting up' }).click()
  await expect(page).toHaveURL(/\/DOC\/docs\/guide\/setup\.md#install$/)
  await expect(article.getByRole('heading', { level: 1, name: 'Setting up' })).toBeVisible()
  await expect(page.getByRole('navigation', { name: 'Docs' }).getByRole('link', { name: 'Setting up' })).toHaveAttribute('aria-current', 'page')

  // Full-text search, by word prefix, with the match marked.
  await page.getByRole('searchbox', { name: 'Search docs' }).fill('dock comp')
  const results = page.getByRole('region', { name: 'Search docs' })
  await expect(results.getByText('1 doc found')).toBeVisible()
  await expect(results.locator('mark').first()).toHaveText('Docker')
  await results.getByRole('link', { name: 'Setting up' }).click()
  await expect(page).toHaveURL(/\/DOC\/docs\/guide\/setup\.md$/)

  // Still there after a reload, straight from the URL.
  await page.reload()
  await expect(article.getByRole('heading', { level: 1, name: 'Setting up' })).toBeVisible()
})
