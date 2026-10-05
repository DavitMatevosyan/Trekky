export type ApiStatus = 'checking' | 'ok' | 'unreachable'

export async function fetchHealth(signal?: AbortSignal): Promise<ApiStatus> {
  try {
    const response = await fetch('/api/v1/health', { signal })
    if (!response.ok) return 'unreachable'
    const body = (await response.json()) as { status?: string }
    return body.status === 'ok' ? 'ok' : 'unreachable'
  } catch {
    return 'unreachable'
  }
}
