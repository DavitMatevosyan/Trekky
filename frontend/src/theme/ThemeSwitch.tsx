import { useId, type ReactNode } from 'react'
import { messages } from '../i18n/messages.ts'
import { THEME_CHOICES, type ThemeChoice } from './theme.ts'
import { useThemeChoice } from './ThemeContext.ts'

const icon = (children: ReactNode) => (
  <svg viewBox="0 0 16 16" className="size-4" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" aria-hidden="true">
    {children}
  </svg>
)

const ICONS: Record<ThemeChoice, ReactNode> = {
  light: icon(
    <>
      <circle cx="8" cy="8" r="3" />
      <path d="M8 1.5v1.5M8 13v1.5M1.5 8H3M13 8h1.5M3.4 3.4l1.1 1.1M11.5 11.5l1.1 1.1M3.4 12.6l1.1-1.1M11.5 4.5l1.1-1.1" />
    </>,
  ),
  dark: icon(<path d="M13.5 9.5A5.5 5.5 0 0 1 6.5 2.5a5.5 5.5 0 1 0 7 7Z" />),
  system: icon(
    <>
      <rect x="1.75" y="2.5" width="12.5" height="8.5" rx="1" />
      <path d="M5.5 14h5M8 11v3" />
    </>,
  ),
}

/**
 * Light, Dark or System (TREK-56): one group of radio buttons, so arrow keys move between them and screen readers hear
 * "Theme, Light, radio button, 1 of 3". The icons have the names as tooltips.
 */
export function ThemeSwitch() {
  const { choice, choose } = useThemeChoice()
  const name = useId()
  return (
    <fieldset className="flex gap-1 rounded-md border border-input p-1">
      <legend className="sr-only">{messages.theme.label}</legend>
      {THEME_CHOICES.map((option) => (
        <label
          key={option}
          title={messages.theme.choices[option]}
          className="flex size-6 cursor-pointer items-center justify-center rounded text-muted-foreground hover:bg-muted hover:text-foreground has-checked:bg-primary has-checked:text-primary-foreground has-focus-visible:ring-2 has-focus-visible:ring-ring"
        >
          <input type="radio" name={name} value={option} checked={choice === option} onChange={() => choose(option)} className="sr-only" />
          {ICONS[option]}
          <span className="sr-only">{messages.theme.choices[option]}</span>
        </label>
      ))}
    </fieldset>
  )
}
