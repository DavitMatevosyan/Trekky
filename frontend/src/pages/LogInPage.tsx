import { useMutation } from '@tanstack/react-query'
import type { FormEvent } from 'react'
import { Link, Navigate, useSearchParams } from 'react-router'
import { asProblem, fieldErrors, formLevelError, ProblemError } from '../api/problem.ts'
import { useAuth, useAuthStatus } from '../auth/AuthContext.ts'
import { beginAuthorization, safeReturnTo } from '../auth/oauthClient.ts'
import { AuthCard } from '../components/AuthCard.tsx'
import { FormField } from '../components/FormField.tsx'
import { FormError } from '../components/FormError.tsx'
import { Button } from '../components/ui/Button.tsx'
import { messages } from '../i18n/messages.ts'

interface Credentials {
  email: string
  password: string
}

const FIELDS = ['email', 'password']

/** US-02: log in sets the session cookie, then the code flow turns it into tokens. */
export function LogInPage() {
  const { api } = useAuth()
  const isAuthenticated = useAuthStatus() === 'authenticated'
  const [searchParams] = useSearchParams()
  const returnTo = safeReturnTo(searchParams.get('returnTo'))

  const logIn = useMutation({
    mutationFn: async (credentials: Credentials) => {
      const { error, response } = await api.POST('/api/v1/auth/login', { body: credentials })
      if (!response.ok) throw new ProblemError(asProblem(error))
      await beginAuthorization({ returnTo })
    },
  })

  if (isAuthenticated) return <Navigate to={returnTo} replace />

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    logIn.mutate({ email: String(form.get('email') ?? ''), password: String(form.get('password') ?? '') })
  }

  const problem = logIn.error instanceof ProblemError ? logIn.error.problem : null
  const errors = problem ? fieldErrors(problem) : {}
  const formError = logIn.error
    ? problem
      ? formLevelError(problem, FIELDS)
      : messages.common.unexpectedError
    : null
  const busy = logIn.isPending || logIn.isSuccess

  return (
    <AuthCard
      title={messages.logIn.title}
      footer={
        <>
          {messages.logIn.noAccount}{' '}
          <Link to="/sign-up" className="font-medium text-primary underline underline-offset-4 hover:no-underline">
            {messages.logIn.signUpLink}
          </Link>
        </>
      }
    >
      <form className="grid gap-4" onSubmit={onSubmit} noValidate>
        <FormError message={formError} />
        <FormField label={messages.common.email} name="email" type="email" autoComplete="email" required error={errors.email} />
        <FormField
          label={messages.common.password}
          name="password"
          type="password"
          autoComplete="current-password"
          required
          error={errors.password}
        />
        <Button type="submit" disabled={busy}>
          {busy ? messages.logIn.submitting : messages.logIn.submit}
        </Button>
      </form>
    </AuthCard>
  )
}
