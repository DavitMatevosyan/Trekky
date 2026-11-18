/**
 * The few W3C WebDriver calls the desktop smoke test needs, spoken to tauri-driver over HTTP, so the test has no
 * dependencies (https://www.w3.org/TR/webdriver2/).
 */

import { writeFileSync } from 'node:fs'

/** The key W3C uses for an element reference in responses. */
const ELEMENT_KEY = 'element-6066-11e4-a52e-4f735466cecf'

interface WebDriverReply<T> {
  value: T
}

export class WebDriverError extends Error {
  readonly status: number
  readonly body: string

  constructor(status: number, body: string) {
    super(`WebDriver ${status}: ${body.slice(0, 300)}`)
    this.name = 'WebDriverError'
    this.status = status
    this.body = body
  }
}

async function call<T>(driverUrl: string, method: string, path: string, body?: unknown): Promise<T> {
  const response = await fetch(`${driverUrl}${path}`, {
    method,
    headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  const text = await response.text()
  if (!response.ok) throw new WebDriverError(response.status, text)
  return (JSON.parse(text) as WebDriverReply<T>).value
}

/** One running app window, driven through tauri-driver. */
export class AppSession {
  private readonly driverUrl: string
  private readonly id: string

  private constructor(driverUrl: string, id: string) {
    this.driverUrl = driverUrl
    this.id = id
  }

  /** Starts the app at `application` (the exe) and connects to its window. */
  static async start(driverUrl: string, application: string): Promise<AppSession> {
    const created = await call<{ sessionId: string }>(driverUrl, 'POST', '/session', {
      capabilities: { alwaysMatch: { 'tauri:options': { application } } },
    })
    return new AppSession(driverUrl, created.sessionId)
  }

  /** Closes the app. */
  async end(): Promise<void> {
    await call(this.driverUrl, 'DELETE', `/session/${this.id}`).catch(() => undefined)
  }

  /** Saves a PNG of the window to `file`. */
  async screenshot(file: string): Promise<void> {
    const png = await call<string>(this.driverUrl, 'GET', `/session/${this.id}/screenshot`)
    writeFileSync(file, Buffer.from(png, 'base64'))
  }

  /** The visible text of the page. */
  async bodyText(): Promise<string> {
    return this.execute<string>('return document.body.innerText')
  }

  async execute<T>(script: string, args: unknown[] = []): Promise<T> {
    return call<T>(this.driverUrl, 'POST', `/session/${this.id}/execute/sync`, { script, args })
  }

  /** Waits until the page shows `text` (default 30 s); fails with what the page showed instead. */
  async waitForText(text: string, timeoutMs = 30_000): Promise<void> {
    const deadline = Date.now() + timeoutMs
    let last = ''
    while (Date.now() < deadline) {
      last = await this.bodyText().catch(() => '')
      if (last.includes(text)) return
      await new Promise((resolve) => setTimeout(resolve, 250))
    }
    const where = await this.execute<string>('return location.href').catch(() => '?')
    throw new Error(`Timed out waiting for "${text}" at ${where}. The page shows:\n${last}`)
  }

  /** Clicks the button whose text is exactly `name`. */
  async clickButton(name: string): Promise<void> {
    const element = await this.find(`//button[normalize-space()=${xpathString(name)}]`)
    await call(this.driverUrl, 'POST', `/session/${this.id}/element/${element}/click`, {})
  }

  /** Replaces the text of the input labelled `label`. */
  async fill(label: string, value: string): Promise<void> {
    const element = await this.find(`//input[@id=//label[normalize-space()=${xpathString(label)}]/@for]`)
    await call(this.driverUrl, 'POST', `/session/${this.id}/element/${element}/clear`, {})
    await call(this.driverUrl, 'POST', `/session/${this.id}/element/${element}/value`, { text: value })
  }

  private async find(xpath: string): Promise<string> {
    const found = await call<Record<string, string>>(this.driverUrl, 'POST', `/session/${this.id}/element`, {
      using: 'xpath',
      value: xpath,
    })
    const element = found[ELEMENT_KEY]
    if (!element) throw new Error(`No element for ${xpath}`)
    return element
  }
}

/** `value` as an XPath string literal, quotes included. */
function xpathString(value: string): string {
  if (!value.includes("'")) return `'${value}'`
  if (!value.includes('"')) return `"${value}"`
  return `concat('${value.split("'").join(`', "'", '`)}')`
}
