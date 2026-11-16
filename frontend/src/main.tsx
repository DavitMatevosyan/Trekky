import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { App } from './App.tsx'
import { DesktopGate } from './platform/desktop/DesktopGate.tsx'
import { createDesktopPlatform } from './platform/desktop/desktopPlatform.ts'
import { createTauriShell, isDesktop } from './platform/desktop/desktopShell.ts'
import './index.css'

const shell = isDesktop() ? createTauriShell() : null

const root = document.getElementById('root')
if (!root) throw new Error('Root element #root not found')

createRoot(root).render(
  <StrictMode>
    {shell ? (
      // Windows desktop app (ADR-0018, ADR-0019): pick and reach a server first, then the same app on it.
      <DesktopGate shell={shell}>{(server, refreshToken) => <App platform={createDesktopPlatform(server, shell, refreshToken)} />}</DesktopGate>
    ) : (
      <App />
    )}
  </StrictMode>,
)
