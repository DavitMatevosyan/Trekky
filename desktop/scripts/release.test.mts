import assert from 'node:assert/strict'
import { test } from 'node:test'
import { cargoPackageVersion, checkVersions } from './release.mts'

const same = { tauriConf: '0.2.0', cargoToml: '0.2.0', packageJson: '0.2.0' }

test('the version is released when the three files agree and the tag names it', () => {
  assert.deepEqual(checkVersions(same), { version: '0.2.0' })
  assert.deepEqual(checkVersions(same, 'desktop-v0.2.0'), { version: '0.2.0' })
})

test('a tag for another version is refused', () => {
  assert.deepEqual(checkVersions(same, 'desktop-v0.3.0'), {
    problems: ['Tag desktop-v0.3.0 does not match the version 0.2.0; expected desktop-v0.2.0.'],
  })
  assert.ok('problems' in checkVersions(same, 'v0.2.0'))
})

test('files that disagree are refused, each named', () => {
  assert.deepEqual(checkVersions({ tauriConf: '0.2.0', cargoToml: '0.1.0', packageJson: '0.2.1' }), {
    problems: ['Cargo.toml says 0.1.0, tauri.conf.json 0.2.0.', 'package.json says 0.2.1, tauri.conf.json 0.2.0.'],
  })
})

test('a missing or pre-release version is refused, because the MSI takes numbers only', () => {
  assert.ok('problems' in checkVersions({ ...same, tauriConf: undefined }))
  assert.ok('problems' in checkVersions({ ...same, cargoToml: undefined }))
  assert.deepEqual(checkVersions({ tauriConf: '0.2.0-beta.1', cargoToml: '0.2.0-beta.1', packageJson: '0.2.0-beta.1' }), {
    problems: ['tauri.conf.json version "0.2.0-beta.1" is not major.minor.patch.'],
  })
})

test('Cargo.toml version comes from [package], not a dependency', () => {
  const toml = [
    '[package]',
    'name = "trekky-desktop"',
    'version = "0.4.1"',
    '',
    '[dependencies]',
    'tauri = { version = "=2.12.5" }',
    'version = "9.9.9"',
  ].join('\n')
  assert.equal(cargoPackageVersion(toml), '0.4.1')
  assert.equal(cargoPackageVersion('[dependencies]\nversion = "1.0.0"\n'), undefined)
})
