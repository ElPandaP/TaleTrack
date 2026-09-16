'use client';

import { LOCALES, useI18n } from '@/lib/i18n';
import { cn } from '@/lib/utils';

export default function LocaleToggle() {
  const { locale, setLocale } = useI18n();

  return (
    <div
      className="inline-flex rounded-full border border-border bg-secondary/50 p-0.5 text-[11px] font-semibold"
      role="group"
      aria-label="Language"
    >
      {LOCALES.map((l) => (
        <button
          key={l}
          type="button"
          onClick={() => setLocale(l)}
          aria-pressed={locale === l}
          className={cn(
            'cursor-pointer rounded-full px-1.5 py-0.5 uppercase transition-colors',
            locale === l ? 'bg-card text-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground',
          )}
        >
          {l}
        </button>
      ))}
    </div>
  );
}
