import { findAnchor } from './anchors.ts'

describe('findAnchor', () => {
  beforeEach(() => {
    document.body.innerHTML = '<h2 id="install"></h2><li id="user-content-user-content-fn-1"></li><a id="user-content-top"></a>'
  })

  it('finds a heading by its plain id and prefixed ids by either form', () => {
    expect(findAnchor('#install')?.tagName).toBe('H2')
    expect(findAnchor('#user-content-fn-1')?.tagName).toBe('LI')
    expect(findAnchor('top')?.tagName).toBe('A')
  })

  it('finds nothing for an empty, unknown or undecodable hash', () => {
    expect(findAnchor('#')).toBeNull()
    expect(findAnchor('#nope')).toBeNull()
    expect(findAnchor('#%E0%A4%A')).toBeNull()
  })
})
