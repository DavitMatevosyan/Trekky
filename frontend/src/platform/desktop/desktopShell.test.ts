import { checkServer, isDesktop } from './desktopShell.ts'

describe('checkServer', () => {
  it('is reachable when the health endpoint answers ok', async () => {
    const fetchFn = vi.fn().mockResolvedValue(new Response(JSON.stringify({ status: 'ok' }), { status: 200 }))

    await expect(checkServer('https://trekky.example.com', fetchFn)).resolves.toBe(true)
    expect(fetchFn).toHaveBeenCalledWith('https://trekky.example.com/api/v1/health', expect.objectContaining({ signal: expect.any(AbortSignal) }))
  })

  it('is unreachable on a network error, an error status or another answer', async () => {
    await expect(checkServer('https://a.example.com', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))).resolves.toBe(false)
    await expect(checkServer('https://a.example.com', vi.fn().mockResolvedValue(new Response('', { status: 502 })))).resolves.toBe(false)
    await expect(checkServer('https://a.example.com', vi.fn().mockResolvedValue(new Response('<html>', { status: 200 })))).resolves.toBe(false)
    await expect(
      checkServer('https://a.example.com', vi.fn().mockResolvedValue(new Response(JSON.stringify({ status: 'starting' }), { status: 200 }))),
    ).resolves.toBe(false)
  })

  it('gives up after the timeout', async () => {
    const hanging = vi.fn(
      (_url: string, init?: RequestInit) =>
        new Promise<Response>((_resolve, reject) => init?.signal?.addEventListener('abort', () => reject(new DOMException('timeout', 'TimeoutError')))),
    )

    await expect(checkServer('https://slow.example.com', hanging as unknown as typeof fetch, 20)).resolves.toBe(false)
  })
})

describe('isDesktop', () => {
  afterEach(() => {
    delete (window as unknown as Record<string, unknown>).__TAURI_INTERNALS__
  })

  it('is false in the browser and true inside the Tauri window', () => {
    expect(isDesktop()).toBe(false)
    ;(window as unknown as Record<string, unknown>).__TAURI_INTERNALS__ = {}
    expect(isDesktop()).toBe(true)
  })
})
