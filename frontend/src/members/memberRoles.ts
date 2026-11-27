import type { Permissions } from '../auth/permissions.ts'
import type { Member } from '../workItems/useMembers.ts'
import type { MemberRole } from './useMemberChanges.ts'

const ALL_ROLES: readonly MemberRole[] = ['owner', 'admin', 'member', 'viewer']
const ADMIN_MANAGED: readonly MemberRole[] = ['member', 'viewer']

/**
 * The roles the caller may give this member; empty when they may neither change nor remove them. Mirrors the API's rules
 * (ADR-0026, ADR-0034): Admins manage Members and Viewers, only an Owner manages Admins and Owners. Nobody changes their
 * own role here, and the last Owner stays as they are.
 */
export function rolesFor(member: Member, can: Permissions, { self, lastOwner }: { self: boolean; lastOwner: boolean }): readonly MemberRole[] {
  if (!can.manageMembers || self || lastOwner) return []
  if (can.manageAdmins) return ALL_ROLES
  return ADMIN_MANAGED.some((role) => role === member.role) ? ADMIN_MANAGED : []
}
