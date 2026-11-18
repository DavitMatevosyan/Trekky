/**
 * What a role may do, mirroring the API's role policies (ADR-0025). The API is the guard and answers 403 otherwise;
 * this only keeps the UI from offering what would be refused.
 */
export interface Permissions {
  /** Create and edit work items, change their status, move them on the board, comment (EditWork). */
  editWork: boolean
  /** Create projects (ManageProjects). */
  manageProjects: boolean
}

const NONE: Permissions = { editWork: false, manageProjects: false }

const BY_ROLE: Record<string, Permissions> = {
  owner: { editWork: true, manageProjects: true },
  admin: { editWork: true, manageProjects: true },
  member: { editWork: true, manageProjects: false },
  viewer: NONE,
}

/** An unknown role, or none yet while /me loads, may do nothing beyond reading. */
export function permissionsFor(role: string | undefined): Permissions {
  return (role !== undefined && Object.hasOwn(BY_ROLE, role) ? BY_ROLE[role] : undefined) ?? NONE
}
