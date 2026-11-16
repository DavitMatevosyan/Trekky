/** The absolute URL of `href` when it leads out of the app (http or https on another origin), else null. */
export function externalUrl(href: string, pageUrl: string = window.location.href): string | null {
  try {
    const url = new URL(href, pageUrl)
    const external = (url.protocol === 'https:' || url.protocol === 'http:') && url.origin !== new URL(pageUrl).origin
    return external ? url.href : null
  } catch {
    return null
  }
}
