import { act, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beginAuthorization } from './auth/oauthClient.ts'
import { jsonResponse, me, problemResponse, renderApp, requestAt, validTokens } from './test/renderApp.tsx'

vi.mock('./auth/oauthClient.ts', async (importOriginal) => ({
  ...(await importOriginal<typeof import('./auth/oauthClient.ts')>()),
  beginAuthorization: vi.fn().mockResolvedValue(undefined),
}))

describe('App', () => {
  beforeEach(() => vi.mocked(beginAuthorization).mockClear())

  describe('protected pages', () => {
    it('re-authenticate silently from the session cookie when no tokens are in memory', async () => {
      renderApp({ path: '/?tab=1' })

      expect(screen.getByRole('status')).toHaveTextContent('Signing you in…')
      await waitFor(() => expect(beginAuthorization).toHaveBeenCalledWith({ prompt: 'none', returnTo: '/?tab=1' }))
    })

    it('show the organization and role of the logged-in user', async () => {
      const fetchFn = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(me))
      renderApp({ tokens: validTokens, fetchFn })

      expect(await screen.findByText('You are the Owner of Acme.')).toBeInTheDocument()
      expect(requestAt(fetchFn, 0).headers.get('Authorization')).toBe(`Bearer ${validTokens.accessToken}`)
    })

    it('log out: revoke on the server, forget the tokens and show the login page', async () => {
      const fetchFn = vi
        .fn<typeof fetch>()
        .mockResolvedValueOnce(jsonResponse(me))
        .mockResolvedValueOnce(new Response(null, { status: 204 }))
      const { session, router } = renderApp({ tokens: validTokens, fetchFn })
      await screen.findByText('You are the Owner of Acme.')

      await userEvent.click(screen.getByRole('button', { name: 'Log out' }))

      expect(await screen.findByRole('heading', { name: 'Log in to Trekky' })).toBeInTheDocument()
      const logout = requestAt(fetchFn, 1)
      expect(new URL(logout.url).pathname).toBe('/api/v1/auth/logout')
      expect(logout.method).toBe('POST')
      expect(session.status).toBe('loggedOut')

      // The back button must not show the protected page again, nor start a silent sign-in.
      await act(() => router.navigate('/'))
      expect(router.state.location.pathname).toBe('/login')
      expect(screen.getByRole('heading', { name: 'Log in to Trekky' })).toBeInTheDocument()
      expect(beginAuthorization).not.toHaveBeenCalled()
    })
  })

  describe('log in', () => {
    async function submitLogin(email = 'ada@example.com', password = 'wrong-password-123') {
      await userEvent.type(screen.getByLabelText('Email'), email)
      await userEvent.type(screen.getByLabelText('Password'), password)
      await userEvent.click(screen.getByRole('button', { name: 'Log in' }))
    }

    it('shows the generic message for wrong credentials', async () => {
      const fetchFn = vi
        .fn<typeof fetch>()
        .mockResolvedValue(problemResponse(401, { title: 'Login failed', detail: 'Email or password is incorrect.' }))
      renderApp({ path: '/login', fetchFn })

      await submitLogin()

      expect(await screen.findByRole('alert')).toHaveTextContent('Email or password is incorrect.')
      expect(beginAuthorization).not.toHaveBeenCalled()
    })

    it('shows validation errors next to their fields', async () => {
      const fetchFn = vi
        .fn<typeof fetch>()
        .mockResolvedValue(problemResponse(400, { title: 'Validation failed', errors: { Email: ['Email is required.'] } }))
      renderApp({ path: '/login', fetchFn })

      await userEvent.click(screen.getByRole('button', { name: 'Log in' }))

      const email = screen.getByLabelText('Email')
      expect(await screen.findByText('Email is required.')).toBeInTheDocument()
      expect(email).toHaveAttribute('aria-invalid', 'true')
      expect(email).toHaveAccessibleDescription('Email is required.')
    })

    it('shows a validation error that has no field on the form', async () => {
      const fetchFn = vi
        .fn<typeof fetch>()
        .mockResolvedValue(problemResponse(400, { title: 'Validation failed', errors: { Other: ['Something is off.'] } }))
      renderApp({ path: '/login', fetchFn })

      await userEvent.click(screen.getByRole('button', { name: 'Log in' }))

      expect(await screen.findByRole('alert')).toHaveTextContent('Something is off.')
    })

    it('starts the code flow after the API accepts the credentials, returning where the user was headed', async () => {
      const fetchFn = vi.fn<typeof fetch>().mockResolvedValue(new Response(null, { status: 204 }))
      renderApp({ path: '/login?returnTo=%2Fprojects', fetchFn })

      await submitLogin('ada@example.com', 'correct-horse-battery')

      await waitFor(() => expect(beginAuthorization).toHaveBeenCalledWith({ returnTo: '/projects' }))
      const request = requestAt(fetchFn, 0)
      expect(new URL(request.url).pathname).toBe('/api/v1/auth/login')
      expect(await request.json()).toEqual({ email: 'ada@example.com', password: 'correct-horse-battery' })
      expect(screen.getByRole('button', { name: 'Logging in…' })).toBeDisabled()
    })
  })

  describe('sign up', () => {
    async function submitSignUp() {
      await userEvent.type(screen.getByLabelText('Email'), 'ada@example.com')
      await userEvent.type(screen.getByLabelText('Password'), 'correct-horse-battery')
      await userEvent.type(screen.getByLabelText('Organization'), 'Acme')
      await userEvent.click(screen.getByRole('button', { name: 'Create account' }))
    }

    it('explains the password rule', () => {
      renderApp({ path: '/sign-up' })

      expect(screen.getByLabelText('Password')).toHaveAccessibleDescription('At least 12 characters')
    })

    it('shows a taken email', async () => {
      const fetchFn = vi.fn<typeof fetch>().mockResolvedValue(
        problemResponse(409, { title: 'Email already registered', detail: 'An account with this email already exists.' }),
      )
      renderApp({ path: '/sign-up', fetchFn })

      await submitSignUp()

      expect(await screen.findByRole('alert')).toHaveTextContent('An account with this email already exists.')
    })

    it('creates the account and signs the user in, landing on the start page', async () => {
      const fetchFn = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(me, 201))
      renderApp({ path: '/sign-up', fetchFn })

      await submitSignUp()

      await waitFor(() => expect(beginAuthorization).toHaveBeenCalledWith({ returnTo: '/' }))
      expect(await requestAt(fetchFn, 0).json()).toEqual({
        email: 'ada@example.com',
        password: 'correct-horse-battery',
        organizationName: 'Acme',
      })
    })
  })

  it('shows a not-found page for unknown paths', () => {
    renderApp({ path: '/nope' })

    expect(screen.getByRole('heading', { name: 'Page not found' })).toBeInTheDocument()
  })
})
