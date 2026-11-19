import { useEffect, useState } from 'react'
import { applyTheme, darkModeQuery, readThemeChoice, resolveTheme, saveThemeChoice, THEME_STORAGE_KEY, type ThemeChoice } from './theme.ts'

/**
 * The user's theme choice and a way to change it (TREK-56). The choice is saved per device and painted on <html>;
 * with System, the page follows the operating system live, also when it switches while the page is open. Another tab
 * of the app that changes the choice changes it here too.
 */
export function useTheme(): [ThemeChoice, (choice: ThemeChoice) => void] {
  const [choice, setChoice] = useState(readThemeChoice)

  useEffect(() => {
    const query = darkModeQuery()
    const paint = () => applyTheme(resolveTheme(choice, query?.matches ?? false))
    paint()
    if (choice !== 'system' || !query) return
    query.addEventListener('change', paint)
    return () => query.removeEventListener('change', paint)
  }, [choice])

  useEffect(() => {
    const follow = (event: StorageEvent) => {
      if (event.storageArea !== window.localStorage) return
      if (event.key === THEME_STORAGE_KEY || event.key === null) setChoice(readThemeChoice())
    }
    window.addEventListener('storage', follow)
    return () => window.removeEventListener('storage', follow)
  }, [])

  function choose(next: ThemeChoice) {
    saveThemeChoice(next)
    setChoice(next)
  }

  return [choice, choose]
}
