import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { App } from './App.tsx'
import { DesktopGate } from './platform/desktop/DesktopGate.tsx'
import { createDesktopPlatform } from './platform/desktop/desktopPlatform.ts'
import { createTauriShell, isDesktop } from './platform/desktop/desktopShell.ts'
import './index.css'

const root = document.getElementById('root')
if (!root) throw new Error('Root element #root not found')

createRoot(root).render(
  <StrictMode>
    {isDesktop() ? (
      // Windows desktop app (ADR-0018): pick and reach a server first, then the same app on it.
      <DesktopGate shell={createTauriShell()}>{(server) => <App platform={createDesktopPlatform(server)} />}</DesktopGate>
    ) : (
      <App />
    )}
  </StrictMode>,
)
