import { useMutation } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router'
import { beginAuthorization } from '../auth/oauthClient.ts'
import { messages } from '../i18n/messages.ts'
import { usePlatform } from '../platform/PlatformContext.ts'
import { AuthCard } from './AuthCard.tsx'
import { FormError } from './FormError.tsx'
import { ServerNote } from './ServerNote.tsx'
import { Button } from './ui/Button.tsx'

export interface BrowserSignInProps {
  screen: 'log-in' | 'sign-up'
  /** Where to go once signed in; carried through the browser and back. */
  returnTo: string
}

/**
 * The desktop app's login and sign-up (ADR-0019): the forms are the server's web pages in the system browser, so the
 * password never passes through the app. The browser sends the user back through `trekky://`, and the app continues
 * on its own; this screen only starts it and offers to open the browser again.
 */
export function BrowserSignIn({ screen, returnTo }: BrowserSignInProps) {
  const platform = usePlatform()
  const text = screen === 'log-in' ? messages.browserSignIn.logIn : messages.browserSignIn.signUp
  const other = { pathname: screen === 'log-in' ? '/sign-up' : '/login', search: returnTo === '/' ? '' : `?${new URLSearchParams({ returnTo }).toString()}` }

  // The first click starts the request; "Open the browser again" opens that same request, because a new one would
  // replace the state the browser's answer has to match.
  const [opened, setOpened] = useState<string | null>(null)
  const start = useMutation({
    mutationFn: async () => {
      if (opened) {
        await platform.login.open(opened)
        return
      }
      setOpened(await beginAuthorization({ returnTo, platform, screen: screen === 'sign-up' ? 'sign-up' : undefined }))
    },
  })

  return (
    <AuthCard
      title={text.title}
      footer={
        <>
          {text.otherPrompt}{' '}
          <Link to={other} className="font-medium text-primary underline underline-offset-4 hover:no-underline">
            {text.otherLink}
          </Link>
        </>
      }
    >
      <div className="grid gap-4">
        <FormError message={start.isError ? messages.common.unexpectedError : null} />
        {opened && !start.isError ? (
          <p role="status" className="text-sm">
            {messages.browserSignIn.waiting}
          </p>
        ) : (
          <p className="text-sm text-muted-foreground">{text.intro}</p>
        )}
        <Button type="button" onClick={() => start.mutate()} disabled={start.isPending} variant={opened ? 'outline' : 'default'}>
          {opened ? messages.browserSignIn.openAgain : text.submit}
        </Button>
      </div>
      <ServerNote />
    </AuthCard>
  )
}
