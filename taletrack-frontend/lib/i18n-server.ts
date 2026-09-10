// Server Component-only i18n helpers — import `next/headers`, so this file
// must never be pulled into the client bundle. See ./i18n-shared.ts for the
// framework-agnostic pieces and ./i18n.tsx for the client provider + hooks.

import { cookies, headers } from 'next/headers';
import en from '@/messages/en.json';
import es from '@/messages/es.json';
import { isLocale, localeFromHeader, type Locale } from './i18n-shared';

/** The visitor's locale: `tt-locale` cookie, falling back to Accept-Language. */
export async function getServerLocale(): Promise<Locale> {
  const cookie = (await cookies()).get('tt-locale')?.value;
  if (isLocale(cookie)) return cookie;
  return localeFromHeader((await headers()).get('accept-language'));
}

/** The message dictionary for the visitor's locale, for static Server Component pages. */
export async function getServerDict(): Promise<Record<string, string>> {
  return (await getServerLocale()) === 'es' ? es : en;
}
