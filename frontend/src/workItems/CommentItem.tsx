import { useEffect, useId, useRef, useState, type FormEvent } from 'react'
import { ProblemError } from '../api/problem.ts'
import { MarkdownText } from '../components/MarkdownText.tsx'
import { RelativeTime } from '../components/RelativeTime.tsx'
import { TextAreaField } from '../components/TextAreaField.tsx'
import { Button } from '../components/ui/Button.tsx'
import { messages } from '../i18n/messages.ts'
import { useDeleteComment, useEditComment, type Comment } from './useComments.ts'

const text = messages.comments

export const COMMENT_MAX_LENGTH = 20_000

type Mode = 'view' | 'edit' | 'confirm-delete'

interface CommentItemProps {
  comment: Comment
  workItemId: string
  /** The current user wrote it, so they may edit or delete it (US-12 AC2). */
  mine: boolean
  /** A change was saved at `at` (server time); the page announces `message` and waits for its history event. */
  onChanged: (message: string, at: string) => void
  /** The comment is gone: focus moves out of it. */
  onGone: () => void
}

/** One comment: author, time, "edited", the Markdown body, and for its author Edit and Delete (US-12 AC1, AC2). */
export function CommentItem({ comment, workItemId, mine, onChanged, onGone }: CommentItemProps) {
  const headerId = useId()
  const [mode, setMode] = useState<Mode>('view')
  const [draft, setDraft] = useState(comment.bodyMd)
  const [fieldError, setFieldError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const edit = useEditComment(workItemId)
  const remove = useDeleteComment(workItemId)
  const editButton = useRef<HTMLButtonElement>(null)
  const deleteButton = useRef<HTMLButtonElement>(null)
  const box = useRef<HTMLTextAreaElement>(null)
  const keep = useRef<HTMLButtonElement>(null)
  // Where focus returns when a mode closes: the button that opened it (rendered anew, so found again by name).
  const returnTo = useRef<'edit' | 'delete' | null>(null)

  useEffect(() => {
    if (mode === 'edit') box.current?.focus()
    else if (mode === 'confirm-delete') keep.current?.focus()
    else if (returnTo.current) (returnTo.current === 'edit' ? editButton : deleteButton).current?.focus()
  }, [mode])

  function open(next: Mode) {
    returnTo.current = next === 'edit' ? 'edit' : 'delete'
    setNotice(null)
    setFieldError(null)
    if (next === 'edit') setDraft(comment.bodyMd)
    setMode(next)
  }

  function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (edit.isPending) return
    if (draft.trim() === '') {
      setFieldError(text.required)
      box.current?.focus()
      return
    }

    setFieldError(null)
    edit.mutate(
      { comment, bodyMd: draft },
      {
        onSuccess: (saved) => {
          setMode('view')
          onChanged(text.saved, saved.updatedAt)
        },
        onError: (failure) => {
          const problem = failure instanceof ProblemError ? failure.problem : null
          const bodyError = problem?.errors?.bodyMd?.[0]
          if (bodyError) {
            setFieldError(bodyError)
            box.current?.focus()
            return
          }

          if (problem?.status === 404) {
            // Deleted meanwhile: the row goes away with the next read, so say so where it stays heard. The delete's
            // event is already in the history or on its way after this comment's last change.
            onChanged(text.gone, comment.updatedAt)
            onGone()
            return
          }

          // Changed meanwhile (412): the draft stays; the re-read brings the new etag, so Save again overwrites.
          setNotice(problem?.status === 412 ? text.stale : text.saveFailed)
          box.current?.focus()
        },
      },
    )
  }

  function confirmDelete() {
    if (remove.isPending) return
    remove.mutate(comment, {
      onSuccess: (deleted) => {
        onChanged(text.deleted, deleted.deletedAt)
        onGone()
      },
      onError: (failure) => {
        // Already deleted (404) is what was asked for.
        if (failure instanceof ProblemError && failure.problem.status === 404) {
          onChanged(text.deleted, comment.updatedAt)
          onGone()
          return
        }

        setNotice(text.deleteFailed)
        setMode('view')
      },
    })
  }

  return (
    <li className="grid gap-1 rounded-xl border border-border px-3 py-2">
      <div className="flex flex-wrap items-baseline justify-between gap-x-2">
        <p id={headerId} className="flex flex-wrap items-baseline gap-x-2 text-sm">
          <span className="font-medium">{comment.author?.displayName ?? text.formerMember}</span>
          <span className="text-muted-foreground">
            <RelativeTime value={comment.createdAt} />
          </span>
          {comment.edited && <span className="text-muted-foreground">{text.edited}</span>}
        </p>
        {mine && mode === 'view' && (
          <span className="flex gap-1">
            <Button ref={editButton} variant="ghost" size="sm" aria-describedby={headerId} onClick={() => open('edit')}>
              {text.edit}
            </Button>
            <Button ref={deleteButton} variant="ghost" size="sm" aria-describedby={headerId} onClick={() => open('confirm-delete')}>
              {text.delete}
            </Button>
          </span>
        )}
      </div>

      {notice && (
        <p role="alert" className="text-sm text-destructive">
          {notice}
        </p>
      )}

      {mode === 'edit' ? (
        <form onSubmit={save} noValidate className="grid gap-2">
          <TextAreaField
            ref={box}
            label={text.editLabel}
            name="bodyMd"
            maxLength={COMMENT_MAX_LENGTH}
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            hint={text.hint}
            error={fieldError ?? undefined}
            className="min-h-20"
          />
          <span className="flex justify-end gap-2">
            <Button type="button" variant="outline" onClick={() => setMode('view')} disabled={edit.isPending}>
              {text.cancel}
            </Button>
            <Button type="submit" disabled={edit.isPending}>
              {edit.isPending ? text.saving : text.save}
            </Button>
          </span>
        </form>
      ) : (
        <MarkdownText>{comment.bodyMd}</MarkdownText>
      )}

      {mode === 'confirm-delete' && (
        <div role="group" aria-label={text.confirmDelete} className="flex flex-wrap items-center justify-end gap-2 text-sm">
          <span>{text.confirmDelete}</span>
          <Button ref={keep} variant="outline" size="sm" onClick={() => setMode('view')} disabled={remove.isPending}>
            {text.keep}
          </Button>
          <Button size="sm" onClick={confirmDelete} disabled={remove.isPending}>
            {remove.isPending ? text.deleting : text.confirmDeleteButton}
          </Button>
        </div>
      )}
    </li>
  )
}
