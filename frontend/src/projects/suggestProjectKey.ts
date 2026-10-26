/** Same rule as the API (US-05 AC1): an uppercase letter, then 1–9 uppercase letters or digits. */
export const PROJECT_KEY_PATTERN = /^[A-Z][A-Z0-9]{1,9}$/

const MAX_KEY_LENGTH = 10
const SUGGESTED_LENGTH = 3

/**
 * Suggests a project key from its name (US-05 AC2): the initials of a multi-word name ("Website Redesign" → `WR`),
 * or the first letter plus the next consonants of a single word ("Trekky" → `TRK`). Returns '' when the name has
 * no letters; the user can always type their own key.
 */
export function suggestProjectKey(name: string): string {
  const words = name
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toUpperCase()
    .split(/[^A-Z0-9]+/)
    .filter(Boolean)
  const firstLetterIndex = words.findIndex((word) => /^[A-Z]/.test(word))
  if (firstLetterIndex === -1) return ''
  const usable = words.slice(firstLetterIndex)
  const first = usable[0] ?? ''

  if (usable.length > 1) {
    const initials = usable.map((word) => word[0]).join('').slice(0, MAX_KEY_LENGTH)
    return initials.length >= 2 ? initials : padFrom(initials, first)
  }

  const consonants = first.slice(1).replace(/[AEIOUY]/g, '')
  const key = (first[0] + consonants).slice(0, SUGGESTED_LENGTH)
  return key.length >= 2 ? key : padFrom(key, first)
}

/** Too short for a key: take the word's next characters instead. */
function padFrom(key: string, word: string): string {
  const padded = (key + word.slice(key.length)).slice(0, SUGGESTED_LENGTH)
  return PROJECT_KEY_PATTERN.test(padded) ? padded : ''
}
