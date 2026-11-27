/** An OKLCH colour as CSS writes it: lightness 0–1, chroma, hue in degrees, and alpha 0–1. */
export interface Oklch {
  l: number
  c: number
  h: number
  alpha: number
}

type Rgb = [number, number, number]

const OKLCH = /^oklch\(\s*([\d.]+)(%?)\s+([\d.]+)\s+([\d.]+)\s*(?:\/\s*([\d.]+)(%?))?\s*\)$/

/** Reads `oklch(L C H)` or `oklch(L C H / A)`, with L and A as numbers or percentages; null for anything else. */
export function parseOklch(value: string): Oklch | null {
  const match = OKLCH.exec(value.trim())
  if (!match) return null
  const [, l, lPercent, c, h, alpha, alphaPercent] = match
  return {
    l: Number(l) / (lPercent ? 100 : 1),
    c: Number(c),
    h: Number(h),
    alpha: alpha === undefined ? 1 : Number(alpha) / (alphaPercent ? 100 : 1),
  }
}

/** `colour` with another opacity, as Tailwind's `bg-destructive/10` paints it. */
export function withAlpha(colour: Oklch, alpha: number): Oklch {
  return { ...colour, alpha }
}

const toLinear = (value: number) => (value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4)
const toGamma = (value: number) => (value <= 0.0031308 ? value * 12.92 : 1.055 * value ** (1 / 2.4) - 0.055)

/** Gamma-encoded sRGB channels (0–1, clipped to the gamut) of an OKLCH colour (Björn Ottosson's OKLab matrices). */
function srgb({ l, c, h }: Oklch): Rgb {
  const a = c * Math.cos((h * Math.PI) / 180)
  const b = c * Math.sin((h * Math.PI) / 180)
  const l_ = (l + 0.3963377774 * a + 0.2158037573 * b) ** 3
  const m_ = (l - 0.1055613458 * a - 0.0638541728 * b) ** 3
  const s_ = (l - 0.0894841775 * a - 1.291485548 * b) ** 3
  const channel = (value: number) => toGamma(Math.min(1, Math.max(0, value)))
  return [
    channel(4.0767416621 * l_ - 3.3077115913 * m_ + 0.2309699292 * s_),
    channel(-1.2684380046 * l_ + 2.6097574011 * m_ - 0.3413193965 * s_),
    channel(-0.0041960863 * l_ - 0.7034186147 * m_ + 1.707614701 * s_),
  ]
}

/** The colour as a `#rrggbb` sRGB hex string, clipped to the gamut, for libraries that don't read `oklch()` (Mermaid). */
export function toHex(colour: Oklch): string {
  return `#${srgb(colour)
    .map((channel) => Math.round(channel * 255).toString(16).padStart(2, '0'))
    .join('')}`
}

/** `top` painted over `bottom`; blending happens in gamma-encoded sRGB, as browsers do. */
function paint(top: Oklch, bottom: Rgb): Rgb {
  const over = srgb(top)
  return over.map((value, i) => value * top.alpha + (bottom[i] ?? 0) * (1 - top.alpha)) as Rgb
}

/** WCAG 2.x relative luminance of gamma-encoded sRGB channels. */
function luminance(rgb: Rgb): number {
  const [r, g, b] = rgb.map(toLinear) as Rgb
  return 0.2126 * r + 0.7152 * g + 0.0722 * b
}

/**
 * WCAG 2.x contrast ratio (1–21) of `foreground` on the opaque `background`, with any translucent `fills` (a tinted
 * banner, a hover) painted over the background first, in order. A translucent foreground is painted over all of it.
 */
export function contrastRatio(foreground: Oklch, background: Oklch, ...fills: Oklch[]): number {
  const under = fills.reduce((bottom, fill) => paint(fill, bottom), srgb(background))
  const fg = luminance(paint(foreground, under))
  const bg = luminance(under)
  const [lighter, darker] = fg > bg ? [fg, bg] : [bg, fg]
  return (lighter + 0.05) / (darker + 0.05)
}
