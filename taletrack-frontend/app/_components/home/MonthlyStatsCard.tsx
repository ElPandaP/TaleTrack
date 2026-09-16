'use client';

import { Separator } from '@/components/ui/separator';
import { useI18n } from '@/lib/i18n';
import type { YearlyStats } from '@/lib/types';

export function MonthlyStatsCard({ stats }: { stats: YearlyStats | null }) {
  const { t, locale } = useI18n();
  const byMonth = stats?.byMonth ?? new Array(12).fill(0);
  const max = Math.max(1, ...byMonth);

  const monthName = (i: number) =>
    new Date(2000, i, 1).toLocaleDateString(locale, { month: 'short' });

  const rows: Array<[string, number]> = [
    [t('home.stats.books'), stats?.byType.book ?? 0],
    [t('home.stats.movies'), stats?.byType.movie ?? 0],
    [t('home.stats.series'), stats?.byType.series ?? 0],
  ];

  return (
    <div className="tt-card flex flex-col gap-3 p-4">
      <p className="text-[10px] font-semibold tracking-widest text-muted-foreground/70 uppercase">
        {t('home.stats.byMonth')} {stats ? `· ${stats.year}` : ''}
      </p>

      <div className="flex h-16 items-end gap-1" role="img" aria-label={t('home.stats.aria')}>
        {byMonth.map((count, i) => (
          <div
            key={i}
            title={`${monthName(i)}: ${count}`}
            className="flex-1 rounded-t-sm bg-primary/70"
            style={{ height: `${Math.max(4, (count / max) * 100)}%` }}
          />
        ))}
      </div>

      <Separator />

      <dl className="flex flex-col gap-1.5 text-sm">
        {rows.map(([label, value]) => (
          <div key={label} className="flex items-center justify-between">
            <dt className="text-muted-foreground">{label}</dt>
            <dd className="font-medium text-foreground">{value}</dd>
          </div>
        ))}
      </dl>
    </div>
  );
}
