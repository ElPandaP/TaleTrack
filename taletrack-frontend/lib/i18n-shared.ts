// Framework-agnostic i18n helpers — safe to import from Server AND Client
// Components. This file exists because neither of the other two i18n modules
// can serve both sides:
// - ./i18n.tsx is marked 'use client', so a Server Component importing a plain
//   function from it gets a client reference and fails when calling it.
// - ./i18n-server.ts imports `next/headers`, which breaks the client bundle.
// Anything that may be needed on both sides (e.g. pickTitle) belongs here, with
// no 'use client' and no server-only imports.

export const LOCALES = ['en', 'es'] as const;
export type Locale = (typeof LOCALES)[number];
export const DEFAULT_LOCALE: Locale = 'en';

export function isLocale(v: string | undefined | null): v is Locale {
  return v === 'en' || v === 'es';
}

/** Best guess from an Accept-Language header (server-side default). */
export function localeFromHeader(header: string | null): Locale {
  const lang = header?.toLowerCase().trimStart().slice(0, 2);
  return isLocale(lang) ? lang : DEFAULT_LOCALE;
}

/** Media titles come from the backend as separate EN/ES fields (see Media.TitleEN/TitleES) —
 * pick the one matching the viewer's locale, falling back to whichever one is set. */
export function pickTitle(
  titleEN: string | null | undefined,
  titleES: string | null | undefined,
  locale: Locale,
): string {
  const preferred = locale === 'es' ? titleES : titleEN;
  const fallback = locale === 'es' ? titleEN : titleES;
  return preferred || fallback || '';
}
