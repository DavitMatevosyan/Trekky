import type { Platform, ServerSwitch } from '../Platform.ts'
import { createWebPlatform } from '../webPlatform.ts'

/**
 * The desktop app's platform (ADR-0018): the API on the chosen server, which the app can change. Sign-in, token
 * storage and links still use the web implementations; the desktop client (`trekky-desktop`, ADR-0017) with the
 * system browser and the `trekky://` deep link replaces them in TREK-35 (TRK-26).
 */
export function createDesktopPlatform(server: ServerSwitch, overrides: Partial<Platform> = {}): Platform {
  const web = createWebPlatform()
  // No browser session in the app's window, so no silent sign-in: protected pages go to the login page.
  return createWebPlatform({ apiBaseUrl: server.url, server, login: { ...web.login, silent: false }, ...overrides })
}
