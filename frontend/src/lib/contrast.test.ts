import { contrastRatio, parseOklch, withAlpha } from './contrast.ts'

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
