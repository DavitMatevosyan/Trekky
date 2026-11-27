import { useEffect, useMemo, useState } from 'react'
import { Link, useLocation, useParams } from 'react-router'
import { ProblemError } from '../api/problem.ts'
import { usePermissions } from '../auth/usePermissions.ts'
import { RelativeTime } from '../components/RelativeTime.tsx'
import { Button } from '../components/ui/Button.tsx'
import { findAnchor } from '../docs/anchors.ts'
import { DocMarkdown } from '../docs/DocMarkdown.tsx'
import { buildDocTree, defaultDoc, docRoute, type DocsLocation } from '../docs/docPaths.ts'
import { DocsSearch } from '../docs/DocsSearch.tsx'
import { DocsTreeNav } from '../docs/DocsTreeNav.tsx'
import { useDoc, useDocsTree, useRefreshDocs, type DocCommit } from '../docs/useDocs.ts'
import { messages } from '../i18n/messages.ts'
import { ProjectHeader } from '../projects/ProjectHeader.tsx'
import { ProjectLoader } from '../projects/ProjectLoader.tsx'
import type { Project } from '../projects/useProjects.ts'

function statusOf(error: Error): number | null {
  return error instanceof ProblemError ? (error.problem.status ?? null) : null
}

/** Why the docs cannot be shown, from the API's answer (TREK-75). */
function DocsUnavailable({ error, projectKey }: { error: Error; projectKey: string }) {
  const can = usePermissions()
  switch (statusOf(error)) {
    case 404:
      return (
        <div className="grid gap-2">
          <p>{messages.docs.notLinked}</p>
          {can.manageProjects ? (
            <Link to={`/${projectKey}/settings`} className="text-primary underline underline-offset-4">
              {messages.docs.linkInSettings}
            </Link>
          ) : (
            <p className="text-sm text-muted-foreground">{messages.docs.askAdmin}</p>
          )}
        </div>
      )
    case 503:
      return <p role="alert">{messages.docs.notConfigured}</p>
    case 409:
      return <p role="alert">{messages.docs.installationGone}</p>
    case 502:
      return <p role="alert">{messages.docs.unavailable}</p>
    default:
      return <p role="alert">{messages.common.unexpectedError}</p>
  }
}

function LastChange({ commit, folder = false }: { commit: DocCommit; folder?: boolean }) {
  return (
    <p className="text-sm text-muted-foreground">
      {folder ? messages.docs.lastFolderChange(commit.author) : messages.docs.lastChange(commit.author)}{' '}
      <a href={commit.htmlUrl} className="underline underline-offset-4" title={commit.message} target="_blank" rel="noreferrer">
        <RelativeTime value={commit.committedAt} />
      </a>
    </p>
  )
}

function Breadcrumb({ projectKey, path }: { projectKey: string; path: string }) {
  const segments = path.split('/')
  return (
    <nav aria-label={messages.docs.breadcrumb}>
      <ol className="flex flex-wrap items-center gap-1 text-sm text-muted-foreground">
        <li>
          <Link to={`/${projectKey}/docs`} className="underline-offset-4 hover:underline">
            {messages.docs.tab}
          </Link>
        </li>
        {segments.map((segment, index) => (
          <li key={index} className="flex items-center gap-1">
            <span aria-hidden="true">/</span>
            <span aria-current={index === segments.length - 1 ? 'page' : undefined} className={index === segments.length - 1 ? 'text-foreground' : undefined}>
              {segment}
            </span>
          </li>
        ))}
      </ol>
    </nav>
  )
}

/** One doc: breadcrumb, last change, a link to GitHub, and its rendered Markdown. */
function DocView({ project, path, location }: { project: Project; path: string; location: DocsLocation }) {
  const doc = useDoc(project.id, path)
  const { hash } = useLocation()

  // A link to a heading (#install) scrolls to it once the doc is drawn.
  useEffect(() => {
    if (!doc.data || !hash) return
    findAnchor(hash)?.scrollIntoView()
  }, [doc.data, hash])

  if (doc.isPending) return <p className="text-muted-foreground">{messages.docs.loadingDoc}</p>
  if (doc.isError) {
    const status = statusOf(doc.error)
    if (status === 404) return <p role="alert">{messages.docs.docNotFound}</p>
    if (status === 400) return <p role="alert">{messages.docs.badPath}</p>
    return <DocsUnavailable error={doc.error} projectKey={project.key} />
  }

  const data = doc.data
  return (
    <article className="grid min-w-0 content-start gap-4">
      <div className="grid gap-1">
        <Breadcrumb projectKey={project.key} path={data.path} />
        <div className="flex flex-wrap items-center justify-between gap-2">
          {data.lastCommit ? <LastChange commit={data.lastCommit} /> : <span />}
          <a href={data.htmlUrl} target="_blank" rel="noreferrer" className="text-sm text-primary underline underline-offset-4">
            {messages.docs.openOnGitHub}
          </a>
        </div>
      </div>
      {data.stale && (
        <p role="status" className="rounded-md border border-border bg-muted px-3 py-2 text-sm">
          {messages.docs.stale}
        </p>
      )}
      {data.tooLarge || data.content === null ? (
        <p>{messages.docs.tooLarge}</p>
      ) : (
        <DocMarkdown projectId={project.id} projectKey={project.key} docPath={data.path} location={location}>
          {data.content}
        </DocMarkdown>
      )}
    </article>
  )
}

function DocsView({ project, path }: { project: Project; path: string | null }) {
  const tree = useDocsTree(project.id)
  const refresh = useRefreshDocs(project.id)
  const [announcement, setAnnouncement] = useState('')
  const files = tree.data?.files
  const nodes = useMemo(() => buildDocTree(files ?? []), [files])
  const location = useMemo<DocsLocation | null>(
    () => (tree.data ? { repository: tree.data.repository, branch: tree.data.branch, docsPath: tree.data.docsPath } : null),
    [tree.data],
  )

  const action = tree.data && (
    <Button
      variant="outline"
      onClick={() => {
        setAnnouncement('')
        refresh.mutate(undefined, { onSuccess: () => setAnnouncement(messages.docs.refreshed) })
      }}
      disabled={refresh.isPending}
    >
      {refresh.isPending ? messages.docs.refreshing : messages.docs.refresh}
    </Button>
  )

  let body
  if (tree.isPending) {
    body = <p className="text-muted-foreground">{messages.docs.loading}</p>
  } else if (tree.isError) {
    body = <DocsUnavailable error={tree.error} projectKey={project.key} />
  } else {
    const data = tree.data
    const current = path ?? defaultDoc(data.files)
    body = (
      <div className="grid gap-4">
        <div className="flex flex-wrap items-center justify-between gap-2 text-sm text-muted-foreground">
          <span>{messages.docs.source(data.repository, data.branch, data.docsPath)}</span>
          <a href={data.htmlUrl} target="_blank" rel="noreferrer" className="text-primary underline underline-offset-4">
            {messages.docs.openOnGitHub}
          </a>
        </div>
        {data.stale && (
          <p role="status" className="rounded-md border border-border bg-muted px-3 py-2 text-sm">
            {messages.docs.stale}
          </p>
        )}
        {refresh.isError && <DocsUnavailable error={refresh.error} projectKey={project.key} />}
        {data.folderMissing ? (
          <p role="alert">{messages.docs.folderMissing}</p>
        ) : (
          <div className="grid gap-6 md:grid-cols-[16rem_minmax(0,1fr)]">
            <aside className="grid content-start gap-4">
              <DocsSearch projectId={project.id} projectKey={project.key} />
              {data.files.length === 0 ? (
                <p className="text-sm text-muted-foreground">{messages.docs.empty}</p>
              ) : (
                <DocsTreeNav projectKey={project.key} nodes={nodes} current={current} />
              )}
              {data.truncated && <p className="text-xs text-muted-foreground">{messages.docs.truncated}</p>}
              {data.lastCommit && <LastChange commit={data.lastCommit} folder />}
            </aside>
            {current && location ? (
              <DocView key={current} project={project} path={current} location={location} />
            ) : (
              data.files.length > 0 && (
                <p className="text-muted-foreground">
                  {messages.docs.chooseDoc}{' '}
                  {data.files[0] && (
                    <Link to={docRoute(project.key, data.files[0].path)} className="text-primary underline underline-offset-4">
                      {data.files[0].title}
                    </Link>
                  )}
                </p>
              )
            )}
          </div>
        )}
      </div>
    )
  }

  return (
    <section className="grid gap-6">
      <ProjectHeader projectKey={project.key} projectName={project.name} action={action} />
      <p role="status" className="sr-only">
        {announcement}
      </p>
      {body}
    </section>
  )
}

/** A project's docs from its GitHub repository (TREK-75): /TREK/docs and /TREK/docs/guide/setup.md. */
export function DocsPage() {
  const { projectKey = '', '*': splat = '' } = useParams()
  return (
    <ProjectLoader projectKey={projectKey}>
      {(project) => <DocsView key={project.id} project={project} path={splat || null} />}
    </ProjectLoader>
  )
}
