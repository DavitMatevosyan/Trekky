import { useMemo, type MouseEvent, type ReactNode } from 'react'
import ReactMarkdown, { type Components } from 'react-markdown'
import type { ExternalLinks } from '../platform/Platform.ts'
import { externalUrl } from '../platform/externalUrl.ts'
import { usePlatform } from '../platform/PlatformContext.ts'

/**
 * A link in a user's Markdown. One that leads out of the app may be opened by the platform itself (the desktop app: the
 * system browser, TRK-23); everything else, and modified clicks (new tab, new window), is left to the browser.
 */
function ExternalLink({ href, title, links, children }: { href: string; title?: string; links: ExternalLinks; children: ReactNode }) {
  function onClick(event: MouseEvent<HTMLAnchorElement>) {
    if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return
    const url = externalUrl(href)
    if (url !== null && links.open(url)) event.preventDefault()
  }
  return (
    <a href={href} title={title} onClick={onClick}>
      {children}
    </a>
  )
}

function componentsFor(links: ExternalLinks): Components {
  return {
    a: ({ href, title, children }) =>
      typeof href === 'string' && href ? (
        <ExternalLink href={href} title={title} links={links}>
          {children}
        </ExternalLink>
      ) : (
        <>{children}</>
      ),
    // Images become links: loading them would tell an image's host who viewed the item, and when.
    img: ({ src, alt }) => (typeof src === 'string' && src ? <ExternalLink href={src} links={links}>{alt || src}</ExternalLink> : <>{alt}</>),
  }
}

/**
 * Renders a user's Markdown (US-09 AC1). react-markdown builds React elements and never injects raw HTML, and its
 * default URL check drops `javascript:` and similar links, so item descriptions cannot run script. Images are shown
 * as links, never loaded.
 */
export function Markdown({ children }: { children: string }) {
  const { links } = usePlatform()
  const components = useMemo(() => componentsFor(links), [links])
  return (
    <div className="grid gap-3 text-sm leading-6 [&_a]:text-primary [&_a]:underline [&_code]:rounded [&_code]:bg-muted [&_code]:px-1 [&_h1]:text-lg [&_h1]:font-semibold [&_h2]:font-semibold [&_h3]:font-semibold [&_ol]:list-decimal [&_ol]:pl-6 [&_pre]:overflow-x-auto [&_pre]:rounded-md [&_pre]:bg-muted [&_pre]:p-3 [&_ul]:list-disc [&_ul]:pl-6 [&_blockquote]:border-l-2 [&_blockquote]:border-border [&_blockquote]:pl-3">
      <ReactMarkdown components={components}>{children}</ReactMarkdown>
    </div>
  )
}
