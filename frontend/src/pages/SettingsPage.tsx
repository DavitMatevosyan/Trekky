import { useLocation } from 'react-router'
import { useMe } from '../auth/useMe.ts'
import { usePermissions } from '../auth/usePermissions.ts'
import { GitHubSettingsSection } from '../github/GitHubSettingsSection.tsx'
import { messages } from '../i18n/messages.ts'

/** Organization settings (TREK-74): for now the GitHub connection. Admins and Owners only, as the API (ManageProjects). */
export function SettingsPage() {
  const me = useMe()
  const can = usePermissions()
  // The GitHub callback comes back here with what it connected, to announce it.
  const notice = (useLocation().state as { notice?: string } | null)?.notice

  return (
    <section className="grid gap-6">
      <h1 className="text-2xl font-semibold">{messages.settings.title}</h1>
      {me.isSuccess && (can.manageProjects ? <GitHubSettingsSection notice={notice} /> : <p>{messages.settings.adminsOnly}</p>)}
    </section>
  )
}
