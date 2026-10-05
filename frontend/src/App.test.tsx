import { render, screen } from '@testing-library/react'
import { App } from './App.tsx'

describe('App', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows the API as ok when the health endpoint answers', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(new Response(JSON.stringify({ status: 'ok' }), { status: 200 })),
    )

    render(<App />)

    expect(await screen.findByText('ok')).toBeInTheDocument()
  })

  it('shows the API as unreachable when the request fails', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('offline')))

    render(<App />)

    expect(await screen.findByText('unreachable')).toBeInTheDocument()
  })
})
