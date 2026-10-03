/**
 * Framework-agnostic i18n helpers, safe to import from both Server and Client Components.
 *
 * @remarks
 * Neither of the other two i18n modules can serve both sides: `i18n.tsx` is marked
 * `'use client'`, so a Server Component importing a plain function from it gets a client
 * reference and fails when calling it; `i18n-server.ts` imports `next/headers`, which breaks
 * the client bundle. Anything needed on both sides belongs here, with no `'use client'` and
 * no server-only imports.
 *
 * @module
 */

/** Every locale the site is translated into. */
export const LOCALES = ['en', 'es'] as const;
/** A supported locale code. */
export type Locale = (typeof LOCALES)[number];
/** Locale used when nothing else (cookie, header) says otherwise. */
export const DEFAULT_LOCALE: Locale = 'en';

/** Type guard: is `v` one of the supported {@link Locale} codes? */
export function isLocale(v: string | undefined | null): v is Locale {
  return v === 'en' || v === 'es';
}

/**
 * Guesses the locale from an `Accept-Language` header by looking at its first language only.
 *
 * @param header - The raw header value, or `null` when absent.
 * @returns The matching locale, or {@link DEFAULT_LOCALE}.
 */
export function localeFromHeader(header: string | null): Locale {
  const lang = header?.toLowerCase().trimStart().slice(0, 2);
  return isLocale(lang) ? lang : DEFAULT_LOCALE;
}

/**
 * Picks the media title to show for a locale. The backend sends separate English and Spanish
 * titles; the one matching the locale wins, falling back to whichever one is set.
 *
 * @param titleEN - English title, if known.
 * @param titleES - Spanish title, if known.
 * @param locale - The viewer's locale.
 * @returns The chosen title, or an empty string when neither is set.
 */
export function pickTitle(
  titleEN: string | null | undefined,
  titleES: string | null | undefined,
  locale: Locale,
): string {
  const preferred = locale === 'es' ? titleES : titleEN;
  const fallback = locale === 'es' ? titleEN : titleES;
  return preferred || fallback || '';
}
