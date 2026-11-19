import { permissionsFor } from './permissions.ts'

describe('permissionsFor', () => {
  it.each([
    ['owner', true, true],
    ['admin', true, true],
    ['member', true, false],
    ['viewer', false, false],
  ])('lets a %s edit work: %s, create projects: %s', (role, editWork, manageProjects) => {
    expect(permissionsFor(role)).toEqual({ editWork, manageProjects })
  })

  it.each([undefined, 'superuser', 'toString', ''])('allows nothing beyond reading for the role %j', (role) => {
    expect(permissionsFor(role)).toEqual({ editWork: false, manageProjects: false })
  })
})
