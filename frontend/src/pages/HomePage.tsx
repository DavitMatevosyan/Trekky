import { useMe } from '../auth/useMe.ts'
import { messages } from '../i18n/messages.ts'

/** Landing page after sign-up and login; becomes the projects list in TRK-10. */
export function HomePage() {
  const me = useMe()

  return (
    <section className="grid gap-2">
      <h1 className="text-2xl font-semibold">{messages.home.title}</h1>
      {me.isError && <p role="alert">{messages.common.unexpectedError}</p>}
      {me.data && (
        <p data-testid="org-summary">{messages.home.welcome(me.data.organization.name, messages.roles[me.data.role] ?? me.data.role)}</p>
      )}
      <p className="text-muted-foreground">{messages.home.empty}</p>
    </section>
  )
}
