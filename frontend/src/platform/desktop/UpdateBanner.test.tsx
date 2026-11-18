import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { UpdateBanner, type UpdateBannerProps } from './UpdateBanner.tsx'

function shellWith(check: UpdateBannerProps['shell']['checkForUpdate'], install = vi.fn().mockResolvedValue(undefined)) {
  return { checkForUpdate: vi.fn(check), installUpdate: install }
}

const newer = { version: '0.2.0', notes: null }

describe('UpdateBanner', () => {
  it('shows nothing when this version is the newest', async () => {
    const shell = shellWith(() => Promise.resolve(null))
    const { container } = render(<UpdateBanner shell={shell} />)

    await vi.waitFor(() => expect(shell.checkForUpdate).toHaveBeenCalledOnce())
    expect(container).toBeEmptyDOMElement()
  })

  it('stays quiet when the release feed cannot be reached', async () => {
    const shell = shellWith(() => Promise.reject(new Error('offline')))
    const { container } = render(<UpdateBanner shell={shell} />)

    await vi.waitFor(() => expect(shell.checkForUpdate).toHaveBeenCalledOnce())
    expect(container).toBeEmptyDOMElement()
  })

  it('offers a newer version, says what installing does, and installs it on request', async () => {
    const shell = shellWith(() => Promise.resolve(newer))
    render(<UpdateBanner shell={shell} />)

    expect(await screen.findByRole('status')).toHaveTextContent('Trekky 0.2.0 is available.')
    expect(screen.getByRole('region', { name: 'Update' })).toHaveTextContent('Windows asks for your permission')
    await userEvent.click(screen.getByRole('button', { name: 'Install and restart' }))

    expect(shell.installUpdate).toHaveBeenCalledOnce()
  })

  it('cannot start a second install while one is running', async () => {
    const shell = shellWith(
      () => Promise.resolve(newer),
      vi.fn(() => new Promise<void>(() => undefined)),
    )
    render(<UpdateBanner shell={shell} />)

    await userEvent.click(await screen.findByRole('button', { name: 'Install and restart' }))

    expect(screen.getByRole('button', { name: 'Installing…' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Later' })).toBeDisabled()
  })

  it('says so when the install fails, and can be put off until the next start', async () => {
    const shell = shellWith(() => Promise.resolve(newer), vi.fn().mockRejectedValue(new Error('bad signature')))
    render(<UpdateBanner shell={shell} />)

    await userEvent.click(await screen.findByRole('button', { name: 'Install and restart' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('The update could not be installed. Please try again later.')

    await userEvent.click(screen.getByRole('button', { name: 'Later' }))
    expect(screen.queryByRole('region', { name: 'Update' })).not.toBeInTheDocument()
  })
})
