import { useMutation } from '@tanstack/react-query'
import type { FormEvent } from 'react'
import { Link, Navigate } from 'react-router'
import { asProblem, fieldErrors, formLevelError, ProblemError } from '../api/problem.ts'
import { useAuth, useAuthStatus } from '../auth/AuthContext.ts'
import { beginAuthorization } from '../auth/oauthClient.ts'
import { AuthCard } from '../components/AuthCard.tsx'
import { FormError } from '../components/FormError.tsx'
import { FormField } from '../components/FormField.tsx'
import { Button } from '../components/ui/Button.tsx'
import { messages } from '../i18n/messages.ts'

/** Mirrors the API's password policy (US-01 AC3); the server stays the source of truth. */
const PASSWORD_MIN_LENGTH = 12

interface SignUpForm {
  email: string
  password: string
  organizationName: string
}

const FIELDS = ['email', 'password', 'organizationName']

/** US-01: sign-up creates the user, the organization and an Owner membership, then signs the user in. */
export function SignUpPage() {
  const { api } = useAuth()
  const isAuthenticated = useAuthStatus() === 'authenticated'

  const signUp = useMutation({
    mutationFn: async (body: SignUpForm) => {
      const { error, response } = await api.POST('/api/v1/auth/sign-up', { body })
      if (!response.ok) throw new ProblemError(asProblem(error))
      // Sign-up also sets the session cookie, so the code flow can run straight away (US-01 AC4).
      await beginAuthorization({ returnTo: '/' })
    },
  })

  if (isAuthenticated) return <Navigate to="/" replace />

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    signUp.mutate({
      email: String(form.get('email') ?? ''),
      password: String(form.get('password') ?? ''),
      organizationName: String(form.get('organizationName') ?? ''),
    })
  }

  const problem = signUp.error instanceof ProblemError ? signUp.error.problem : null
  const errors = problem ? fieldErrors(problem) : {}
  const formError = signUp.error
    ? problem
      ? formLevelError(problem, FIELDS)
      : messages.common.unexpectedError
    : null
  const busy = signUp.isPending || signUp.isSuccess

  return (
    <AuthCard
      title={messages.signUp.title}
      footer={
        <>
          {messages.signUp.haveAccount}{' '}
          <Link to="/login" className="font-medium text-primary underline underline-offset-4 hover:no-underline">
            {messages.signUp.logInLink}
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
          autoComplete="new-password"
          minLength={PASSWORD_MIN_LENGTH}
          required
          hint={messages.signUp.passwordHint}
          error={errors.password}
        />
        <FormField
          label={messages.signUp.organization}
          name="organizationName"
          autoComplete="organization"
          required
          error={errors.organizationName}
        />
        <Button type="submit" disabled={busy}>
          {busy ? messages.signUp.submitting : messages.signUp.submit}
        </Button>
      </form>
    </AuthCard>
  )
}
