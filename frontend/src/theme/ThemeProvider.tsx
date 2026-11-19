import type { ReactNode } from 'react'
import { ThemeContext } from './ThemeContext.ts'
import { useTheme } from './useTheme.ts'

/** Paints the chosen theme on every screen, the desktop app's own included, and shares the choice with the switch. */
export function ThemeProvider({ children }: { children: ReactNode }) {
  const [choice, choose] = useTheme()
  return <ThemeContext.Provider value={{ choice, choose }}>{children}</ThemeContext.Provider>
}
