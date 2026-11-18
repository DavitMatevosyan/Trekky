/**
 * The update feed (ADR-0024): `latest.json`, which the installed app reads from the newest GitHub release to learn
 * whether a newer version exists, where its MSI is and the MSI's signature. CI writes it next to the signed MSI:
 *
 *   node scripts/update-manifest.mts <version> <tag> <msi file> <out file>
 *
 * The signature is the content of `<msi file>.sig`, which `tauri build` writes when the signing key is set.
 */
import { readFileSync, writeFileSync } from 'node:fs'
import { basename } from 'node:path'
import { fileURLToPath } from 'node:url'

export const REPOSITORY_URL = 'https://github.com/DavitMatevosyan/Trekky'

export interface UpdateManifest {
  version: string
  notes: string
  pub_date: string
  platforms: Record<string, { signature: string; url: string }>
}

/** The feed entry for one Windows release; the MSI is downloaded from the release the tag created. */
export function updateManifest(input: { version: string; tag: string; msiName: string; signature: string; publishedAt: Date }): UpdateManifest {
  const signature = input.signature.trim()
  if (signature === '') throw new Error('The MSI signature is empty; was the signing key set for the build?')
  const url = `${REPOSITORY_URL}/releases/download/${encodeURIComponent(input.tag)}/${encodeURIComponent(input.msiName)}`
  return {
    version: input.version,
    notes: `Trekky for Windows ${input.version}`,
    pub_date: input.publishedAt.toISOString(),
    // The updater looks for "windows-x86_64-msi" first, then "windows-x86_64".
    platforms: { 'windows-x86_64': { signature, url } },
  }
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const [version, tag, msi, out] = process.argv.slice(2)
  if (!version || !tag || !msi || !out) {
    console.error('Usage: node scripts/update-manifest.mts <version> <tag> <msi file> <out file>')
    process.exit(1)
  }
  const manifest = updateManifest({ version, tag, msiName: basename(msi), signature: readFileSync(`${msi}.sig`, 'utf8'), publishedAt: new Date() })
  writeFileSync(out, `${JSON.stringify(manifest, null, 2)}\n`)
  console.log(out)
}
