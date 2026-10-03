/**
 * i18n helpers for Server Components.
 *
 * @remarks
 * This module imports `next/headers`, so it must never be imported from a Client Component.
 * Pieces shared by both sides live in `i18n-shared.ts`; the client provider and hooks live
 * in `i18n.tsx`.
 *
 * @module
 */

import { cookies, headers } from 'next/headers';
import en from '@/messages/en.json';
import es from '@/messages/es.json';
import { isLocale, localeFromHeader, type Locale } from './i18n-shared';

const DICTS: Record<Locale, Record<string, string>> = { en, es };

/**
 * Resolves the visitor's locale from the `tt-locale` cookie, falling back to the
 * `Accept-Language` header.
 */
export async function getServerLocale(): Promise<Locale> {
  const cookie = (await cookies()).get('tt-locale')?.value;
  if (isLocale(cookie)) return cookie;
  return localeFromHeader((await headers()).get('accept-language'));
}

/**
 * Returns a translator for the visitor's locale, the Server Component counterpart of `useT()`.
 * A key missing from the dictionary is returned as is.
 */
export async function getServerT(): Promise<(key: string) => string> {
  const dict = DICTS[await getServerLocale()];
  return (key) => dict[key] ?? key;
}
