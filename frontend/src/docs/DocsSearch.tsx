import { useId, useState } from 'react'
import { Link } from 'react-router'
import { Input } from '../components/ui/Input.tsx'
import { messages } from '../i18n/messages.ts'
import { useDebounced } from '../lib/useDebounced.ts'
import { docRoute } from './docPaths.ts'
import { useDocSearch, type DocSnippetPart } from './useDocs.ts'

function Snippet({ parts }: { parts: DocSnippetPart[] }) {
  return (
    <p className="line-clamp-3 text-xs text-muted-foreground">
      {parts.map((part, index) =>
        part.match ? (
          <mark key={index} className="rounded bg-accent px-0.5 text-accent-foreground">
            {part.text}
          </mark>
        ) : (
          <span key={index}>{part.text}</span>
        ),
      )}
    </p>
  )
}

/** Full-text search across the project's docs, with snippets (TREK-75). Results replace nothing: they show above the tree. */
export function DocsSearch({ projectId, projectKey }: { projectId: string; projectKey: string }) {
  const [query, setQuery] = useState('')
  const debounced = useDebounced(query, 250)
  const search = useDocSearch(projectId, debounced)
  const resultsId = useId()
  const active = query.trim().length > 0

  return (
    <div role="search" className="grid gap-2">
      <Input
        type="search"
        aria-label={messages.docs.search.label}
        aria-controls={active ? resultsId : undefined}
        placeholder={messages.docs.search.placeholder}
        value={query}
        onChange={(event) => setQuery(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === 'Escape') setQuery('')
        }}
      />
      {active && (
        <section id={resultsId} aria-label={messages.docs.search.label} className="grid gap-2">
          <p role="status" className="text-xs text-muted-foreground">
            {search.isPending || search.isFetching
              ? messages.docs.search.searching
              : search.isError
                ? messages.docs.search.failed
                : search.data.results.length === 0
                  ? messages.docs.search.none(search.data.query)
                  : messages.docs.search.results(search.data.results.length)}
          </p>
          {search.data && search.data.results.length > 0 && (
            <ul className="grid gap-2">
              {search.data.results.map((hit) => (
                <li key={hit.path} className="grid gap-0.5">
                  <Link to={docRoute(projectKey, hit.path)} className="text-sm font-medium text-primary underline-offset-4 hover:underline">
                    {hit.title}
                  </Link>
                  <Snippet parts={hit.snippet} />
                </li>
              ))}
            </ul>
          )}
          {search.data?.incomplete && <p className="text-xs text-muted-foreground">{messages.docs.search.incomplete}</p>}
        </section>
      )}
    </div>
  )
}
