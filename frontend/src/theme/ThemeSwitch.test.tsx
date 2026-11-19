import { act, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import themeScript from '../../public/theme.js?raw'
import { fakeApi, renderApp, validTokens } from '../test/renderApp.tsx'
import { THEME_STORAGE_KEY } from './theme.ts'

/** A stand-in for the OS dark-mode setting that can change while the page is open. */
function fakeSystemTheme(dark: boolean) {
  const listeners = new Set<() => void>()
  const query = {
    matches: dark,
    media: '(prefers-color-scheme: dark)',
    addEventListener: (_: string, listener: () => void) => listeners.add(listener),
    removeEventListener: (_: string, listener: () => void) => listeners.delete(listener),
  }
  vi.stubGlobal('matchMedia', vi.fn(() => query))
  return {
    set(next: boolean) {
      query.matches = next
      act(() => listeners.forEach((listener) => listener()))
    },
    listeners,
  }
}

const isDark = () => document.documentElement.classList.contains('dark')

function renderShell() {
  renderApp({ path: '/', tokens: validTokens, fetchFn: fakeApi() })
  return screen.findByRole('group', { name: 'Theme' })
}

describe('theme switch', () => {
  beforeEach(() => {
    window.localStorage.clear()
    document.documentElement.classList.remove('dark')
  })
  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('offers Light, Dark and System in the app bar, System by default', async () => {
    fakeSystemTheme(false)
    const group = await renderShell()

    expect(within(group).getAllByRole('radio').map((radio) => radio.closest('label')?.textContent)).toEqual(['Light', 'Dark', 'System'])
    expect(within(group).getByRole('radio', { name: 'System' })).toBeChecked()
    expect(isDark()).toBe(false)
  })

  it('switches the theme, saves the choice and moves with the arrow keys', async () => {
    fakeSystemTheme(false)
    const group = await renderShell()

    await userEvent.click(within(group).getByRole('radio', { name: 'Dark' }))
    expect(isDark()).toBe(true)
    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark')

    await userEvent.keyboard('{ArrowLeft}')
    expect(within(group).getByRole('radio', { name: 'Light' })).toBeChecked()
    expect(within(group).getByRole('radio', { name: 'Light' })).toHaveFocus()
    expect(isDark()).toBe(false)
    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe('light')
  })

  it('starts with the saved choice', async () => {
    fakeSystemTheme(false)
    window.localStorage.setItem(THEME_STORAGE_KEY, 'dark')
    const group = await renderShell()

    expect(within(group).getByRole('radio', { name: 'Dark' })).toBeChecked()
    expect(isDark()).toBe(true)
  })

  it('follows the operating system live with System, and stops once a theme is chosen', async () => {
    const system = fakeSystemTheme(false)
    const group = await renderShell()

    system.set(true)
    expect(isDark()).toBe(true)
    system.set(false)
    expect(isDark()).toBe(false)

    await userEvent.click(within(group).getByRole('radio', { name: 'Light' }))
    expect(system.listeners.size).toBe(0)
    system.set(true)
    expect(isDark()).toBe(false)
  })

  it('follows a choice made in another tab', async () => {
    fakeSystemTheme(false)
    const group = await renderShell()

    window.localStorage.setItem(THEME_STORAGE_KEY, 'dark')
    act(() => {
      window.dispatchEvent(new StorageEvent('storage', { key: THEME_STORAGE_KEY, newValue: 'dark', storageArea: window.localStorage }))
    })

    expect(within(group).getByRole('radio', { name: 'Dark' })).toBeChecked()
    expect(isDark()).toBe(true)
  })

  it('still switches when storage is blocked, and falls back to System', async () => {
    fakeSystemTheme(true)
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new DOMException('blocked', 'SecurityError')
    })
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('blocked', 'SecurityError')
    })
    const group = await renderShell()

    expect(within(group).getByRole('radio', { name: 'System' })).toBeChecked()
    expect(isDark()).toBe(true)
    await userEvent.click(within(group).getByRole('radio', { name: 'Light' }))
    expect(isDark()).toBe(false)
  })
})

describe('theme.js (before the first paint)', () => {
  const run = () => new Function(themeScript)()

  beforeEach(() => {
    window.localStorage.clear()
    document.documentElement.classList.remove('dark')
  })
  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('paints the saved theme, or the operating system one for System or nothing saved', () => {
    fakeSystemTheme(true)
    run()
    expect(isDark()).toBe(true)

    window.localStorage.setItem(THEME_STORAGE_KEY, 'light')
    run()
    expect(isDark()).toBe(false)

    window.localStorage.setItem(THEME_STORAGE_KEY, 'system')
    run()
    expect(isDark()).toBe(true)
  })

  it('ignores an unknown saved value and blocked storage', () => {
    fakeSystemTheme(false)
    window.localStorage.setItem(THEME_STORAGE_KEY, 'neon')
    run()
    expect(isDark()).toBe(false)

    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new DOMException('blocked', 'SecurityError')
    })
    run()
    expect(isDark()).toBe(false)
  })
})
