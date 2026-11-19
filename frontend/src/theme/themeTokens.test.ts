import css from '../index.css?raw'
import { contrastRatio, parseOklch, withAlpha, type Oklch } from '../lib/contrast.ts'
import { typeStyle, WORK_ITEM_TYPES } from '../workItems/workItemTypes.ts'

/** The token name of each type (`tech-debt` for `tech_debt`). */
const TYPE_TOKENS = WORK_ITEM_TYPES.map((type) => typeStyle(type).token ?? type)

/**
 * The `--name: oklch(…)` tokens declared in the CSS block that starts with `selector {`. A token in any other form
 * fails, so no colour can slip past the contrast checks.
 */
function tokens(selector: string): Map<string, Oklch> {
  const start = css.indexOf(`${selector} {`)
  if (start === -1) throw new Error(`No ${selector} block in index.css`)
  const block = css.slice(start, css.indexOf('}', start))
  const found = new Map<string, Oklch>()
  for (const [, name, value] of block.matchAll(/--([\w-]+):\s*([^;]+);/g)) {
    if (!name || !value) continue
    const colour = parseOklch(value)
    if (!colour) throw new Error(`--${name} in ${selector} is not oklch(L C H): ${value}`)
    found.set(name, colour)
  }
  return found
}

const themes = { light: tokens(':root'), dark: tokens(':root.dark') }

/** WCAG AA: 4.5:1 for text (1.4.3), 3:1 for input borders and the focus ring against what they sit on (1.4.11). */
const TEXT = 4.5
const UI = 3
const surfaces = ['background', 'surface', 'surface-raised']
/** A translucent layer between the background and the foreground: a token at an opacity (`bg-destructive/10`). */
type Fill = [token: string, alpha: number]
type Pair = [foreground: string, background: string, minimum: number, fill?: Fill]

/** Every pair the app paints, with where (grep the class names to find more when adding one). */
const pairs: Pair[] = [
  ...[...surfaces, 'muted'].flatMap((bg): Pair[] => [
    // Body text, hints and labels.
    ['foreground', bg, TEXT],
    ['muted-foreground', bg, TEXT],
    // Links (text-primary), e.g. on the sign-in pages and in Markdown comments.
    ['primary', bg, TEXT],
  ]),
  ...surfaces.flatMap((bg): Pair[] => [
    // Field errors (text-destructive), and the error banners on a destructive/10 tint (FormError, comments).
    ['destructive', bg, TEXT],
    ['destructive', bg, TEXT, ['destructive', 0.1]],
    // Input borders and the focus ring (1.4.11).
    ['input', bg, UI],
    ['ring', bg, UI],
    // A primary button, a pressed toggle and a ticked checkbox stand out by their fill.
    ['primary', bg, UI],
  ]),
  // The active option's outline in the parent and epic pickers.
  ['ring', 'muted', UI],
  ['accent-foreground', 'accent', TEXT],
  ['primary-foreground', 'primary', TEXT],
  // A pressed filter toggle while hovered (bg-primary/90 over the page).
  ['primary-foreground', 'background', TEXT, ['primary', 0.9]],
  // Work item types (TREK-57): badge text on its tint, the icon on the tint, and the stripe and icon against every panel.
  ...TYPE_TOKENS.flatMap((type): Pair[] => [
    [`type-${type}-text`, `type-${type}-tint`, TEXT],
    [`type-${type}-accent`, `type-${type}-tint`, UI],
    ...surfaces.map((bg): Pair => [`type-${type}-accent`, bg, UI]),
    // A tinted row or card (an Epic, TREK-58): its text, key link and errors sit on the tint.
    ...['foreground', 'muted-foreground', 'primary', 'destructive'].map((fg): Pair => [fg, `type-${type}-tint`, TEXT]),
    // The stripe on a row while hovered (hover:bg-muted).
    [`type-${type}-accent`, 'muted', UI],
  ]),
]

describe('theme tokens', () => {
  it.each(Object.entries(themes))('%s defines every token in the same set', (_, theme) => {
    expect([...theme.keys()].sort()).toEqual([...themes.light.keys()].sort())
    // 14 base tokens (TREK-56) and three shades for each of the six types (TREK-57).
    expect(theme.size).toBe(14 + 3 * TYPE_TOKENS.length)
  })

  for (const [name, theme] of Object.entries(themes)) {
    it.each(pairs)(`${name}: %s on %s meets %d:1 (fill %j)`, (fg, bg, minimum, fill) => {
      const get = (token: string) => {
        const colour = theme.get(token)
        if (!colour) throw new Error(`Missing --${token}`)
        return colour
      }
      const fills = fill ? [withAlpha(get(fill[0]), fill[1])] : []
      expect(contrastRatio(get(fg), get(bg), ...fills)).toBeGreaterThanOrEqual(minimum)
    })
  }

  it('keeps the light theme beige and the dark theme warm, never pure grey, white or black', () => {
    for (const theme of Object.values(themes)) {
      for (const name of ['background', 'surface', 'foreground']) {
        const colour = theme.get(name)
        expect(colour?.c).toBeGreaterThan(0.005)
        expect(colour?.h).toBeGreaterThanOrEqual(55)
        expect(colour?.h).toBeLessThanOrEqual(90)
      }
    }
    expect(themes.dark.get('background')?.l).toBeGreaterThan(0.1)
  })

  it('raises each panel a step above the one below it', () => {
    for (const [name, theme] of Object.entries(themes)) {
      const [background, surface, raised] = surfaces.map((token) => theme.get(token)?.l ?? Number.NaN)
      expect(surface, name).toBeGreaterThan(background ?? Number.NaN)
      expect(raised, name).toBeGreaterThan(surface ?? Number.NaN)
    }
  })
})
