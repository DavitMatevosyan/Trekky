import { useEffect, useId, useState, useSyncExternalStore } from 'react'
import { messages } from '../i18n/messages.ts'
import { parseOklch, toHex } from '../lib/contrast.ts'

/** Mermaid's theme variables, each taken from a theme token (ADR-0025) and converted to hex, which Mermaid can read. */
const THEME_VARIABLES: Record<string, string> = {
  background: 'surface',
  mainBkg: 'muted',
  primaryColor: 'muted',
  primaryTextColor: 'foreground',
  primaryBorderColor: 'input',
  nodeBorder: 'input',
  secondaryColor: 'accent',
  tertiaryColor: 'surface-raised',
  lineColor: 'muted-foreground',
  textColor: 'foreground',
  noteBkgColor: 'accent',
  noteTextColor: 'accent-foreground',
  edgeLabelBackground: 'surface',
}

function subscribeToThemeClass(onChange: () => void) {
  const observer = new MutationObserver(onChange)
  observer.observe(document.documentElement, { attributes: true, attributeFilter: ['class'] })
  return () => observer.disconnect()
}

/** Whether the dark theme is painted on <html> now; follows the theme switch and the system setting live. */
function useDarkTheme(): boolean {
  return useSyncExternalStore(subscribeToThemeClass, () => document.documentElement.classList.contains('dark'))
}

function themeVariables(dark: boolean): Record<string, string | boolean> {
  const style = getComputedStyle(document.documentElement)
  const variables: Record<string, string | boolean> = { darkMode: dark, fontFamily: 'inherit' }
  for (const [name, token] of Object.entries(THEME_VARIABLES)) {
    const colour = parseOklch(style.getPropertyValue(`--${token}`).trim())
    if (colour) variables[name] = toHex(colour)
  }
  return variables
}

type DiagramState = { status: 'drawing' } | { status: 'drawn'; svg: string } | { status: 'failed' }

/**
 * A ```mermaid block of a doc, drawn as SVG (TREK-75). Mermaid is loaded only when a doc has a diagram, and runs with
 * `securityLevel: 'strict'`: labels are sanitised and click handlers in the diagram are ignored.
 */
export function MermaidDiagram({ source }: { source: string }) {
  const id = `mermaid-${useId().replace(/[^a-zA-Z0-9]/g, '')}`
  const dark = useDarkTheme()
  const [state, setState] = useState<DiagramState>({ status: 'drawing' })

  useEffect(() => {
    let cancelled = false
    import('mermaid')
      .then(async ({ default: mermaid }) => {
        mermaid.initialize({ startOnLoad: false, securityLevel: 'strict', theme: 'base', themeVariables: themeVariables(dark) })
        const { svg } = await mermaid.render(id, source)
        if (!cancelled) setState({ status: 'drawn', svg })
      })
      .catch(() => {
        if (!cancelled) setState({ status: 'failed' })
      })
    return () => {
      cancelled = true
    }
  }, [id, source, dark])

  if (state.status === 'failed') {
    return (
      <figure className="grid gap-2">
        <figcaption role="alert" className="text-sm text-destructive">
          {messages.docs.diagram.failed}
        </figcaption>
        <pre>
          <code>{source}</code>
        </pre>
      </figure>
    )
  }

  if (state.status === 'drawing') {
    return <p className="text-sm text-muted-foreground">{messages.docs.diagram.loading}</p>
  }

  return (
    <figure
      aria-label={messages.docs.diagram.label}
      className="overflow-x-auto [&_svg]:mx-auto [&_svg]:h-auto [&_svg]:max-w-full"
      // Mermaid's SVG, sanitised by Mermaid itself in strict mode.
      dangerouslySetInnerHTML={{ __html: state.svg }}
    />
  )
}
