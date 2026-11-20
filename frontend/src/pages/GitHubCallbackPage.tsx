import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { ProblemError } from '../api/problem.ts'
import { FormError } from '../components/FormError.tsx'
import { Button } from '../components/ui/Button.tsx'
import { finishInstall } from '../github/installState.ts'
import { useConnectGitHub } from '../github/useGitHub.ts'
import { messages } from '../i18n/messages.ts'

const t = messages.github.callback

/**
 * Where GitHub sends the browser after the Trekky app is installed (TREK-74, ADR-0027), with `code` and
 * `installation_id`. When `state` is the one this tab sent, it connects at once; otherwise (a link from someone else,
 * or the install was started in the desktop app) it asks first. The API checks the code against the installation.
 */
export function GitHubCallbackPage() {
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const connect = useConnectGitHub()
  const code = params.get('code')
  const installationId = Number(params.get('installation_id'))
  const requested = params.get('setup_action') === 'request'
  const usable = code !== null && code !== '' && Number.isSafeInteger(installationId) && installationId > 0
  const [confirming, setConfirming] = useState(false)
  // A code works once, and React runs effects twice in development: start only once.
  const started = useRef(false)

  function submit() {
    if (!usable) return
    connect.mutate(
      { code, installationId },
      { onSuccess: (connection) => void navigate('/settings', { replace: true, state: { notice: messages.github.connected(connection.account) } }) },
    )
  }

  useEffect(() => {
    if (started.current) return
    started.current = true
    const ours = finishInstall(params.get('state'))
    if (!usable || requested) return
    if (ours) submit()
    else setConfirming(true)
    // Runs once for the URL GitHub sent; submit only reads values fixed for this page.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const problem = connect.error instanceof ProblemError ? connect.error.problem : null
  const error = connect.error ? (problem?.detail ?? problem?.title ?? messages.common.unexpectedError) : null

  return (
    <section aria-labelledby="github-callback-title" className="mx-auto grid max-w-lg gap-4">
      <h1 id="github-callback-title" className="text-2xl font-semibold">
        {confirming && !connect.isPending && !connect.isSuccess ? t.confirmTitle : t.title}
      </h1>

      {requested && <p>{t.requested}</p>}
      {!requested && !usable && <p role="alert">{t.missing}</p>}

      {connect.isPending && <p role="status">{t.connecting}</p>}
      <FormError message={error} />

      {confirming && !connect.isPending && !connect.isSuccess && !connect.isError && (
        <>
          <p>{t.confirmBody}</p>
          <div className="flex flex-wrap gap-2">
            <Button onClick={submit}>{t.confirm}</Button>
            <Button variant="outline" onClick={() => void navigate('/settings', { replace: true })}>
              {t.cancel}
            </Button>
          </div>
        </>
      )}

      {(requested || !usable || connect.isError) && (
        <p>
          <Link to="/settings" className="text-primary underline underline-offset-4 hover:no-underline">
            {t.backToSettings}
          </Link>
        </p>
      )}
    </section>
  )
}
