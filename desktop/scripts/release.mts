/**
 * The desktop app's version (ADR-0020): it lives in tauri.conf.json (which names the MSI), Cargo.toml and
 * package.json, and a release tag `desktop-v<version>` must match all three. CI runs this before building:
 *
 *   node scripts/release.mts                   # the three files agree; prints the version
 *   node scripts/release.mts desktop-v0.2.0    # and the tag names that version
 *
 * Node 22.18 or later runs it directly; no dependencies.
 */
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

export const TAG_PREFIX = 'desktop-v'

export interface DeclaredVersions {
  tauriConf: string | undefined
  cargoToml: string | undefined
  packageJson: string | undefined
}

/** The version in Cargo.toml's [package] section, or undefined. */
export function cargoPackageVersion(cargoToml: string): string | undefined {
  const section = cargoToml.split(/^\[/m).find((part) => part.startsWith('package]'))
  return section?.match(/^version\s*=\s*"([^"]+)"/m)?.[1]
}

/** The release version, or the problems that stop a release; `tag` is the pushed tag, if any. */
export function checkVersions(declared: DeclaredVersions, tag?: string): { version: string } | { problems: string[] } {
  const problems: string[] = []
  const { tauriConf, cargoToml, packageJson } = declared
  if (!tauriConf) problems.push('tauri.conf.json has no version.')
  if (!cargoToml) problems.push('Cargo.toml has no [package] version.')
  if (!packageJson) problems.push('package.json has no version.')
  if (tauriConf && !/^\d+\.\d+\.\d+$/.test(tauriConf)) {
    // The MSI's ProductVersion (Windows Installer) takes major.minor.build numbers only.
    problems.push(`tauri.conf.json version "${tauriConf}" is not major.minor.patch.`)
  }
  if (tauriConf && cargoToml && tauriConf !== cargoToml) problems.push(`Cargo.toml says ${cargoToml}, tauri.conf.json ${tauriConf}.`)
  if (tauriConf && packageJson && tauriConf !== packageJson) problems.push(`package.json says ${packageJson}, tauri.conf.json ${tauriConf}.`)
  if (tag !== undefined && tauriConf && tag !== `${TAG_PREFIX}${tauriConf}`) {
    problems.push(`Tag ${tag} does not match the version ${tauriConf}; expected ${TAG_PREFIX}${tauriConf}.`)
  }
  return problems.length > 0 || !tauriConf ? { problems } : { version: tauriConf }
}

function readDeclared(desktopDir: string): DeclaredVersions {
  const json = (file: string) => JSON.parse(readFileSync(join(desktopDir, file), 'utf8')) as { version?: string }
  return {
    tauriConf: json('src-tauri/tauri.conf.json').version,
    cargoToml: cargoPackageVersion(readFileSync(join(desktopDir, 'src-tauri/Cargo.toml'), 'utf8')),
    packageJson: json('package.json').version,
  }
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const result = checkVersions(readDeclared(join(dirname(fileURLToPath(import.meta.url)), '..')), process.argv[2])
  if ('problems' in result) {
    for (const problem of result.problems) console.error(problem)
    process.exit(1)
  }
  console.log(result.version)
}
