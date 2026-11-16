/** A form-level error, announced to screen readers as soon as it appears. */
export function FormError({ message }: { message: string | null }) {
  return (
    <div role="alert">
      {message && (
        <p className="rounded-md border border-destructive/50 bg-destructive/10 px-3 py-2 text-sm text-destructive">{message}</p>
      )}
    </div>
  )
}
