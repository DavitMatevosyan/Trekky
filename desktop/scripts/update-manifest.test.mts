import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { updateManifest } from './update-manifest.mts'

const input = {
  version: '0.2.0',
  tag: 'desktop-v0.2.0',
  msiName: 'Trekky_0.2.0_x64_en-US.msi',
  signature: 'c2lnbmF0dXJl\n',
  publishedAt: new Date('2026-11-16T12:00:00Z'),
}

describe('updateManifest', () => {
  it('points the Windows entry at the MSI on the tagged release, with its signature', () => {
    assert.deepEqual(updateManifest(input), {
      version: '0.2.0',
      notes: 'Trekky for Windows 0.2.0',
      pub_date: '2026-11-16T12:00:00.000Z',
      platforms: {
        'windows-x86_64': {
          signature: 'c2lnbmF0dXJl',
          url: 'https://github.com/DavitMatevosyan/Trekky/releases/download/desktop-v0.2.0/Trekky_0.2.0_x64_en-US.msi',
        },
      },
    })
  })

  it('refuses an empty signature, so an unsigned build never reaches the feed', () => {
    assert.throws(() => updateManifest({ ...input, signature: ' \n' }), /signature is empty/)
  })
})
