/**
 * Smoke e2e for Trekky for Windows (TREK-35 / TRK-26, ADR-0019): the real debug app, driven by tauri-driver, against
 * the local stack. The test plays the system browser itself: the debug build writes the address it would open to
 * TREKKY_OPEN_URL_LOG, and the test logs in to the server over HTTP and opens the trekky:// link the way Windows does,
 * by starting a second trekky-desktop.exe with it.
 *
 * Needs: the API and the web dev server running (`npm run dev` in frontend proxies the API, default
 * http://localhost:5173), `npm run build:debug`, tauri-driver on PATH (`cargo install tauri-driver --locked`) and
 * msedgedriver matching the WebView2 runtime in MSEDGEDRIVER. See desktop/README.md.
 *
 * It replaces the app's settings for the run and restores them afterwards; the Credential Manager entry for the test
 * server is removed. Each run creates one user `e2e-desktop-*@example.com`, like the web e2e.
 */
import assert from 'node:assert/strict'
import { execFileSync, spawn, type ChildProcess } from 'node:child_process'
import { existsSync, readFileSync, renameSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join, resolve } from 'node:path'
import { after, before, test } from 'node:test'
import { fileURLToPath } from 'node:url'
import { AppSession } from './webdriver.ts'

const here = dirname(fileURLToPath(import.meta.url))
const SERVER = process.env.TREKKY_E2E_SERVER ?? 'http://localhost:5173'
const APP = resolve(here, '../src-tauri/target/debug/trekky-desktop.exe')
const DRIVER_PORT = 4444
const DRIVER_URL = `http://127.0.0.1:${DRIVER_PORT}`
const URL_LOG = join(tmpdir(), `trekky-e2e-open-urls-${process.pid}.txt`)
const CONFIG_DIR = join(process.env.APPDATA ?? '', 'app.trekky.desktop')
const CONFIG_BACKUP = `${CONFIG_DIR}.e2e-backup`
/** The Credential Manager entry the app keeps for SERVER (keyring: `<user>.<service>`). */
const CREDENTIAL = `${SERVER}.Trekky`
const PASSWORD = 'correct horse battery staple'

let driver: ChildProcess | undefined
let sessionCookie = ''

before(async () => {
  assert.ok(existsSync(APP), `Build the app first: npm run build:debug (${APP} is missing)`)
  const health = await fetch(`${SERVER}/api/v1/health`).catch(() => null)
  assert.ok(health?.ok, `The server does not answer at ${SERVER}; start the API and the web dev server`)

  // A fresh first run: the app's settings aside, no stored token for the test server.
  if (existsSync(CONFIG_DIR)) renameSync(CONFIG_DIR, CONFIG_BACKUP)
  removeCredential()

  // The account, and a browser session for it (sign-up logs in, as in the web app).
  const email = `e2e-desktop-${Date.now()}@example.com`
  const signUp = await fetch(`${SERVER}/api/v1/auth/sign-up`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password: PASSWORD, organizationName: 'E2E Desktop Org' }),
  })
  assert.equal(signUp.status, 201, await signUp.text())
  sessionCookie = (signUp.headers.getSetCookie().find((cookie) => cookie.startsWith('trekky.session=')) ?? '').split(';')[0] ?? ''
  assert.ok(sessionCookie, 'sign-up set no session cookie')

  driver = spawn('tauri-driver', ['--port', String(DRIVER_PORT), ...nativeDriverArgs()], {
    env: { ...process.env, TREKKY_OPEN_URL_LOG: URL_LOG },
    stdio: 'ignore',
  })
  await waitForDriver()
})

after(() => {
  driver?.kill()
  rmSync(URL_LOG, { force: true })
  removeCredential()
  rmSync(CONFIG_DIR, { recursive: true, force: true })
  if (existsSync(CONFIG_BACKUP)) renameSync(CONFIG_BACKUP, CONFIG_DIR)
})

test('first run: pick the server, sign in through the browser, start signed in next time, log out', async () => {
  const app = await AppSession.start(DRIVER_URL, APP)
  try {
    // First run: the server screen, then the desktop login page.
    await app.waitForText('Connect to your Trekky server')
    await app.fill('Server address', SERVER)
    await app.clickButton('Connect')
    await app.waitForText('Log in with your browser')

    // "Log in with your browser" opens the authorization request for trekky-desktop.
    await app.clickButton('Log in with your browser')
    const authorize = new URL(await openedUrl())
    assert.equal(authorize.origin + authorize.pathname, `${SERVER}/connect/authorize`)
    assert.equal(authorize.searchParams.get('client_id'), 'trekky-desktop')
    assert.equal(authorize.searchParams.get('redirect_uri'), 'trekky://auth/callback')
    await app.waitForText('Finish in your browser')

    // The browser (this test) has a session, so the server redirects straight to the app's deep link.
    const answer = await fetch(authorize, { headers: { Cookie: sessionCookie }, redirect: 'manual' })
    assert.equal(answer.status, 302)
    const callback = answer.headers.get('location') ?? ''
    assert.match(callback, /^trekky:\/\/auth\/callback\?code=/)

    // Windows opens trekky:// by starting a second instance with the link; the open window takes it over.
    await runToExit(APP, [callback])
    await app.waitForText('You are the Owner of E2E Desktop Org.')
    assert.ok(hasCredential(), 'the refresh token was not stored in Credential Manager')
  } finally {
    await app.end()
  }

  // Next start: signed in from Credential Manager, without the browser.
  const linesBefore = openedUrls().length
  const again = await AppSession.start(DRIVER_URL, APP)
  try {
    await again.waitForText('You are the Owner of E2E Desktop Org.')
    assert.equal(openedUrls().length, linesBefore, 'the app opened the browser although it had a stored token')

    // Log out: back to the login page, and the stored token is gone.
    await again.clickButton('Log out')
    await again.waitForText('Log in with your browser')
    assert.equal(hasCredential(), false, 'the refresh token is still in Credential Manager after logout')
  } finally {
    await again.end()
  }
})

function nativeDriverArgs(): string[] {
  const native = process.env.MSEDGEDRIVER
  return native ? ['--native-driver', native] : []
}

async function waitForDriver(): Promise<void> {
  const deadline = Date.now() + 15_000
  while (Date.now() < deadline) {
    const status = await fetch(`${DRIVER_URL}/status`).catch(() => null)
    if (status?.ok) return
    await new Promise((resolve) => setTimeout(resolve, 200))
  }
  throw new Error('tauri-driver did not start; is it on PATH, and MSEDGEDRIVER set?')
}

function openedUrls(): string[] {
  return existsSync(URL_LOG) ? readFileSync(URL_LOG, 'utf8').split(/\r?\n/).filter(Boolean) : []
}

/** The last address the app asked the browser to open, waiting up to 10 s for it. */
async function openedUrl(): Promise<string> {
  const deadline = Date.now() + 10_000
  while (Date.now() < deadline) {
    const urls = openedUrls()
    const last = urls[urls.length - 1]
    if (last) return last
    await new Promise((resolve) => setTimeout(resolve, 200))
  }
  throw new Error('The app opened no address in the browser')
}

function runToExit(file: string, args: string[]): Promise<void> {
  return new Promise((resolvePromise, reject) => {
    const child = spawn(file, args, { stdio: 'ignore' })
    const timer = setTimeout(() => {
      child.kill()
      reject(new Error('The second instance did not exit; the single-instance lock may not have caught it'))
    }, 15_000)
    child.on('exit', () => {
      clearTimeout(timer)
      resolvePromise()
    })
    child.on('error', reject)
  })
}

/** cmdkey repeats the filter in its heading, so only a `Target:` line means the entry exists. */
function hasCredential(): boolean {
  const listing = execFileSync('cmdkey', [`/list:${CREDENTIAL}`], { encoding: 'utf8' })
  return listing.split(/\r?\n/).some((line) => line.trim().startsWith('Target:') && line.includes(CREDENTIAL))
}

function removeCredential(): void {
  try {
    execFileSync('cmdkey', [`/delete:${CREDENTIAL}`], { stdio: 'ignore' })
  } catch {
    // Nothing stored.
  }
}
