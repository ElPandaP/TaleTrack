// Framework-agnostic i18n helpers — safe to import from Server AND Client
// Components. Server-only helpers (which need `next/headers`) live in
// ./i18n-server.ts instead, so this file stays safe for the client bundle.

export const LOCALES = ['en', 'es'] as const;
export type Locale = (typeof LOCALES)[number];
export const DEFAULT_LOCALE: Locale = 'en';

export function isLocale(v: string | undefined | null): v is Locale {
  return v === 'en' || v === 'es';
}

/** Best guess from an Accept-Language header (server-side default). */
export function localeFromHeader(header: string | null): Locale {
  return header?.toLowerCase().trimStart().startsWith('es') ? 'es' : 'en';
}
