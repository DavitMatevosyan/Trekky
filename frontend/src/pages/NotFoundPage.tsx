import { Link } from 'react-router'
import { messages } from '../i18n/messages.ts'

export function NotFoundPage() {
  return (
    <main className="p-8 text-center">
      <h1 className="mb-4 text-xl font-semibold">{messages.notFound.title}</h1>
      <Link to="/" className="text-primary underline underline-offset-4">
        {messages.notFound.backHome}
      </Link>
    </main>
  )
}
