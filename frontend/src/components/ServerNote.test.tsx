import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createDesktopPlatform } from '../platform/desktop/desktopPlatform.ts'
import { PlatformContext } from '../platform/PlatformContext.ts'
import { createWebPlatform } from '../platform/webPlatform.ts'
import { ServerNote } from './ServerNote.tsx'

describe('ServerNote', () => {
  it('shows nothing in the browser', () => {
    const { container } = render(
      <PlatformContext.Provider value={createWebPlatform()}>
        <ServerNote />
      </PlatformContext.Provider>,
    )

    expect(container).toBeEmptyDOMElement()
  })

  it('in the desktop app shows the server and changes it on request', async () => {
    const change = vi.fn()
    const platform = createDesktopPlatform({ url: 'https://trekky.example.ts.net', change })
    render(
      <PlatformContext.Provider value={platform}>
        <ServerNote />
      </PlatformContext.Provider>,
    )

    expect(screen.getByText('Server: trekky.example.ts.net')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Change server' }))
    expect(change).toHaveBeenCalledOnce()
    expect(platform.apiBaseUrl).toBe('https://trekky.example.ts.net')
  })
})
