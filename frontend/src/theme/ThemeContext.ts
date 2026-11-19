import { createContext, useContext } from 'react'
import type { ThemeChoice } from './theme.ts'

export interface ThemeState {
  choice: ThemeChoice
  choose: (choice: ThemeChoice) => void
}

export const ThemeContext = createContext<ThemeState | null>(null)

/** The theme choice and how to change it, from the ThemeProvider around the app. */
export function useThemeChoice(): ThemeState {
  const state = useContext(ThemeContext)
  if (!state) throw new Error('useThemeChoice needs a ThemeProvider')
  return state
}
