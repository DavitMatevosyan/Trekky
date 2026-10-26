/** Proof Key for Code Exchange (RFC 7636), S256 method. */
export interface PkcePair {
  verifier: string
  challenge: string
}

export function randomUrlSafeString(byteLength = 32): string {
  return base64Url(crypto.getRandomValues(new Uint8Array(byteLength)))
}

export async function createPkcePair(): Promise<PkcePair> {
  const verifier = randomUrlSafeString()
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier))
  return { verifier, challenge: base64Url(new Uint8Array(digest)) }
}

function base64Url(bytes: Uint8Array): string {
  let binary = ''
  for (const byte of bytes) binary += String.fromCharCode(byte)
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}
