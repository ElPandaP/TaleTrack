import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';
import { isJwtValid } from '@/lib/jwt';

const protectedPrefixes = [
  '/dashboard', '/library', '/reviews', '/activity', '/friends', '/profile', '/media', '/u',
  '/connections',
];
const authPrefixes = ['/login', '/register'];

const API_BASE = process.env.INTERNAL_API_URL ?? 'http://localhost:8080/api';
const COOKIE_MAX_AGE = 60 * 60 * 24 * 30; // 30 days

function setAuthCookies(res: NextResponse, token: string, refreshToken: string) {
  const opts = { path: '/', sameSite: 'lax' as const, maxAge: COOKIE_MAX_AGE };
  res.cookies.set('tt-token', token, opts);
  res.cookies.set('tt-refresh', refreshToken, opts);
}

function clearAuthCookies(res: NextResponse) {
  res.cookies.delete('tt-token');
  res.cookies.delete('tt-refresh');
}

/** Exchange the refresh cookie for a fresh access token, server-side, before the
 *  request reaches the RSC tree — so a >60-min-old session survives a hard navigation. */
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

export async function proxy(request: NextRequest) {
  const rawToken = request.cookies.get('tt-token')?.value;
  const refreshToken = request.cookies.get('tt-refresh')?.value;
  const { pathname } = request.nextUrl;

  const isProtected = protectedPrefixes.some((p) => pathname === p || pathname.startsWith(p + '/'));
  const isAuth = authPrefixes.some((p) => pathname === p || pathname.startsWith(p + '/'));

  let authed = isJwtValid(rawToken);
  let refreshed: { token: string; refreshToken: string } | null = null;

  // Access token stale/missing but we hold a refresh token: rotate it now.
  if (!authed && refreshToken) {
    refreshed = await tryRefresh(refreshToken);
    if (refreshed) {
      authed = true;
      // Make the rotated token visible to RSC handlers in this same pass.
      request.cookies.set('tt-token', refreshed.token);
      request.cookies.set('tt-refresh', refreshed.refreshToken);
    }
  }

  const withRefreshedCookies = (res: NextResponse) => {
    if (refreshed) setAuthCookies(res, refreshed.token, refreshed.refreshToken);
    return res;
  };

  // Had a token (or refresh token) but neither is usable: strip and behave as logged-out.
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

export const config = {
  matcher: ['/((?!api|_next/static|_next/image|favicon\\.ico).*)'],
};
