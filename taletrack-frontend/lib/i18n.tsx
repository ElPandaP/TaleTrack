'use client';

import { createContext, useCallback, useContext, useMemo, useState } from 'react';
import en from '@/messages/en.json';
import es from '@/messages/es.json';
import { type Locale } from './i18n-shared';

export { LOCALES, DEFAULT_LOCALE, isLocale, localeFromHeader, pickTitle, type Locale } from './i18n-shared';

const DICTS: Record<Locale, Record<string, string>> = { en, es };

type Params = Record<string, string | number>;

interface I18nContext {
  locale: Locale;
  setLocale: (l: Locale) => void;
  /** Translate a key. `{name}` placeholders are filled from `params`. */
  t: (key: string, params?: Params) => string;
  /** Plural helper: uses `<key>.one` / `<key>.other`, injects `count`. */
  tp: (key: string, count: number, params?: Params) => string;
}

const Ctx = createContext<I18nContext | null>(null);

function interpolate(msg: string, params?: Params) {
  if (!params) return msg;
  let out = msg;
  for (const [k, v] of Object.entries(params)) out = out.replaceAll(`{${k}}`, String(v));
  return out;
}

export function LocaleProvider({
  initialLocale,
  children,
}: {
  initialLocale: Locale;
  children: React.ReactNode;
}) {
  const [locale, setLocaleState] = useState<Locale>(initialLocale);

  const setLocale = useCallback((l: Locale) => {
    setLocaleState(l);
    document.cookie = `tt-locale=${l}; path=/; max-age=${60 * 60 * 24 * 365}; SameSite=Lax`;
    try {
      localStorage.setItem('tt-locale', l);
    } catch {
      /* private mode */
    }
    document.documentElement.lang = l;
  }, []);

  const value = useMemo<I18nContext>(() => {
    const t = (key: string, params?: Params) =>
      interpolate(DICTS[locale][key] ?? DICTS.en[key] ?? key, params);
    const tp = (key: string, count: number, params?: Params) =>
      t(`${key}.${count === 1 ? 'one' : 'other'}`, { count, ...params });
    return { locale, setLocale, t, tp };
  }, [locale, setLocale]);

  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

export function useI18n(): I18nContext {
  const ctx = useContext(Ctx);
  if (!ctx) throw new Error('useI18n must be used inside <LocaleProvider>');
  return ctx;
}

/** Shorthand when you only need the translate function. */
export function useT() {
  return useI18n().t;
}
