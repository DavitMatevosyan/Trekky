import { finishInstall, startInstall } from './installState.ts'

function memoryStore() {
  const values = new Map<string, string>()
  return {
    getItem: (key: string) => values.get(key) ?? null,
    setItem: (key: string, value: string) => void values.set(key, value),
    removeItem: (key: string) => void values.delete(key),
  }
}

describe('GitHub install state', () => {
  it('adds a random state to the install URL and accepts it back once', () => {
    const store = memoryStore()

    const url = new URL(startInstall('https://github.com/apps/trekky/installations/new', store))
    const state = url.searchParams.get('state')

    expect(url.pathname).toBe('/apps/trekky/installations/new')
    expect(state).toMatch(/^[0-9a-f]{32}$/)
    expect(finishInstall(state, store)).toBe(true)
    expect(finishInstall(state, store)).toBe(false)
  })

  it('refuses a missing or different state', () => {
    const store = memoryStore()
    startInstall('https://github.com/apps/trekky/installations/new', store)
    expect(finishInstall(null, store)).toBe(false)

    startInstall('https://github.com/apps/trekky/installations/new', store)
    expect(finishInstall('someone-elses', store)).toBe(false)
  })

  it('refuses any state when this tab started no install', () => {
    expect(finishInstall('anything', memoryStore())).toBe(false)
  })
})
