/**
 * Where the session tokens are kept, shared by the browser client, the auth context, the proxy
 * and the Server Components.
 *
 * @remarks
 * The browser keeps both tokens in `localStorage` and mirrors them into cookies, so the proxy and
 * Server Components can read the session too.
 *
 * @module
 */

/** `localStorage` key of the access token. */
export const ACCESS_TOKEN_KEY = 'token';
/** `localStorage` key of the refresh token. */
export const REFRESH_TOKEN_KEY = 'tt-refresh';
/** Cookie holding the access token. */
export const ACCESS_TOKEN_COOKIE = 'tt-token';
/** Cookie holding the refresh token. */
export const REFRESH_TOKEN_COOKIE = 'tt-refresh';
/** Lifetime of both cookies, in seconds (30 days). */
export const AUTH_COOKIE_MAX_AGE = 60 * 60 * 24 * 30;
