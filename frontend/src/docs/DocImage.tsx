import { messages } from '../i18n/messages.ts'
import { useDocImage } from './useDocs.ts'

/** An image from the docs folder, loaded through the API with the user's session (TREK-75). */
export function DocImage({ projectId, path, alt, title }: { projectId: string; path: string; alt: string; title?: string }) {
  const image = useDocImage(projectId, path)

  if (image.isPending) {
    return <span className="text-sm text-muted-foreground">{messages.docs.image.loading(alt)}</span>
  }
  if (image.isError) {
    return <span className="text-sm text-muted-foreground">{messages.docs.image.failed(alt)}</span>
  }
  return <img src={image.data} alt={alt} title={title} className="inline-block h-auto max-w-full" />
}
