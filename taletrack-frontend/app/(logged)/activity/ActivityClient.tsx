'use client';

import { useState } from 'react';
import { ActivityRow } from '@/components/activity/ActivityRow';
import { EmptyState } from '@/components/ui/empty-state';
import { Pagination } from '@/components/ui/pagination';
import { useI18n } from '@/lib/i18n';
import { cn } from '@/lib/utils';
import type { ActivityItem } from '@/lib/types';

/** Whose activity the feed shows: everyone's, only the user's, or only their friends'. */
export type Scope = 'all' | 'mine' | 'friends';
const SCOPES: Scope[] = ['all', 'mine', 'friends'];
const PAGE_SIZE = 15;

/**
 * Paginated activity feed with a filter for everyone, only the user, or only friends.
 *
 * @param props - Component props.
 * @param props.feeds - The feed for each scope, each fetched separately from the backend.
 */
export default function ActivityClient({ feeds }: { feeds: Record<Scope, ActivityItem[]> }) {
  const { t } = useI18n();
  const [scope, setScope] = useState<Scope>('all');
  const [page, setPage] = useState(1);

  const filtered = feeds[scope];

  // Back to page 1 when the scope changes (state adjusted during render).
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
        <EmptyState>{t(scope === 'mine' ? 'activity.empty.mine' : 'activity.empty')}</EmptyState>
      ) : (
        <>
          <ul className="flex flex-col gap-3">
            {pageItems.map((it) => (
              <ActivityRow key={it.id} item={it} />
            ))}
          </ul>

          <Pagination page={current} totalPages={totalPages} onChange={setPage} className="mt-8" />
        </>
      )}
    </div>
  );
}
