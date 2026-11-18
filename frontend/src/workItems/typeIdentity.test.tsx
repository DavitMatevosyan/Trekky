import { render, screen } from '@testing-library/react'
import css from '../index.css?raw'
import { TypeBadge } from './TypeBadge.tsx'
import { TypeIcon } from './TypeIcon.tsx'
import { messages } from '../i18n/messages.ts'
import { typeAccent, typeLabel, typeStyle, WORK_ITEM_TYPES } from './workItemTypes.ts'

/** The `--color-…` names index.css exposes to Tailwind (`@theme inline`). */
const themeColours = new Set([...css.matchAll(/--color-([\w-]+):/g)].map(([, name]) => name))

/** The colour tokens a class string uses, e.g. `border-l-type-bug-accent` → `type-bug-accent`. */
function coloursIn(classes: string): string[] {
  return [...classes.matchAll(/(?:^|\s)(?:bg|text|border|border-l)-((?:type-[\w-]+-(?:tint|accent|text))|muted-foreground|muted|border)(?=\s|$)/g)].map(
    ([, name]) => name ?? '',
  )
}

describe('type identity', () => {
  it.each(WORK_ITEM_TYPES)('%s has a label, tokens in both themes, and its own icon', (type) => {
    const style = typeStyle(type)
    expect(typeLabel(type)).toBe(messages.workItemTypes[type])
    expect(typeLabel(type).length).toBeGreaterThan(0)
    expect(style.token).not.toBeNull()
    for (const shade of ['tint', 'accent', 'text']) {
      expect(css).toContain(`--type-${style.token}-${shade}:`)
      // Defined in the light and the dark block.
      expect(css.split(`--type-${style.token}-${shade}: oklch(`)).toHaveLength(3)
    }
    for (const classes of [style.badge, style.icon, style.stripe, style.tint]) {
      const colours = coloursIn(classes)
      expect(colours.length).toBeGreaterThan(0)
      for (const colour of colours) {
        expect(themeColours, `${type}: ${colour}`).toContain(colour)
        // Its own tokens, never another type's (a copy-paste slip would pass the check above).
        expect(colour, type).toMatch(new RegExp(`^type-${style.token}-`))
      }
    }
  })

  it('gives every type a different shape and colour', () => {
    const shapes = WORK_ITEM_TYPES.map((type) => render(<TypeIcon type={type} />).container.querySelector('svg')?.innerHTML)
    expect(new Set(shapes).size).toBe(WORK_ITEM_TYPES.length)
    expect(new Set(WORK_ITEM_TYPES.map((type) => typeStyle(type).token)).size).toBe(WORK_ITEM_TYPES.length)
  })

  it('draws an unknown type in the neutral style with a plain shape', () => {
    const style = typeStyle('initiative')
    expect(style.token).toBeNull()
    expect(style.badge).toBe('bg-muted text-muted-foreground border-border')
    expect(typeAccent('initiative')).toBe('border-l-[3px] border-l-input')
    const { container } = render(<TypeIcon type="initiative" />)
    expect(container.querySelector('svg')).toHaveAttribute('data-type-icon', 'unknown')
  })

  it('makes the stripe 3px in the type accent', () => {
    expect(typeAccent('bug')).toBe('border-l-[3px] border-l-type-bug-accent')
    expect(typeAccent('tech_debt')).toBe('border-l-[3px] border-l-type-tech-debt-accent')
  })
})

describe('TypeBadge', () => {
  it('shows the icon and the label in the type colours', () => {
    render(<TypeBadge type="story" />)

    const badge = screen.getByText('Story')
    expect(badge).toHaveClass('bg-type-story-tint', 'text-type-story-text', 'border-type-story-accent')
    const icon = badge.querySelector('svg')
    expect(icon).toHaveAttribute('aria-hidden', 'true')
    expect(icon).toHaveClass('text-type-story-accent')
    // Also on its own, without a badge around it.
    expect(render(<TypeIcon type="bug" />).container.querySelector('svg')).toHaveClass('text-type-bug-accent')
    expect(icon).toHaveAttribute('data-type-icon', 'story')
  })

  it('keeps the label for screen readers and as a tooltip when compact', () => {
    const { container } = render(<TypeBadge type="tech_debt" compact />)

    const badge = container.firstElementChild
    expect(badge).toHaveAttribute('title', 'TechDebt')
    expect(screen.getByText('TechDebt')).toHaveClass('sr-only')
    expect(badge?.querySelector('svg')).toHaveAttribute('data-type-icon', 'tech_debt')
  })

  it('shows an unknown type as sent, in the neutral style', () => {
    render(<TypeBadge type="initiative" />)

    expect(screen.getByText('initiative')).toHaveClass('bg-muted', 'text-muted-foreground')
  })
})
