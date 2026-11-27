import { useMemo, type ReactNode } from 'react'
import ReactMarkdown, { type Components, type Options } from 'react-markdown'
import { Link } from 'react-router'
import rehypeHighlight from 'rehype-highlight'
import rehypeRaw from 'rehype-raw'
import rehypeSanitize, { defaultSchema } from 'rehype-sanitize'
import rehypeSlug from 'rehype-slug'
import remarkGfm from 'remark-gfm'
import { ExternalLink } from '../components/Markdown.tsx'
import type { ExternalLinks } from '../platform/Platform.ts'
import { usePlatform } from '../platform/PlatformContext.ts'
import { findAnchor } from './anchors.ts'
import { DocImage } from './DocImage.tsx'
import { docRoute, resolveDocTarget, type DocsLocation } from './docPaths.ts'
import { MermaidDiagram } from './MermaidDiagram.tsx'

/** The parts of a hast node the renderer reads (react-markdown passes the node behind each element). */
interface HastNode {
  type: string
  tagName?: string
  value?: string
  properties?: { className?: unknown }
  children?: HastNode[]
}

function textOf(node: HastNode): string {
  return node.type === 'text' ? (node.value ?? '') : (node.children ?? []).map(textOf).join('')
}

/** The `mermaid` code block inside a `<pre>`, if that is what it holds. */
function mermaidCode(pre: HastNode | undefined): HastNode | null {
  const code = pre?.children?.find((child) => child.type === 'element')
  const classes = code?.properties?.className
  return code?.tagName === 'code' && Array.isArray(classes) && classes.includes('language-mermaid') ? code : null
}

/**
 * Plugins, in order: GitHub-flavoured Markdown (tables, task lists, strikethrough, autolinks, footnotes); raw HTML
 * parsed, then sanitised with GitHub's own allow-list (no scripts, styles, event handlers or `javascript:` URLs);
 * heading anchors; then code highlighting, which adds only its own classes after the sanitiser has run.
 */
const remarkPlugins: Options['remarkPlugins'] = [remarkGfm]
const rehypePlugins: Options['rehypePlugins'] = [
  rehypeRaw,
  [rehypeSanitize, defaultSchema],
  rehypeSlug,
  [rehypeHighlight, { detect: false, plainText: ['mermaid'] }],
]

interface DocContext {
  projectId: string
  projectKey: string
  docPath: string
  location: DocsLocation
  links: ExternalLinks
}

function componentsFor({ projectId, projectKey, docPath, location, links }: DocContext): Components {
  function linkTo(href: string, title: string | undefined, children: ReactNode) {
    const target = resolveDocTarget(href, docPath, location, 'link')
    switch (target.kind) {
      case 'anchor':
        return (
          <a
            href={`#${target.hash}`}
            title={title}
            onClick={(event) => {
              // Modified clicks (new tab, new window) are the browser's.
              if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return
              // Footnotes and HTML anchors carry the sanitiser's prefix; find either form and keep the URL's hash.
              const element = findAnchor(target.hash)
              if (!element) return
              event.preventDefault()
              element.scrollIntoView()
              window.history.replaceState(window.history.state, '', `#${target.hash}`)
            }}
          >
            {children}
          </a>
        )
      case 'doc':
        return (
          <Link to={docRoute(projectKey, target.path, target.hash)} title={title}>
            {children}
          </Link>
        )
      case 'repository':
      case 'external':
        return (
          <ExternalLink href={target.url} title={title} links={links}>
            {children}
          </ExternalLink>
        )
      default:
        return <>{children}</>
    }
  }

  return {
    a: ({ href, title, children }) => (typeof href === 'string' && href ? linkTo(href, title, children) : <>{children}</>),
    img: ({ src, alt, title }) => {
      if (typeof src !== 'string' || !src) return <>{alt}</>
      const target = resolveDocTarget(src, docPath, location, 'image')
      if (target.kind === 'image') return <DocImage projectId={projectId} path={target.path} alt={alt ?? ''} title={title} />
      // Images from elsewhere are shown as links, never loaded: loading them would tell their host who read the doc.
      if (target.kind === 'repository' || target.kind === 'external') {
        return (
          <ExternalLink href={target.url} title={title} links={links}>
            {alt || src}
          </ExternalLink>
        )
      }
      return <>{alt}</>
    },
    // <picture><source srcset> would make the browser load an image from anywhere; <img> inside it still renders.
    source: () => null,
    pre: ({ node, children }) => {
      const mermaid = mermaidCode(node as HastNode | undefined)
      return mermaid ? <MermaidDiagram source={textOf(mermaid)} /> : <pre>{children}</pre>
    },
  }
}

/** Typography for rendered docs, from theme tokens only (ADR-0025). */
const DOC_CLASSES = [
  'grid gap-4 leading-7 text-foreground',
  '[&_a]:text-primary [&_a]:underline [&_a]:underline-offset-2',
  '[&_h1]:text-2xl [&_h1]:font-semibold [&_h2]:mt-2 [&_h2]:border-b [&_h2]:border-border [&_h2]:pb-1 [&_h2]:text-xl [&_h2]:font-semibold',
  '[&_h3]:text-lg [&_h3]:font-semibold [&_h4]:font-semibold [&_h1]:scroll-mt-4 [&_h2]:scroll-mt-4 [&_h3]:scroll-mt-4',
  '[&_ul]:list-disc [&_ul]:pl-6 [&_ol]:list-decimal [&_ol]:pl-6 [&_li]:my-1',
  '[&_ul.contains-task-list]:list-none [&_ul.contains-task-list]:pl-2 [&_li.task-list-item_input]:mr-2',
  '[&_blockquote]:border-l-4 [&_blockquote]:border-border [&_blockquote]:pl-4 [&_blockquote]:text-muted-foreground',
  '[&_code]:rounded [&_code]:bg-muted [&_code]:px-1 [&_code]:font-mono [&_code]:text-sm',
  '[&_pre]:overflow-x-auto [&_pre]:rounded-md [&_pre]:bg-muted [&_pre]:p-4 [&_pre_code]:bg-transparent [&_pre_code]:p-0',
  '[&_table]:block [&_table]:max-w-full [&_table]:overflow-x-auto [&_table]:border-collapse',
  '[&_th]:border [&_th]:border-border [&_th]:bg-muted [&_th]:px-3 [&_th]:py-1 [&_th]:text-left [&_th]:font-semibold',
  '[&_td]:border [&_td]:border-border [&_td]:px-3 [&_td]:py-1',
  '[&_hr]:border-border [&_img]:max-w-full [&_details]:rounded-md [&_details]:border [&_details]:border-border [&_details]:p-3',
  '[&_summary]:cursor-pointer [&_summary]:font-medium',
].join(' ')

interface DocMarkdownProps {
  projectId: string
  projectKey: string
  /** The doc's path inside the docs folder, which relative links and images are resolved from. */
  docPath: string
  location: DocsLocation
  children: string
}

/**
 * A doc rendered as GitHub renders it (TREK-75): GitHub-flavoured Markdown, sanitised HTML, highlighted code and Mermaid
 * diagrams. Links to other docs open inside Trekky, other repository files on GitHub; images in the docs folder load
 * through the API.
 */
export function DocMarkdown({ projectId, projectKey, docPath, location, children }: DocMarkdownProps) {
  const { links } = usePlatform()
  const components = useMemo(
    () => componentsFor({ projectId, projectKey, docPath, location, links }),
    [projectId, projectKey, docPath, location, links],
  )
  return (
    <div className={DOC_CLASSES}>
      <ReactMarkdown remarkPlugins={remarkPlugins} rehypePlugins={rehypePlugins} components={components}>
        {children}
      </ReactMarkdown>
    </div>
  )
}
