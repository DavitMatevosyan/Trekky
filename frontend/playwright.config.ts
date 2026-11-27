import { generateKeyPairSync } from 'node:crypto'
import { defineConfig, devices } from '@playwright/test'

const isCi = !!process.env.CI
/** Set to test a deployed stack instead, e.g. E2E_BASE_URL=https://localhost:8443 (deploy/README.md). */
const deployedUrl = process.env.E2E_BASE_URL

/**
 * The API started here talks to the fake GitHub that e2e/docs.spec.ts runs (TREK-75), as a GitHub App with a key made
 * for this run. The fake doesn't check the key; the .NET tests do. The fake reads the other settings from these
 * variables, which the test workers inherit: this file must not import from e2e/, which the web image's Docker
 * build leaves out while it type-checks this file.
 */
process.env.E2E_FAKE_GITHUB_PORT ??= '5181'
process.env.E2E_FAKE_GITHUB_CLIENT_ID ??= 'Iv23.e2e-fake'
process.env.E2E_FAKE_GITHUB_CLIENT_SECRET ??= 'e2e-fake-secret'
process.env.E2E_FAKE_GITHUB_SLUG ??= 'trekky-e2e'
const fakeGitHub = `http://127.0.0.1:${process.env.E2E_FAKE_GITHUB_PORT}/`
const gitHubApp = {
  GitHub__ClientId: process.env.E2E_FAKE_GITHUB_CLIENT_ID,
  GitHub__ClientSecret: process.env.E2E_FAKE_GITHUB_CLIENT_SECRET,
  GitHub__AppSlug: process.env.E2E_FAKE_GITHUB_SLUG,
  GitHub__PrivateKey: generateKeyPairSync('rsa', { modulusLength: 2048 }).privateKey.export({ type: 'pkcs1', format: 'pem' }).toString(),
  GitHub__ApiUrl: fakeGitHub,
  GitHub__WebUrl: fakeGitHub,
}

/**
 * End-to-end tests against the real API and web app. Postgres must be running (`docker compose up -d`);
 * Playwright starts the API and the Vite dev server unless they are already up, or E2E_BASE_URL is set.
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: isCi,
  retries: isCi ? 1 : 0,
  reporter: isCi ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: deployedUrl ?? 'http://localhost:5173',
    // Caddy's local CA when the production stack runs on a laptop (TREKKY_SITE_ADDRESS=localhost).
    ignoreHTTPSErrors: deployedUrl?.startsWith('https://localhost') ?? false,
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: deployedUrl ? [] : [
    {
      // CI builds the API in an earlier step.
      command: `dotnet run ${isCi ? '--no-build ' : ''}--project ../backend/src/Trekky.Api`,
      url: 'http://localhost:5080/api/v1/health',
      env: gitHubApp,
      reuseExistingServer: !isCi,
      timeout: 180_000,
    },
    {
      command: 'npm run dev',
      url: 'http://localhost:5173',
      reuseExistingServer: !isCi,
      timeout: 60_000,
    },
  ],
})
