/**
 * Next.js middleware (`proxy.ts` in Next 16): server-side route guard and session refresh.
 *
 * @module
 */
import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';
import { isJwtValid } from '@/lib/jwt';
import { ACCESS_TOKEN_COOKIE, AUTH_COOKIE_MAX_AGE, REFRESH_TOKEN_COOKIE } from '@/lib/auth-storage';

/** Routes that need a session; visitors without one are redirected to `/login`. */
const protectedPrefixes = [
  '/library', '/reviews', '/activity', '/friends', '/profile', '/media', '/u',
  '/connections',
];
/** Routes only for visitors without a session; signed-in users are sent to `/`. */
const authPrefixes = ['/login', '/register'];

const API_BASE = process.env.INTERNAL_API_URL ?? 'http://localhost:8080/api';

/** Writes a fresh access and refresh token pair to the response cookies. */
function setAuthCookies(res: NextResponse, token: string, refreshToken: string) {
  const opts = { path: '/', sameSite: 'lax' as const, maxAge: AUTH_COOKIE_MAX_AGE };
  res.cookies.set(ACCESS_TOKEN_COOKIE, token, opts);
  res.cookies.set(REFRESH_TOKEN_COOKIE, refreshToken, opts);
}

/** Deletes both auth cookies from the response. */
function clearAuthCookies(res: NextResponse) {
  res.cookies.delete(ACCESS_TOKEN_COOKIE);
  res.cookies.delete(REFRESH_TOKEN_COOKIE);
}

/**
 * Exchanges the refresh cookie for a new token pair before the request reaches the Server
 * Components, so a session whose access token has expired survives a full page load.
 *
 * @param refreshToken - The value of the `tt-refresh` cookie.
 * @returns The rotated pair, or `null` if the backend rejected it or could not be reached.
 */
async function tryRefresh(refreshToken: string): Promise<{ token: string; refreshToken: string } | null> {
  try {
    const res = await fetch(`${API_BASE}/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken }),
    });
    if (!res.ok) return null;
    const data = (await res.json()) as { token?: string; refreshToken?: string };
    if (!data.token || !data.refreshToken) return null;
    return { token: data.token, refreshToken: data.refreshToken };
  } catch {
    return null;
  }
}

/**
 * Runs before every page request (see {@link config}).
 *
 * @remarks
 * Checks the `tt-token` cookie and, when it is missing or expired but a `tt-refresh` cookie
 * exists, rotates the pair on the spot. Then it redirects anonymous visitors away from
 * protected routes, sends signed-in users away from `/login` and `/register`, and clears
 * cookies that can no longer be used.
 *
 * @param request - The incoming request.
 * @returns A redirect, or a pass-through response carrying any rotated cookies.
 */
export async function proxy(request: NextRequest) {
  const rawToken = request.cookies.get(ACCESS_TOKEN_COOKIE)?.value;
  const refreshToken = request.cookies.get(REFRESH_TOKEN_COOKIE)?.value;
  const { pathname } = request.nextUrl;

  const isProtected = protectedPrefixes.some((p) => pathname === p || pathname.startsWith(p + '/'));
  const isAuth = authPrefixes.some((p) => pathname === p || pathname.startsWith(p + '/'));

  let authed = isJwtValid(rawToken);
  let refreshed: { token: string; refreshToken: string } | null = null;

  // Access token missing or expired but a refresh token is present: rotate the pair now.
  if (!authed && refreshToken) {
    refreshed = await tryRefresh(refreshToken);
    if (refreshed) {
      authed = true;
      // Make the rotated tokens visible to the Server Components rendering this same request.
      request.cookies.set(ACCESS_TOKEN_COOKIE, refreshed.token);
      request.cookies.set(REFRESH_TOKEN_COOKIE, refreshed.refreshToken);
    }
  }

  const withRefreshedCookies = (res: NextResponse) => {
    if (refreshed) setAuthCookies(res, refreshed.token, refreshed.refreshToken);
    return res;
  };

  // Cookies are present but unusable: clear them and treat the visitor as signed out.
  if ((rawToken || refreshToken) && !authed) {
    const response = isProtected
      ? NextResponse.redirect(new URL('/login', request.url))
      : NextResponse.next({ request });
    clearAuthCookies(response);
    return response;
  }

  if (isProtected && !authed) {
    return NextResponse.redirect(new URL('/login', request.url));
  }

  if (isAuth && authed) {
    return withRefreshedCookies(NextResponse.redirect(new URL('/', request.url)));
  }

  return withRefreshedCookies(NextResponse.next({ request }));
}

/** Runs the middleware on every route except the API proxy, Next.js assets and the favicon. */
export const config = {
  matcher: ['/((?!api|_next/static|_next/image|favicon\\.ico).*)'],
};
