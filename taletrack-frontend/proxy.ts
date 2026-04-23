import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

const protectedPrefixes = ['/dashboard', '/books', '/movies', '/series', '/comics', '/profile'];
const authPrefixes = ['/login', '/register'];

export function proxy(request: NextRequest) {
  const token = request.cookies.get('tt-token')?.value;
  const { pathname } = request.nextUrl;

  const isProtected = protectedPrefixes.some((p) => pathname === p || pathname.startsWith(p + '/'));
  const isAuth = authPrefixes.some((p) => pathname === p || pathname.startsWith(p + '/'));

  if (isProtected && !token) {
    return NextResponse.redirect(new URL('/login', request.url));
  }

  if (isAuth && token) {
    return NextResponse.redirect(new URL('/dashboard', request.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: ['/((?!api|_next/static|_next/image|favicon\\.ico).*)'],
};
