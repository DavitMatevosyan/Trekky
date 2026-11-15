import { fakeDesktopShell } from '../../test/fakeDesktopShell.ts'
import { createDesktopPlatform } from './desktopPlatform.ts'

const server = { url: 'https://trekky.example.com', change: vi.fn() }

describe('createDesktopPlatform', () => {
  it('signs in as trekky-desktop in the system browser, back through trekky://', () => {
    const shell = fakeDesktopShell()
    const platform = createDesktopPlatform(server, shell, null)

    platform.login.open('https://trekky.example.com/connect/authorize?x=1')

    expect(platform.apiBaseUrl).toBe('https://trekky.example.com')
    expect(platform.clientId).toBe('trekky-desktop')
    expect(platform.login.redirectUri()).toBe('trekky://auth/callback')
    expect(platform.login).toMatchObject({ silent: false, mode: 'browser' })
    expect(shell.opened).toEqual(['https://trekky.example.com/connect/authorize?x=1'])
  })

  it('passes on sign-in callbacks only', () => {
    const shell = fakeDesktopShell()
    const received: string[] = []
    const stop = createDesktopPlatform(server, shell, null).login.onCallback?.((url) => received.push(url))

    shell.sendCallback('trekky://auth/callback?code=c&state=s')
    shell.sendCallback('trekky://auth/other?code=c')
    stop?.()
    shell.sendCallback('trekky://auth/callback?code=late&state=s')

    expect(received).toEqual(['trekky://auth/callback?code=c&state=s'])
  })

  it('keeps the refresh token in Credential Manager for this server', async () => {
    const shell = fakeDesktopShell()
    const { tokens } = createDesktopPlatform(server, shell, 'stored')

    expect(tokens.load()).toBe('stored')
    tokens.save('rotated')
    expect(tokens.load()).toBe('rotated')
    await vi.waitFor(() => expect(shell.credentials.get('https://trekky.example.com')).toBe('rotated'))

    tokens.save(null)
    expect(tokens.load()).toBeNull()
    await vi.waitFor(() => expect(shell.credentials.has('https://trekky.example.com')).toBe(false))
  })

  it('writes to Credential Manager in order, so the newest token is the one kept', async () => {
    let releaseFirst: () => void = () => {}
    const shell = fakeDesktopShell()
    const write = shell.saveRefreshToken
    shell.saveRefreshToken = vi.fn(async (server: string, token: string | null) => {
      if (token === 'first') await new Promise<void>((resolve) => (releaseFirst = resolve))
      await write(server, token)
    })
    const { tokens } = createDesktopPlatform(server, shell, null)

    tokens.save('first')
    tokens.save('second')
    await new Promise((resolve) => setTimeout(resolve, 0))
    releaseFirst()

    await vi.waitFor(() => expect(shell.saveRefreshToken).toHaveBeenCalledTimes(2))
    await vi.waitFor(() => expect(shell.credentials.get('https://trekky.example.com')).toBe('second'))
  })

  it('keeps working when Credential Manager cannot be written', () => {
    const shell = fakeDesktopShell({ saveRefreshToken: vi.fn().mockRejectedValue(new Error('denied')) })
    const { tokens } = createDesktopPlatform(server, shell, null)

    tokens.save('fresh')

    expect(tokens.load()).toBe('fresh')
  })

  it('opens outside links in the system browser and leaves in-app links alone', () => {
    const shell = fakeDesktopShell()
    const { links } = createDesktopPlatform(server, shell, null)

    expect(links.open('https://docs.example.com/guide')).toBe(true)
    expect(links.open('/TREK/items/TREK-1')).toBe(false)
    expect(links.open('mailto:ada@example.com')).toBe(false)
    expect(shell.opened).toEqual(['https://docs.example.com/guide'])
  })
})
