'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { UserAvatar } from '@/components/media/user-avatar';
import { Cover } from '@/components/media/cover';
import { StarRating, toStars } from '@/components/media/star-rating';
import { useAuth } from '@/lib/auth-context';
import { useI18n } from '@/lib/i18n';
import { cn } from '@/lib/utils';
import type { ActivityItem } from '@/lib/types';

type Scope = 'all' | 'mine' | 'friends';
const SCOPES: Scope[] = ['all', 'mine', 'friends'];
const PAGE_SIZE = 15;

function useRelativeTime() {
  const { locale } = useI18n();
  const rtf = new Intl.RelativeTimeFormat(locale, { numeric: 'auto' });
  return (iso: string) => {
    const diff = Date.now() - new Date(iso).getTime();
    const mins = Math.round(diff / 60000);
    if (Math.abs(mins) < 60) return rtf.format(-mins, 'minute');
    const hours = Math.round(mins / 60);
    if (Math.abs(hours) < 24) return rtf.format(-hours, 'hour');
    const days = Math.round(hours / 24);
    if (Math.abs(days) < 30) return rtf.format(-days, 'day');
    return rtf.format(-Math.round(days / 30), 'month');
  };
}

export default function ActivityClient({ items }: { items: ActivityItem[] }) {
  const { user } = useAuth();
  const { t } = useI18n();
  const rel = useRelativeTime();
  const [scope, setScope] = useState<Scope>('all');
  const [page, setPage] = useState(1);

  const filtered = useMemo(() => {
    if (scope === 'mine') return items.filter((i) => i.userId === user?.id);
    if (scope === 'friends') return items.filter((i) => i.userId !== user?.id);
    return items;
  }, [items, scope, user?.id]);

  // Reset page when the scope changes (adjust state during render).
  const [prevScope, setPrevScope] = useState(scope);
  if (scope !== prevScope) {
    setPrevScope(scope);
    setPage(1);
  }

  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const current = Math.min(page, totalPages);
  const pageItems = filtered.slice((current - 1) * PAGE_SIZE, current * PAGE_SIZE);

  return (
    <div>
      <h1 className="font-heading text-2xl font-semibold">{t('activity.title')}</h1>
      <p className="mt-1 mb-6 text-sm text-muted-foreground">{t('activity.subtitle')}</p>

      <div className="mb-6 inline-flex rounded-lg border border-border bg-secondary/40 p-0.5">
        {SCOPES.map((s) => (
          <button
            key={s}
            type="button"
            onClick={() => setScope(s)}
            className={cn(
              'cursor-pointer rounded-md px-3 py-1 text-sm font-medium transition-colors',
              scope === s
                ? 'bg-card text-foreground shadow-sm'
                : 'text-muted-foreground hover:text-foreground',
            )}
          >
            {t(`activity.scope.${s}`)}
          </button>
        ))}
      </div>

      {filtered.length === 0 ? (
        <p className="rounded-xl border border-dashed border-border px-4 py-16 text-center text-sm text-muted-foreground">
          {t(scope === 'mine' ? 'activity.empty.mine' : 'activity.empty')}
        </p>
      ) : (
        <>
          <ul className="flex flex-col gap-3">
            {pageItems.map((it) => (
              <li key={it.id} className="tt-card flex gap-3 p-4">
                <Link href={`/u/${it.userId}`} className="shrink-0">
                  <UserAvatar username={it.username} avatarUrl={it.avatarUrl} size="md" />
                </Link>
                <div className="min-w-0 flex-1">
                  <div className="flex items-start justify-between gap-2">
                    <p className="text-sm text-foreground">
                      <Link href={`/u/${it.userId}`} className="font-medium hover:text-primary">
                        @{it.username}
                      </Link>{' '}
                      <span className="font-semibold">{t(`activity.${it.kind}`)}</span>{' '}
                      <Link
                        href={`/media/${it.mediaId}`}
                        className="font-medium text-primary hover:underline"
                      >
                        {it.mediaTitle}
                      </Link>
                    </p>
                    <span className="shrink-0 text-xs text-muted-foreground">{rel(it.date)}</span>
                  </div>
                  {it.kind === 'reviewed' && it.rating != null && (
                    <StarRating stars={toStars(it.rating)} className="mt-1.5" />
                  )}
                  {it.kind === 'reviewed' && it.comment && (
                    <p className="mt-1.5 line-clamp-3 text-sm text-foreground/80">{it.comment}</p>
                  )}
                </div>
                <Link href={`/media/${it.mediaId}`} className="shrink-0">
                  <Cover
                    title={it.mediaTitle}
                    type={it.mediaType}
                    posterUrl={it.mediaPosterUrl}
                    className="w-10 rounded-md"
                  />
                </Link>
              </li>
            ))}
          </ul>

          {totalPages > 1 && (
            <nav className="mt-8 flex items-center justify-center gap-2" aria-label="Pagination">
              <button
                type="button"
                onClick={() => setPage(current - 1)}
                disabled={current === 1}
                aria-label={t('a11y.previousPage')}
                className="flex size-8 items-center justify-center rounded-lg border border-border text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground disabled:opacity-30"
              >
                <ChevronLeft aria-hidden="true" className="size-4" />
              </button>
              <span className="px-2 text-sm text-muted-foreground">
                {t('pagination.pageLabel')} <span className="font-medium text-foreground">{current}</span>{' '}
                {t('pagination.of')} {totalPages}
              </span>
              <button
                type="button"
                onClick={() => setPage(current + 1)}
                disabled={current === totalPages}
                aria-label={t('a11y.nextPage')}
                className="flex size-8 items-center justify-center rounded-lg border border-border text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground disabled:opacity-30"
              >
                <ChevronRight aria-hidden="true" className="size-4" />
              </button>
            </nav>
          )}
        </>
      )}
    </div>
  );
}
