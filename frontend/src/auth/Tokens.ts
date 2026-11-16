/** OAuth tokens, held in memory only (ADR-0003). */
export interface Tokens {
  accessToken: string
  refreshToken: string | null
  /** Epoch milliseconds. */
  expiresAt: number
}
