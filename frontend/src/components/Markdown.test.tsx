import { fireEvent, render, screen } from '@testing-library/react'
import type { Platform } from '../platform/Platform.ts'
import { PlatformContext } from '../platform/PlatformContext.ts'
import { createWebPlatform } from '../platform/webPlatform.ts'
import { externalUrl } from '../platform/externalUrl.ts'
import { Markdown } from './Markdown.tsx'

function renderWith(open: (url: string) => boolean, text: string) {
  const platform: Platform = { ...createWebPlatform(), links: { open } }
  return render(
    <PlatformContext.Provider value={platform}>
      <Markdown>{text}</Markdown>
    </PlatformContext.Provider>,
  )
}

describe('Markdown links', () => {
  it('lets the browser follow a link when the platform does not open it (the web app)', () => {
    const open = vi.fn<(url: string) => boolean>().mockReturnValue(false)
    renderWith(open, '[Docs](https://example.com/docs)')
    const link = screen.getByRole('link', { name: 'Docs' })

    const followed = fireEvent.click(link)

    expect(link).toHaveAttribute('href', 'https://example.com/docs')
    expect(open).toHaveBeenCalledWith('https://example.com/docs')
    expect(followed).toBe(true)
  })

  it('stops the browser when the platform opens the link itself (the desktop app)', () => {
    const open = vi.fn<(url: string) => boolean>().mockReturnValue(true)
    renderWith(open, '[Docs](https://example.com/docs)')

    const followed = fireEvent.click(screen.getByRole('link', { name: 'Docs' }))

    expect(followed).toBe(false)
  })

  it('keeps a link\'s title', () => {
    renderWith(() => false, '[Docs](https://example.com/docs "The spec")')

    expect(screen.getByRole('link', { name: 'Docs' })).toHaveAttribute('title', 'The spec')
  })

  it('never hands a link inside the app to the platform', () => {
    const open = vi.fn<(url: string) => boolean>().mockReturnValue(true)
    renderWith(open, '[Item](/TREK/items/TREK-5)')

    fireEvent.click(screen.getByRole('link', { name: 'Item' }))

    expect(open).not.toHaveBeenCalled()
  })

  it('leaves new-tab clicks to the browser', () => {
    const open = vi.fn<(url: string) => boolean>().mockReturnValue(true)
    renderWith(open, '[Docs](https://example.com/docs)')

    const followed = fireEvent.click(screen.getByRole('link', { name: 'Docs' }), { ctrlKey: true })

    expect(open).not.toHaveBeenCalled()
    expect(followed).toBe(true)
  })

  it('shows images as links through the platform too, never loading them', () => {
    const open = vi.fn<(url: string) => boolean>().mockReturnValue(false)
    renderWith(open, '![tracker](https://example.com/pixel.png)')

    expect(screen.queryByRole('img')).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'tracker' })).toHaveAttribute('href', 'https://example.com/pixel.png')
  })
})

describe('externalUrl', () => {
  it('is the absolute URL of an http(s) link to another origin, and null otherwise', () => {
    expect(externalUrl('https://example.com/a?b=1', 'https://trekky.test/x')).toBe('https://example.com/a?b=1')
    expect(externalUrl('/TREK/items/TREK-5', 'https://trekky.test/x')).toBeNull()
    expect(externalUrl('https://trekky.test/other', 'https://trekky.test/x')).toBeNull()
    expect(externalUrl('mailto:a@b.c', 'https://trekky.test/x')).toBeNull()
  })
})
