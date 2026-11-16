import { fullDateTime, relativeTime } from '../lib/relativeTime.ts'

/** A `<time>` that reads "2 hours ago" and shows the exact date and time on hover. */
export function RelativeTime({ value }: { value: string }) {
  return (
    <time dateTime={value} title={fullDateTime(value)}>
      {relativeTime(value)}
    </time>
  )
}
