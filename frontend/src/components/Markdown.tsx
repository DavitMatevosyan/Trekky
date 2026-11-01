import ReactMarkdown, { type Components } from 'react-markdown'

// Images become links: loading them would tell an image's host who viewed the item, and when.
const components: Components = {
  img: ({ src, alt }) => (typeof src === 'string' && src ? <a href={src}>{alt || src}</a> : <>{alt}</>),
}

/**
 * Renders a user's Markdown (US-09 AC1). react-markdown builds React elements and never injects raw HTML, and its
 * default URL check drops `javascript:` and similar links, so item descriptions cannot run script. Images are shown
 * as links, never loaded.
 */
export function Markdown({ children }: { children: string }) {
  return (
    <div className="grid gap-3 text-sm leading-6 [&_a]:text-primary [&_a]:underline [&_code]:rounded [&_code]:bg-muted [&_code]:px-1 [&_h1]:text-lg [&_h1]:font-semibold [&_h2]:font-semibold [&_h3]:font-semibold [&_ol]:list-decimal [&_ol]:pl-6 [&_pre]:overflow-x-auto [&_pre]:rounded-md [&_pre]:bg-muted [&_pre]:p-3 [&_ul]:list-disc [&_ul]:pl-6 [&_blockquote]:border-l-2 [&_blockquote]:border-border [&_blockquote]:pl-3">
      <ReactMarkdown components={components}>{children}</ReactMarkdown>
    </div>
  )
}
