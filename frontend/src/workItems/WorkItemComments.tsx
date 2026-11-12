import { useId, useRef, useState, type FormEvent } from 'react'
import { ProblemError } from '../api/problem.ts'
import { useMe } from '../auth/useMe.ts'
import { TextAreaField } from '../components/TextAreaField.tsx'
import { Button } from '../components/ui/Button.tsx'
import { messages } from '../i18n/messages.ts'
import { COMMENT_MAX_LENGTH, CommentItem } from './CommentItem.tsx'
import { useAddComment, useComments } from './useComments.ts'

const text = messages.comments

/**
 * The item's comments, oldest first with author and time, a Markdown box to add one, and the author's edit and delete
 * (US-12, wireframes: Item detail). `onChanged` gets what to announce and when the change was saved; the page announces
 * it in its one status region and waits for its history event.
 */
export function WorkItemComments({ workItemId, onChanged }: { workItemId: string; onChanged: (message: string, at: string) => void }) {
  const headingId = useId()
  const heading = useRef<HTMLHeadingElement>(null)
  const me = useMe()
  const comments = useComments(workItemId)
  const add = useAddComment(workItemId)
  const [body, setBody] = useState('')
  const [error, setError] = useState<string | null>(null)
  const box = useRef<HTMLTextAreaElement>(null)
  const items = comments.data?.pages.flatMap((page) => page.items) ?? []

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (add.isPending) return
    if (body.trim() === '') {
      setError(text.required)
      box.current?.focus()
      return
    }

    setError(null)
    add.mutate(body, {
      onSuccess: (comment) => {
        setBody('')
        onChanged(text.posted, comment.createdAt)
      },
      onError: (failure) => {
        const fieldError = failure instanceof ProblemError ? failure.problem.errors?.bodyMd?.[0] : undefined
        setError(fieldError ?? null)
        if (fieldError) box.current?.focus()
      },
    })
  }

  const postFailed = add.isError && error === null

  return (
    <section aria-labelledby={headingId} className="grid gap-3">
      <h2 id={headingId} ref={heading} tabIndex={-1} className="font-semibold">
        {text.title}
      </h2>

      {comments.isPending && <p className="text-sm text-muted-foreground">{text.loading}</p>}
      {comments.isError && !comments.data && <p role="alert">{text.loadFailed}</p>}
      {comments.isSuccess && items.length === 0 && <p className="text-sm text-muted-foreground">{text.empty}</p>}

      {items.length > 0 && (
        <ul aria-labelledby={headingId} className="grid gap-3">
          {items.map((comment) => (
            <CommentItem
              key={comment.id}
              comment={comment}
              workItemId={workItemId}
              mine={comment.authorId === me.data?.user.id}
              onChanged={onChanged}
              // The comment and its buttons are gone; focus stays in the section (WCAG 2.4.3).
              onGone={() => heading.current?.focus()}
            />
          ))}
        </ul>
      )}

      {comments.isFetchNextPageError && <p role="alert">{text.loadFailed}</p>}
      {comments.hasNextPage && (
        <Button variant="outline" className="justify-self-center" onClick={() => void comments.fetchNextPage()} disabled={comments.isFetchingNextPage}>
          {text.loadMore}
        </Button>
      )}

      <form onSubmit={submit} noValidate className="grid gap-2">
        <TextAreaField
          ref={box}
          label={text.label}
          name="bodyMd"
          maxLength={COMMENT_MAX_LENGTH}
          value={body}
          onChange={(event) => setBody(event.target.value)}
          hint={text.hint}
          error={error ?? undefined}
          className="min-h-20"
        />
        {postFailed && (
          <p role="alert" className="rounded-md border border-destructive/50 bg-destructive/10 px-3 py-2 text-sm text-destructive">
            {text.postFailed}
          </p>
        )}
        <Button type="submit" className="justify-self-end" disabled={add.isPending}>
          {add.isPending ? text.posting : text.post}
        </Button>
      </form>
    </section>
  )
}
