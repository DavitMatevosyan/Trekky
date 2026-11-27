import { contrastRatio, parseOklch, toHex, withAlpha } from './contrast.ts'

const colour = (value: string) => {
  const parsed = parseOklch(value)
  if (!parsed) throw new Error(`Not an oklch colour: ${value}`)
  return parsed
}

describe('contrast', () => {
  it('reads oklch with numbers or percentages, and an optional alpha', () => {
    expect(parseOklch('oklch(0.5 0.1 200)')).toEqual({ l: 0.5, c: 0.1, h: 200, alpha: 1 })
    expect(parseOklch('oklch(50% 0.1 200 / 45%)')).toEqual({ l: 0.5, c: 0.1, h: 200, alpha: 0.45 })
    expect(parseOklch('#fff')).toBeNull()
  })

  it('gives 21:1 for black on white and 1:1 for a colour on itself', () => {
    expect(contrastRatio(colour('oklch(0 0 0)'), colour('oklch(1 0 0)'))).toBeCloseTo(21, 1)
    expect(contrastRatio(colour('oklch(0.6 0.1 30)'), colour('oklch(0.6 0.1 30)'))).toBeCloseTo(1, 5)
  })

  it('matches known sRGB pairs', () => {
    // #767676 on white is the classic 4.54:1; in OKLCH it is oklch(0.5659 0 0).
    expect(contrastRatio(colour('oklch(0.5659 0 0)'), colour('oklch(1 0 0)'))).toBeCloseTo(4.54, 1)
  })

  it('paints translucent fills over the background before measuring', () => {
    const white = colour('oklch(1 0 0)')
    const black = colour('oklch(0 0 0)')
    // Black at 50% over white blends in gamma sRGB, as browsers do, to #808080: 3.98:1 against white. (A linear blend
    // would give #bcbcbc and about 1.98:1.)
    expect(contrastRatio(white, white, withAlpha(black, 0.5))).toBeCloseTo(3.98, 1)
    expect(contrastRatio(black, white, withAlpha(black, 0))).toBeCloseTo(21, 1)
  })

  it('paints a translucent colour over its background first', () => {
    const white = colour('oklch(1 0 0)')
    expect(contrastRatio(colour('oklch(0 0 0 / 0%)'), white)).toBeCloseTo(1, 5)
    expect(contrastRatio(colour('oklch(0 0 0 / 50%)'), white)).toBeLessThan(contrastRatio(colour('oklch(0 0 0)'), white))
  })
})

describe('toHex', () => {
  it('converts OKLCH to sRGB hex, clipping to the gamut', () => {
    expect(toHex({ l: 1, c: 0, h: 0, alpha: 1 })).toBe('#ffffff')
    expect(toHex({ l: 0, c: 0, h: 0, alpha: 1 })).toBe('#000000')
    // oklch(0.628 0.2577 29.23) is sRGB red.
    expect(toHex({ l: 0.628, c: 0.2577, h: 29.23, alpha: 1 })).toBe('#ff0000')
    expect(toHex({ l: 0.9, c: 0.4, h: 140, alpha: 1 })).toMatch(/^#[0-9a-f]{6}$/)
  })
})
