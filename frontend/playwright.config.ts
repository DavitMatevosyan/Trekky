import { generateKeyPairSync } from 'node:crypto'
import { defineConfig, devices } from '@playwright/test'
import { FAKE_GITHUB_CLIENT_ID, FAKE_GITHUB_CLIENT_SECRET, FAKE_GITHUB_PORT, FAKE_GITHUB_SLUG } from './e2e/support/fakeGitHub.ts'

const isCi = !!process.env.CI
/** Set to test a deployed stack instead, e.g. E2E_BASE_URL=https://localhost:8443 (deploy/README.md). */
const deployedUrl = process.env.E2E_BASE_URL

/**
 * The API started here talks to the fake GitHub that e2e/docs.spec.ts runs (TREK-75), as a GitHub App with a key made
 * for this run. The fake doesn't check the key; the .NET tests do.
 */
const fakeGitHub = `http://127.0.0.1:${FAKE_GITHUB_PORT}/`
const gitHubApp = {
  GitHub__ClientId: FAKE_GITHUB_CLIENT_ID,
  GitHub__ClientSecret: FAKE_GITHUB_CLIENT_SECRET,
  GitHub__AppSlug: FAKE_GITHUB_SLUG,
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
