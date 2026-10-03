/**
 * Client-side configuration. The `NEXT_PUBLIC_*` values come from environment variables, with
 * defaults that work for local development.
 */
export const API_CONFIG = {
  /**
   * Base URL of the backend API as seen by the browser. Always same-origin: Next.js (development)
   * or Caddy (production) forwards `/api` to the backend.
   */
  baseURL: '/api',
  /** OAuth client id for the "Sign in with Google" button. */
  googleClientId: process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID || '',
  /**
   * Id of the Netflix browser extension that `/extension-auth` sends tokens to. The default is
   * the id derived from the `key` committed in the extension's manifest.
   */
  netflixExtensionId: process.env.NEXT_PUBLIC_EXTENSION_ID || 'kbhjoofgffidokbnlklekelkpdhcllih',
};
