const UNITS: [Intl.RelativeTimeFormatUnit, number][] = [
  ['year', 365 * 24 * 60 * 60],
  ['month', 30 * 24 * 60 * 60],
  ['day', 24 * 60 * 60],
  ['hour', 60 * 60],
  ['minute', 60],
]

/** "2 hours ago", "yesterday", "just now" (wireframes: List view, "Updated"). */
export function relativeTime(iso: string, now: Date = new Date(), locale = 'en'): string {
  const seconds = Math.round((new Date(iso).getTime() - now.getTime()) / 1000)
  const format = new Intl.RelativeTimeFormat(locale, { numeric: 'auto' })
  for (const [unit, size] of UNITS) {
    if (Math.abs(seconds) >= size) return format.format(Math.trunc(seconds / size), unit)
  }
  return format.format(0, 'second')
}

/** A full date and time for tooltips and screen readers next to a relative time. */
export function fullDateTime(iso: string, locale = 'en'): string {
  return new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(iso))
}
