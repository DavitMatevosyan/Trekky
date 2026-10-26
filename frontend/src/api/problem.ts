/** RFC 9457 problem details, as the API returns them (validation problems add `errors`). */
export interface ProblemDetails {
  title?: string | null
  detail?: string | null
  status?: number | null
  errors?: Record<string, string[]>
}

export function asProblem(value: unknown): ProblemDetails {
  return typeof value === 'object' && value !== null ? (value as ProblemDetails) : {}
}

/** First validation message per field, keyed by the camelCase field name. */
export function fieldErrors(problem: ProblemDetails): Record<string, string> {
  const result: Record<string, string> = {}
  for (const [key, messages] of Object.entries(problem.errors ?? {})) {
    const first = messages[0]
    if (first) result[key.charAt(0).toLowerCase() + key.slice(1)] = first
  }
  return result
}

/**
 * The message to show above the form: the problem's detail, or for a validation problem the first error that
 * has no field of its own on the form (so no message is ever lost).
 */
export function formLevelError(problem: ProblemDetails, formFields: readonly string[]): string | null {
  if (!problem.errors) return problem.detail ?? problem.title ?? null
  const unmatched = Object.entries(fieldErrors(problem)).find(([field]) => !formFields.includes(field))
  return unmatched?.[1] ?? null
}

/** Thrown by mutations when the API answers with a problem, so forms can show its message and field errors. */
export class ProblemError extends Error {
  readonly problem: ProblemDetails

  constructor(problem: ProblemDetails) {
    super(problem.detail ?? problem.title ?? 'Request failed')
    this.name = 'ProblemError'
    this.problem = problem
  }
}
