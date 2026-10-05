import { useEffect, useState } from 'react'
import { fetchHealth, type ApiStatus } from './api/health.ts'

export function App() {
  const [status, setStatus] = useState<ApiStatus>('checking')

  useEffect(() => {
    const controller = new AbortController()
    fetchHealth(controller.signal).then(setStatus)
    return () => controller.abort()
  }, [])

  return (
    <main className="app">
      <h1>Trekky</h1>
      <p>A lean task tracker.</p>
      <p role="status">
        API: <strong>{status}</strong>
      </p>
    </main>
  )
}
