import { expect, test } from '@playwright/test'

const password = 'correct-horse-battery'

function uniqueEmail(): string {
  return `e2e-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@example.com`
}

test('a new user signs up, logs out and logs back in', async ({ page }) => {
  const email = uniqueEmail()

  await page.goto('/sign-up')
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Password').fill(password)
  await page.getByLabel('Organization').fill('E2E Org')
  await page.getByRole('button', { name: 'Create account' }).click()

  // US-01 AC4: lands on the start page, logged in, as the Owner of the new organization.
  await expect(page.getByRole('heading', { name: 'Projects' })).toBeVisible()
  await expect(page.getByTestId('org-summary')).toHaveText('You are the Owner of E2E Org.')

  // US-02 AC3: a reload keeps the user logged in (silent code flow from the session cookie).
  await page.reload()
  await expect(page.getByTestId('org-summary')).toHaveText('You are the Owner of E2E Org.')

  // US-02 AC4: log out ends the session; the back button does not show the protected page.
  await page.getByRole('button', { name: 'Log out' }).click()
  await expect(page.getByRole('heading', { name: 'Log in to Trekky' })).toBeVisible()
  await page.goBack()
  await expect(page.getByRole('heading', { name: 'Log in to Trekky' })).toBeVisible()
  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'Log in to Trekky' })).toBeVisible()

  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Password').fill(password)
  await page.getByRole('button', { name: 'Log in' }).click()

  await expect(page.getByTestId('org-summary')).toHaveText('You are the Owner of E2E Org.')
})

test('a wrong password shows one generic message', async ({ page }) => {
  await page.goto('/login')
  await page.getByLabel('Email').fill(uniqueEmail())
  await page.getByLabel('Password').fill('not-the-right-password')
  await page.getByRole('button', { name: 'Log in' }).click()

  await expect(page.getByRole('alert')).toHaveText('Email or password is incorrect.')
})
