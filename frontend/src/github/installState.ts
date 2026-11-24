import type { KeyValueStore } from '../platform/Platform.ts'

const STORAGE_KEY = 'trekky.github.installState'

/**
 * The `state` sent with the GitHub App install (TREK-74), so the callback can tell an install this tab started from a
 * link someone else sent. GitHub may not send it back, and the desktop app finishes in the system browser, which has
 * not seen it; then the callback asks the user to confirm instead of connecting by itself.
 */
export function startInstall(installUrl: string, store: KeyValueStore = sessionStorage): string {
  const bytes = crypto.getRandomValues(new Uint8Array(16))
  const state = Array.from(bytes, (byte) => byte.toString(16).padStart(2, '0')).join('')
  store.setItem(STORAGE_KEY, state)
  const url = new URL(installUrl)
  url.searchParams.set('state', state)
  return url.toString()
}

/** Whether `state` is the one this tab sent; the stored value is used up either way. */
export function finishInstall(state: string | null, store: KeyValueStore = sessionStorage): boolean {
  const expected = store.getItem(STORAGE_KEY)
  store.removeItem(STORAGE_KEY)
  return expected !== null && state !== null && state === expected
}
