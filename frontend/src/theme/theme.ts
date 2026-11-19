/** What the user picked (TREK-56): a theme, or whatever the operating system uses. */
export type ThemeChoice = 'light' | 'dark' | 'system'

export const THEME_CHOICES: readonly ThemeChoice[] = ['light', 'dark', 'system']

/** Per device. public/theme.js reads the same key before the page paints. */
export const THEME_STORAGE_KEY = 'trekky.theme'

const DARK_QUERY = '(prefers-color-scheme: dark)'

function isThemeChoice(value: unknown): value is ThemeChoice {
  return typeof value === 'string' && (THEME_CHOICES as readonly string[]).includes(value)
}

/** The saved choice; System when none is saved or storage cannot be read. */
export function readThemeChoice(): ThemeChoice {
  try {
    const saved = window.localStorage.getItem(THEME_STORAGE_KEY)
    return isThemeChoice(saved) ? saved : 'system'
  } catch {
    return 'system'
  }
}

/** Saves the choice for this device; a choice that cannot be saved still applies until the page closes. */
export function saveThemeChoice(choice: ThemeChoice): void {
  try {
    window.localStorage.setItem(THEME_STORAGE_KEY, choice)
  } catch {
    // Storage blocked: nothing to keep it in.
  }
}

/** The operating system's dark-mode query, where the browser has one. */
export function darkModeQuery(): MediaQueryList | null {
  return typeof window.matchMedia === 'function' ? window.matchMedia(DARK_QUERY) : null
}

/** Light or dark for a choice: System follows the operating system. */
export function resolveTheme(choice: ThemeChoice, systemDark: boolean): 'light' | 'dark' {
  return choice === 'system' ? (systemDark ? 'dark' : 'light') : choice
}

/** Paints the page in a theme: the tokens in index.css switch on the `dark` class of <html>. */
export function applyTheme(theme: 'light' | 'dark'): void {
  document.documentElement.classList.toggle('dark', theme === 'dark')
}
