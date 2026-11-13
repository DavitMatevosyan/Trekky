import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ServerSwitch } from '../Platform.ts'
import { DesktopGate } from './DesktopGate.tsx'
import { ServerUrlError, type DesktopShell } from './desktopShell.ts'

function fakeShell(overrides: Partial<DesktopShell> = {}): DesktopShell & { saved: string[] } {
  const saved: string[] = []
  return {
    saved,
    serverSettings: vi.fn().mockResolvedValue({ serverUrl: null, defaultServerUrl: 'https://default.example.com' }),
    setServerUrl: vi.fn(async (url: string) => {
      saved.push(url)
      return url.replace(/\/+$/, '')
    }),
    checkServer: vi.fn().mockResolvedValue(true),
    ...overrides,
  }
}

function renderGate(shell: DesktopShell) {
  let server: ServerSwitch | undefined
  render(
    <DesktopGate shell={shell}>
      {(current) => {
        server = current
        return <p>App on {current.url}</p>
      }}
    </DesktopGate>,
  )
  return {
    server: () => {
      if (!server) throw new Error('The app has not been rendered yet')
      return server
    },
  }
}

describe('DesktopGate', () => {
  it('first run: offers the default server, saves it and opens the app once it answers', async () => {
    const shell = fakeShell()
    renderGate(shell)

    const field = await screen.findByLabelText('Server address')
    expect(field).toHaveValue('https://default.example.com')
    await userEvent.click(screen.getByRole('button', { name: 'Connect' }))

    expect(await screen.findByText('App on https://default.example.com')).toBeInTheDocument()
    expect(shell.saved).toEqual(['https://default.example.com'])
    expect(shell.checkServer).toHaveBeenCalledWith('https://default.example.com')
  })

  it('first run: a refused address shows why and nothing opens', async () => {
    const shell = fakeShell({ setServerUrl: vi.fn().mockRejectedValue(new ServerUrlError('insecure')) })
    renderGate(shell)

    const field = await screen.findByLabelText('Server address')
    await userEvent.clear(field)
    await userEvent.type(field, 'http://trekky.example.com')
    await userEvent.click(screen.getByRole('button', { name: 'Connect' }))

    expect(await screen.findByText('Use an https:// address. Plain http is only allowed for localhost.')).toBeInTheDocument()
    expect(screen.getByLabelText('Server address')).toHaveValue('http://trekky.example.com')
    expect(shell.checkServer).not.toHaveBeenCalled()
  })

  it('a saved server that does not answer shows "can’t reach" until a retry succeeds', async () => {
    const checkServer = vi.fn().mockResolvedValueOnce(false).mockResolvedValueOnce(true)
    const shell = fakeShell({
      serverSettings: vi.fn().mockResolvedValue({ serverUrl: 'https://trekky.example.com', defaultServerUrl: 'https://default.example.com' }),
      checkServer,
    })
    renderGate(shell)

    expect(await screen.findByRole('heading', { name: 'Can’t reach the server' })).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('https://trekky.example.com')

    await userEvent.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByText('App on https://trekky.example.com')).toBeInTheDocument()
    expect(checkServer).toHaveBeenCalledTimes(2)
  })

  it('"Change server" on the unreachable screen opens the server screen with the current address', async () => {
    const shell = fakeShell({
      serverSettings: vi.fn().mockResolvedValue({ serverUrl: 'https://old.example.com', defaultServerUrl: 'https://default.example.com' }),
      checkServer: vi.fn(async (url: string) => url === 'https://new.example.com'),
    })
    renderGate(shell)

    await userEvent.click(await screen.findByRole('button', { name: 'Change server' }))
    const field = screen.getByLabelText('Server address')
    expect(field).toHaveValue('https://old.example.com')
    await userEvent.clear(field)
    await userEvent.type(field, 'https://new.example.com')
    await userEvent.click(screen.getByRole('button', { name: 'Connect' }))

    expect(await screen.findByText('App on https://new.example.com')).toBeInTheDocument()
    expect(shell.saved).toEqual(['https://new.example.com'])
  })

  it('the app can leave for the server screen and come back to the same server', async () => {
    const shell = fakeShell({
      serverSettings: vi.fn().mockResolvedValue({ serverUrl: 'https://trekky.example.com', defaultServerUrl: 'https://default.example.com' }),
    })
    const gate = renderGate(shell)
    await screen.findByText('App on https://trekky.example.com')

    gate.server().change()

    expect(await screen.findByRole('heading', { name: 'Connect to your Trekky server' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Keep the current server' }))
    expect(await screen.findByText('App on https://trekky.example.com')).toBeInTheDocument()
    expect(shell.saved).toEqual([])
  })

  it('shows that it is connecting while the server is checked', async () => {
    let answer: (reachable: boolean) => void = () => {}
    const shell = fakeShell({
      serverSettings: vi.fn().mockResolvedValue({ serverUrl: 'https://trekky.example.com', defaultServerUrl: 'https://default.example.com' }),
      checkServer: vi.fn(() => new Promise<boolean>((resolve) => (answer = resolve))),
    })
    renderGate(shell)

    expect(await screen.findByRole('status')).toHaveTextContent('Connecting to https://trekky.example.com…')
    answer(true)
    await waitFor(() => expect(screen.getByText('App on https://trekky.example.com')).toBeInTheDocument())
  })
})
