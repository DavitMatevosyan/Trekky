import { lazy, Suspense } from 'react'
import { ErrorBoundary } from './ErrorBoundary.tsx'

// The Markdown renderer is most of a bundle's weight, and only the item page needs it.
const Markdown = lazy(() => import('./Markdown.tsx').then((module) => ({ default: module.Markdown })))

/** A user's Markdown, rendered once the renderer has loaded; until then, or if it fails to load, plain text. */
export function MarkdownText({ children }: { children: string }) {
  const plain = <p className="text-sm whitespace-pre-wrap break-words">{children}</p>
  return (
    <ErrorBoundary fallback={plain}>
      <Suspense fallback={plain}>
        <Markdown>{children}</Markdown>
      </Suspense>
    </ErrorBoundary>
  )
}
