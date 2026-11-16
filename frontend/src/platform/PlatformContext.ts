import { createContext, useContext } from 'react'
import type { Platform } from './Platform.ts'
import { createWebPlatform } from './webPlatform.ts'

/** The platform the app runs on; the web one unless <App> is given another (desktop, tests). */
export const PlatformContext = createContext<Platform>(createWebPlatform())

export function usePlatform(): Platform {
  return useContext(PlatformContext)
}
