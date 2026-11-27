/** The prefix the sanitiser gives every `id` and `name` from a doc's HTML and footnotes, as GitHub does. */
const CLOBBER_PREFIX = 'user-content-'

/**
 * The element a `#hash` in a doc points at (TREK-75): the id as written, or else with the sanitiser's prefix, the way
 * GitHub's own page script resolves them. Heading anchors keep their plain id (`#install`); ids from the doc's own HTML
 * carry one prefix (`#top` finds `user-content-top`), and GFM footnotes, whose links already say
 * `#user-content-fn-1`, carry two. A hash that cannot be decoded finds nothing.
 */
export function findAnchor(hash: string, root: Document = document): HTMLElement | null {
  let id: string
  try {
    id = decodeURIComponent(hash.replace(/^#/, ''))
  } catch {
    return null
  }
  if (!id) return null
  return root.getElementById(id) ?? root.getElementById(CLOBBER_PREFIX + id)
}
