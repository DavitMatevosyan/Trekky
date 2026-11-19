import { expect, test } from '@playwright/test'

test('the chosen theme is saved on the device and painted before the app starts', async ({ page }) => {
  // Records the theme when parsing ends: after the blocking public/theme.js, before any module script (React) runs.
  // A string, because the e2e TypeScript has no DOM types.
  await page.addInitScript({
    content: `document.addEventListener('readystatechange', () => {
      if (document.readyState === 'interactive') window.darkBeforeApp = document.documentElement.classList.contains('dark')
    })`,
  })
  await page.emulateMedia({ colorScheme: 'light' })

  await page.goto('/sign-up')
  await page.getByLabel('Email').fill(`e2e-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.com`)
  await page.getByLabel('Password').fill('correct-horse-battery')
  await page.getByLabel('Organization').fill('E2E Org')
  await page.getByRole('button', { name: 'Create account' }).click()

  const theme = page.getByRole('group', { name: 'Theme' })
  const html = page.locator('html')
  await expect(theme.getByRole('radio', { name: 'System' })).toBeChecked()
  await expect(html).not.toHaveClass(/dark/)

  // System follows the operating system while the page is open.
  await page.emulateMedia({ colorScheme: 'dark' })
  await expect(html).toHaveClass(/dark/)
  await page.emulateMedia({ colorScheme: 'light' })
  await expect(html).not.toHaveClass(/dark/)

  const bodyBackground = () => page.evaluate('getComputedStyle(document.body).backgroundColor')
  const lightBackground = await bodyBackground()

  // Dark is kept across a reload and is there before the app runs, so the page never flashes light.
  await theme.getByTitle('Dark').click()
  await expect(html).toHaveClass(/dark/)
  await page.reload()
  // The silent sign-in after a reload loads the page again; read the flag once the app has settled on the last load.
  await expect(theme.getByRole('radio', { name: 'Dark' })).toBeChecked()
  expect(await page.evaluate('window.darkBeforeApp')).toBe(true)

  // The body takes the dark theme's own background (warm charcoal), not the light one and not black.
  const darkBackground = await bodyBackground()
  expect(darkBackground).not.toBe(lightBackground)
  expect(darkBackground).not.toBe('rgb(0, 0, 0)')
})
