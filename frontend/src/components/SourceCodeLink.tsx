import type { MouseEvent } from 'react'
import { messages } from '../i18n/messages.ts'
import { SOURCE_CODE_URL } from '../lib/sourceCode.ts'
import { usePlatform } from '../platform/PlatformContext.ts'

/** The link to Trekky's source code (AGPL-3.0, ADR-0021); the desktop app opens it in the system browser. */
export function SourceCodeLink() {
  const { links } = usePlatform()

  function open(event: MouseEvent<HTMLAnchorElement>) {
    if (links.open(SOURCE_CODE_URL)) event.preventDefault()
  }

  return (
    <a
      href={SOURCE_CODE_URL}
      target="_blank"
      rel="noreferrer"
      onClick={open}
      className="underline underline-offset-4 hover:no-underline"
    >
      {messages.shell.sourceCode}
    </a>
  )
}
