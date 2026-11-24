import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { fieldErrors, formLevelError, ProblemError } from '../api/problem.ts'
import { usePermissions } from '../auth/usePermissions.ts'
import { FormError } from '../components/FormError.tsx'
import { FormField } from '../components/FormField.tsx'
import { InlineConfirm } from '../components/InlineConfirm.tsx'
import { SelectField } from '../components/SelectField.tsx'
import { Button } from '../components/ui/Button.tsx'
import { messages } from '../i18n/messages.ts'
import { usePlatform } from '../platform/PlatformContext.ts'
import {
  useGitHubRepositories,
  useGitHubSettings,
  useLinkProjectRepository,
  useProjectRepository,
  useUnlinkProjectRepository,
  type GitHubConnection,
  type ProjectRepositoryLink,
  type VersionedProjectRepository,
} from './useGitHub.ts'

const t = messages.projectSettings
const FIELDS = ['installationId', 'repository', 'docsPath', 'branch']

/**
 * Project settings → Docs repository (TREK-74): where the project's docs live on GitHub. Everyone sees the link; Admins
 * and Owners pick a connected account, repository, folder and branch, which the API checks on GitHub before saving.
 */
export function DocsRepositorySection({ projectId }: { projectId: string }) {
  const can = usePermissions()
  const current = useProjectRepository(projectId)
  const settings = useGitHubSettings(can.manageProjects)
  const [announcement, setAnnouncement] = useState('')
  // Kept here: a 412 refetches the link, and its new ETag starts the form again, which would drop the form's own error.
  const [stale, setStale] = useState(false)

  return (
    <section aria-labelledby="docs-repository-title" className="grid gap-4 rounded-xl border border-border bg-surface p-4">
      <div className="grid gap-1">
        <h2 id="docs-repository-title" className="text-lg font-semibold">
          {t.title}
        </h2>
        <p className="text-sm text-muted-foreground">{t.intro}</p>
      </div>

      <p role="status" className="text-sm empty:sr-only">
        {announcement}
      </p>
      <FormError message={stale ? t.stale : null} />

      {current.isPending && <p className="text-sm text-muted-foreground">{t.loading}</p>}
      {current.isError && <FormError message={messages.common.unexpectedError} />}
      {current.isSuccess && (current.data ? <LinkSummary link={current.data.link} /> : <p className="text-sm">{t.notLinked}</p>)}

      {!can.manageProjects && current.isSuccess && <p className="text-sm text-muted-foreground">{t.readOnly}</p>}

      {can.manageProjects && current.isSuccess && settings.isPending && <p className="text-sm text-muted-foreground">{t.loading}</p>}
      {can.manageProjects && current.isSuccess && settings.data && !settings.data.configured && (
        <p className="text-sm">{messages.github.notConfigured}</p>
      )}
      {can.manageProjects && current.isSuccess && settings.data?.configured && settings.data.installations.length === 0 && (
        <p className="text-sm">
          {t.noConnection}{' '}
          <Link to="/settings" className="text-primary underline underline-offset-4 hover:no-underline">
            {t.goToSettings}
          </Link>
        </p>
      )}
      {can.manageProjects && current.isSuccess && settings.data?.configured && settings.data.installations.length > 0 && (
        <DocsRepositoryForm
          // A new version (saved, or refetched after a 412) starts the form again from it.
          key={current.data?.etag ?? 'new'}
          projectId={projectId}
          current={current.data}
          connections={settings.data.installations}
          onSaving={() => {
            setStale(false)
            setAnnouncement('')
          }}
          onStale={() => setStale(true)}
          onSaved={() => setAnnouncement(t.saved)}
          onUnlinked={() => setAnnouncement(t.unlinked)}
        />
      )}
    </section>
  )
}

function LinkSummary({ link }: { link: ProjectRepositoryLink }) {
  const { links } = usePlatform()
  return (
    <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
      <dt className="text-muted-foreground">{t.linkedTo}</dt>
      <dd className="font-mono">{link.repository}</dd>
      <dt className="text-muted-foreground">{t.folder}</dt>
      <dd className="font-mono">{link.docsPath === '' ? t.rootFolder : link.docsPath}</dd>
      <dt className="text-muted-foreground">{t.branch}</dt>
      <dd className="font-mono">{link.branch}</dd>
      <dd className="col-span-2">
        <a
          href={link.htmlUrl}
          target="_blank"
          rel="noreferrer"
          className="text-primary underline underline-offset-4 hover:no-underline"
          onClick={(event) => {
            if (links.open(link.htmlUrl)) event.preventDefault()
          }}
        >
          {t.openOnGitHub}
        </a>
      </dd>
    </dl>
  )
}

interface DocsRepositoryFormProps {
  projectId: string
  current: VersionedProjectRepository | null
  connections: GitHubConnection[]
  onSaving: () => void
  /** Someone else changed the link (412); the latest is being refetched. */
  onStale: () => void
  onSaved: () => void
  onUnlinked: () => void
}

function DocsRepositoryForm({ projectId, current, connections, onSaving, onStale, onSaved, onUnlinked }: DocsRepositoryFormProps) {
  const link = current?.link
  const firstConnection = connections[0]?.id ?? ''
  const [installationId, setInstallationId] = useState(
    link && connections.some((c) => c.id === link.installationId) ? link.installationId : firstConnection,
  )
  const [repository, setRepository] = useState(link?.repository ?? '')
  const [docsPath, setDocsPath] = useState(link ? link.docsPath || '/' : 'docs/')
  const [branch, setBranch] = useState(link?.branch ?? 'dev')
  const [confirmingUnlink, setConfirmingUnlink] = useState(false)
  const repositories = useGitHubRepositories(installationId || null)
  const save = useLinkProjectRepository(projectId, current)
  const unlink = useUnlinkProjectRepository(projectId)

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    onSaving()
    save.mutate(
      { installationId, repository: repository.trim(), docsPath, branch: branch.trim() },
      {
        onSuccess: onSaved,
        onError: (error) => {
          if (error instanceof ProblemError && error.problem.status === 412) onStale()
        },
      },
    )
  }

  const problem = save.error instanceof ProblemError ? save.error.problem : null
  const errors = problem ? fieldErrors(problem) : {}
  // A 412 is shown by the section, which outlives this form.
  const formError = !save.error || problem?.status === 412
    ? null
    : problem
      ? problem.errors
        ? formLevelError(problem, FIELDS)
        : (problem.detail ?? problem.title ?? messages.common.unexpectedError)
      : messages.common.unexpectedError

  // A list to pick from when the account's repositories are all listed; otherwise the name is typed.
  const listed = repositories.data && !repositories.data.truncated ? repositories.data.items : null
  const repositoryOptions = listed
    ? [
        { value: '', label: t.chooseRepository },
        ...(repository && !listed.some((r) => r.fullName === repository) ? [{ value: repository, label: repository }] : []),
        ...listed.map((r) => ({ value: r.fullName, label: r.fullName })),
      ]
    : null

  return (
    <form aria-labelledby="docs-repository-title" className="grid gap-4" onSubmit={onSubmit} noValidate>
      <FormError message={formError} />
      {connections.length > 1 && (
        <SelectField
          label={t.connection}
          name="installationId"
          value={installationId}
          onChange={(event) => {
            setInstallationId(event.target.value)
            setRepository('')
          }}
          options={connections.map((c) => ({ value: c.id, label: c.account }))}
          error={errors.installationId}
        />
      )}
      {connections.length === 1 && errors.installationId && <FormError message={errors.installationId} />}
      {repositoryOptions ? (
        <SelectField
          label={t.repository}
          name="repository"
          value={repository}
          onChange={(event) => setRepository(event.target.value)}
          options={repositoryOptions}
          error={errors.repository}
        />
      ) : (
        <FormField
          label={t.repository}
          name="repository"
          autoComplete="off"
          spellCheck={false}
          className="font-mono"
          value={repository}
          onChange={(event) => setRepository(event.target.value)}
          hint={repositories.isPending && installationId ? t.loadingRepositories : repositories.data?.truncated ? t.truncated : t.repositoryHint}
          error={errors.repository}
        />
      )}
      {repositories.isError && (
        <FormError
          message={
            repositories.error instanceof ProblemError
              ? (repositories.error.problem.detail ?? repositories.error.problem.title ?? messages.common.unexpectedError)
              : messages.common.unexpectedError
          }
        />
      )}
      <FormField
        label={t.docsPath}
        name="docsPath"
        autoComplete="off"
        spellCheck={false}
        className="font-mono"
        value={docsPath}
        onChange={(event) => setDocsPath(event.target.value)}
        hint={t.docsPathHint}
        error={errors.docsPath}
      />
      <FormField
        label={t.branch}
        name="branch"
        autoComplete="off"
        spellCheck={false}
        className="font-mono"
        value={branch}
        onChange={(event) => setBranch(event.target.value)}
        hint={t.branchHint}
        error={errors.branch}
      />
      <div className="flex flex-wrap items-start justify-between gap-2">
        <Button type="submit" disabled={save.isPending || unlink.isPending}>
          {save.isPending ? t.saving : t.save}
        </Button>
        {current &&
          (confirmingUnlink ? (
            <InlineConfirm
              message={t.confirmUnlink}
              confirmLabel={t.unlink}
              pendingLabel={t.unlinking}
              cancelLabel={messages.github.callback.cancel}
              pending={unlink.isPending}
              onConfirm={() => unlink.mutate(undefined, { onSuccess: onUnlinked })}
              onCancel={() => setConfirmingUnlink(false)}
            />
          ) : (
            <Button type="button" variant="outline" onClick={() => setConfirmingUnlink(true)} disabled={save.isPending}>
              {t.unlink}
            </Button>
          ))}
      </div>
      {unlink.isError && <FormError message={messages.common.unexpectedError} />}
    </form>
  )
}
