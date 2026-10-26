/** User-facing strings in one place, so they can move to i18next later without touching components. */
export const messages = {
  appName: 'Trekky',
  common: {
    email: 'Email',
    password: 'Password',
    unexpectedError: 'Something went wrong. Please try again.',
    signingIn: 'Signing you in…',
    goToLogIn: 'Go to log in',
  },
  signUp: {
    title: 'Create your Trekky account',
    organization: 'Organization',
    passwordHint: 'At least 12 characters',
    submit: 'Create account',
    submitting: 'Creating account…',
    haveAccount: 'Have an account?',
    logInLink: 'Log in',
  },
  logIn: {
    title: 'Log in to Trekky',
    submit: 'Log in',
    submitting: 'Logging in…',
    noAccount: 'No account?',
    signUpLink: 'Create one',
  },
  shell: {
    logOut: 'Log out',
    loggingOut: 'Logging out…',
    mainNav: 'Main',
  },
  /** Labels for the API's role values (`owner`, `admin`, ...); unknown values are shown as sent. */
  roles: { owner: 'Owner', admin: 'Admin', member: 'Member', viewer: 'Viewer' } as Record<string, string | undefined>,
  home: {
    title: 'Projects',
    welcome: (organization: string, role: string) => `You are the ${role} of ${organization}.`,
    empty: 'No projects yet. Projects arrive in the next version.',
  },
  notFound: {
    title: 'Page not found',
    backHome: 'Go to the start page',
  },
} as const
