import { fireEvent, render, screen } from '@testing-library/react'
import { SOURCE_CODE_URL } from '../lib/sourceCode.ts'
import { PlatformContext } from '../platform/PlatformContext.ts'
import { createWebPlatform } from '../platform/webPlatform.ts'
import { SourceCodeLink } from './SourceCodeLink.tsx'

describe('SourceCodeLink', () => {
  it('links to the source code in a new tab in the browser (AGPL-3.0)', () => {
    render(<SourceCodeLink />)

    const link = screen.getByRole('link', { name: 'Source code (AGPL-3.0)' })
    expect(link).toHaveAttribute('href', SOURCE_CODE_URL)
    expect(link).toHaveAttribute('target', '_blank')
    expect(link).toHaveAttribute('rel', 'noreferrer')
  })

  it('lets the desktop app open it in the system browser instead of the window', () => {
    const opened: string[] = []
    const platform = createWebPlatform({
      links: {
        open: (url) => {
          opened.push(url)
          return true
        },
      },
    })
    render(
      <PlatformContext.Provider value={platform}>
        <SourceCodeLink />
      </PlatformContext.Provider>,
    )

    const click = fireEvent.click(screen.getByRole('link', { name: 'Source code (AGPL-3.0)' }))

    expect(opened).toEqual([SOURCE_CODE_URL])
    expect(click).toBe(false) // default prevented: the app window stays where it is
  })
})
