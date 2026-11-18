import { permissionsFor, type Permissions } from './permissions.ts'
import { useMe } from './useMe.ts'

/** What the signed-in user's role may do (ADR-0025); nothing beyond reading until /me has loaded. */
export function usePermissions(): Permissions {
  return permissionsFor(useMe().data?.role)
}
