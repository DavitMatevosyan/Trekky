import { permissionsFor } from './permissions.ts'

describe('permissionsFor', () => {
  it.each([
    ['owner', true, true, true, true],
    ['admin', true, true, true, false],
    ['member', true, false, false, false],
    ['viewer', false, false, false, false],
  ])(
    'lets a %s edit work: %s, create projects: %s, manage members: %s, invite admins: %s',
    (role, editWork, manageProjects, manageMembers, inviteAdmins) => {
      expect(permissionsFor(role)).toEqual({ editWork, manageProjects, manageMembers, inviteAdmins })
    },
  )

  it.each([undefined, 'superuser', 'toString', ''])('allows nothing beyond reading for the role %j', (role) => {
    expect(permissionsFor(role)).toEqual({ editWork: false, manageProjects: false, manageMembers: false, inviteAdmins: false })
  })
})
