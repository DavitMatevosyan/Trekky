import { defineConfig, devices } from '@playwright/test'

const isCi = !!process.env.CI

/**
 * End-to-end tests against the real API and web app. Postgres must be running (`docker compose up -d`);
 * Playwright starts the API and the Vite dev server unless they are already up.
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: isCi,
  retries: isCi ? 1 : 0,
  reporter: isCi ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: 'http://localhost:5173',
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: [
    {
      // CI builds the API in an earlier step.
      command: `dotnet run ${isCi ? '--no-build ' : ''}--project ../backend/src/Trekky.Api`,
      url: 'http://localhost:5080/api/v1/health',
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
