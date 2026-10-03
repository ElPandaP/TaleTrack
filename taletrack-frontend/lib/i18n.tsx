'use client';

import { createContext, useCallback, useContext, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import en from '@/messages/en.json';
import es from '@/messages/es.json';
import { DEFAULT_LOCALE, type Locale } from './i18n-shared';

// Re-exported so Client Components can get every i18n helper from this one module.
export { LOCALES, DEFAULT_LOCALE, isLocale, localeFromHeader, pickTitle, type Locale } from './i18n-shared';

const DICTS: Record<Locale, Record<string, string>> = { en, es };

/** Values for the `{name}` placeholders of a message. */
export type Params = Record<string, string | number>;

/** What {@link useI18n} returns: the current locale, a setter and the translate functions. */
export interface I18nContext {
  /** The active locale. */
  locale: Locale;
  /** Switches the locale and remembers it in the `tt-locale` cookie. */
  setLocale: (l: Locale) => void;
  /** Translates a key, filling `{name}` placeholders from `params`. Unknown keys fall back to English, then to the key itself. */
  t: (key: string, params?: Params) => string;
  /** Plural helper: uses `<key>.one` or `<key>.other` depending on `count`, and passes `count` as a parameter. */
  tp: (key: string, count: number, params?: Params) => string;
}

const Ctx = createContext<I18nContext | null>(null);

/** Replaces every `{key}` placeholder in `msg` with the matching value from `params`. */
function interpolate(msg: string, params?: Params) {
  if (!params) return msg;
  let out = msg;
  for (const [k, v] of Object.entries(params)) out = out.replaceAll(`{${k}}`, String(v));
  return out;
}

/**
 * Provides the active locale and the translate functions to every Client Component below it.
 * The initial locale comes from the server (cookie or `Accept-Language`) so the first render
 * matches the server's.
 */
export function LocaleProvider({
  initialLocale,
  children,
}: {
  initialLocale: Locale;
  children: React.ReactNode;
}) {
  const [locale, setLocaleState] = useState<Locale>(initialLocale);
  const router = useRouter();

  const setLocale = useCallback((l: Locale) => {
    setLocaleState(l);
    document.cookie = `tt-locale=${l}; path=/; max-age=${60 * 60 * 24 * 365}; SameSite=Lax`;
    document.documentElement.lang = l;
    // Server Components (footer, static pages) read the cookie, so re-render them.
    router.refresh();
  }, [router]);

  const value = useMemo<I18nContext>(() => {
    const t = (key: string, params?: Params) =>
      interpolate(DICTS[locale][key] ?? DICTS[DEFAULT_LOCALE][key] ?? key, params);
    const tp = (key: string, count: number, params?: Params) =>
      t(`${key}.${count === 1 ? 'one' : 'other'}`, { count, ...params });
    return { locale, setLocale, t, tp };
  }, [locale, setLocale]);

  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

/**
 * Returns the i18n context: `locale`, `setLocale`, `t` and `tp`.
 *
 * @throws If called outside a {@link LocaleProvider}.
 */
export function useI18n(): I18nContext {
  const ctx = useContext(Ctx);
  if (!ctx) throw new Error('useI18n must be used inside <LocaleProvider>');
  return ctx;
}

/** Shorthand for `useI18n().t`, for components that only need to translate. */
export function useT() {
  return useI18n().t;
}
