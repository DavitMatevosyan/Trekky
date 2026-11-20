import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { FormEvent } from 'react'
import { Link, useLocation } from 'react-router'
import { asProblem, fieldErrors, formLevelError, ProblemError } from '../api/problem.ts'
import { useAuth, useAuthStatus } from '../auth/AuthContext.ts'
import { beginAuthorization } from '../auth/oauthClient.ts'
import { useMe } from '../auth/useMe.ts'
import { AuthCard } from '../components/AuthCard.tsx'
import { FormError } from '../components/FormError.tsx'
import { FormField } from '../components/FormField.tsx'
import { Button } from '../components/ui/Button.tsx'
import { Input } from '../components/ui/Input.tsx'
import { Label } from '../components/ui/Label.tsx'
import { messages } from '../i18n/messages.ts'
import { usePlatform } from '../platform/PlatformContext.ts'

/** Mirrors the API's password policy (US-01 AC3); the server stays the source of truth. */
const PASSWORD_MIN_LENGTH = 12

const FIELDS = ['password', 'displayName']

/** The token is the link's fragment (`/invite#<token>`, ADR-0027), which never reaches a server log. */
function useInviteToken(): string {
  // Used as is: a token is base64url, so it needs no decoding, and a stray `%` must not crash the page.
  return useLocation().hash.replace(/^#/, '')
}

/**
 * The page an invite link opens (TREK-47): who invited you to which organization and role, then a password (and an
 * optional name) to create the account. Accepting sets the session cookie, so sign-in continues with the code flow
 * as after sign-up, and the new member lands in the inviter's organization.
 */
export function InvitePage() {
  const token = useInviteToken()
  const { api } = useAuth()
  const platform = usePlatform()
  const isAuthenticated = useAuthStatus() === 'authenticated'

  const preview = useQuery({
    queryKey: ['invite-preview', token],
    enabled: token !== '' && !isAuthenticated,
    retry: false,
    queryFn: async () => {
      const { data, error } = await api.POST('/api/v1/auth/invites/preview', { body: { token } })
      if (!data) throw new ProblemError(asProblem(error))
      return data
    },
  })

  const accept = useMutation({
    mutationFn: async (body: { password: string; displayName: string }) => {
      const { error, response } = await api.POST('/api/v1/auth/invites/accept', { body: { token, ...body } })
      if (!response.ok) throw new ProblemError(asProblem(error))
      await beginAuthorization({ returnTo: '/', platform })
    },
  })

  const footer = (
    <>
      {messages.acceptInvite.haveAccount}{' '}
      <Link to="/login" className="font-medium text-primary underline underline-offset-4 hover:no-underline">
        {messages.signUp.logInLink}
      </Link>
    </>
  )

  if (isAuthenticated) return <SignedInNotice />

  if (token === '' || preview.isError) {
    const detail = preview.error instanceof ProblemError ? preview.error.message : token === '' ? messages.acceptInvite.missingToken : messages.common.unexpectedError
    return (
      <AuthCard title={messages.acceptInvite.unavailableTitle} footer={footer}>
        <p role="alert">{detail}</p>
      </AuthCard>
    )
  }

  if (!preview.data) {
    return (
      <AuthCard title={messages.acceptInvite.title} footer={footer}>
        <p role="status" className="text-muted-foreground">
          {messages.acceptInvite.loading}
        </p>
      </AuthCard>
    )
  }

  const invite = preview.data

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    accept.mutate({ password: String(form.get('password') ?? ''), displayName: String(form.get('displayName') ?? '') })
  }

  const problem = accept.error instanceof ProblemError ? accept.error.problem : null
  const errors = problem ? fieldErrors(problem) : {}
  const formError = accept.error ? (problem ? formLevelError(problem, FIELDS) : messages.common.unexpectedError) : null
  const busy = accept.isPending || accept.isSuccess

  return (
    <AuthCard title={messages.acceptInvite.title} footer={footer}>
      <p className="mb-4 text-sm">
        {invite.invitedBy
          ? messages.acceptInvite.summary(invite.invitedBy, invite.organization.name, messages.acceptInvite.asRole(invite.role))
          : messages.acceptInvite.summaryNoInviter(invite.organization.name, messages.acceptInvite.asRole(invite.role))}
      </p>
      <form className="grid gap-4" onSubmit={onSubmit} noValidate>
        <FormError message={formError} />
        <div className="grid gap-1.5">
          <Label htmlFor="invite-email">{messages.common.email}</Label>
          <Input id="invite-email" type="email" autoComplete="username" value={invite.email} readOnly />
        </div>
        <FormField
          label={messages.acceptInvite.displayName}
          name="displayName"
          autoComplete="name"
          maxLength={80}
          hint={messages.acceptInvite.displayNameHint}
          error={errors.displayName}
        />
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
        <Button type="submit" disabled={busy}>
          {busy ? messages.acceptInvite.submitting : messages.acceptInvite.submit}
        </Button>
      </form>
    </AuthCard>
  )
}

/** Accepting creates a new account, so someone already signed in logs out first; the page then shows the invite. */
function SignedInNotice() {
  const { api, session } = useAuth()
  const queryClient = useQueryClient()
  const me = useMe()
  const logOut = useMutation({
    // Drop the old user's cached data before the session ends: once it does, this page asks for the invite, and
    // clearing the cache after that would drop that request too.
    mutationFn: () =>
      session.logOut(async () => {
        try {
          return await api.POST('/api/v1/auth/logout')
        } finally {
          queryClient.clear()
        }
      }),
  })

  return (
    <AuthCard title={messages.acceptInvite.title} footer={null}>
      <div className="grid gap-4">
        {me.data && <p>{messages.acceptInvite.signedInAs(me.data.user.email ?? me.data.user.displayName)}</p>}
        <Button onClick={() => logOut.mutate()} disabled={logOut.isPending}>
          {logOut.isPending ? messages.shell.loggingOut : messages.acceptInvite.logOutToAccept}
        </Button>
      </div>
    </AuthCard>
  )
}
