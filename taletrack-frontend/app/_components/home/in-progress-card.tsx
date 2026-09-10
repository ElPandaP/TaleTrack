'use client';

import Link from 'next/link';
import { Progress } from '@/components/ui/progress';
import { Cover } from '@/components/media/cover';
import { useT } from '@/lib/i18n';
import type { LibraryItem } from '@/lib/types';

const MAX_ROWS = 6;

export function InProgressCard({ items }: { items: LibraryItem[] }) {
  const t = useT();
  const shown = items.slice(0, MAX_ROWS);

  return (
    <div className="tt-card flex flex-col gap-3 p-4">
      <div className="flex items-baseline justify-between">
        <Link
          href="/library?status=reading"
          className="text-[10px] font-semibold tracking-widest text-muted-foreground/70 uppercase transition-colors hover:text-foreground"
        >
          {t('home.inProgress')}
        </Link>
        {items.length > 0 && (
          <span className="text-[10px] text-muted-foreground/70">{items.length}</span>
        )}
      </div>

      {items.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t('home.inProgress.empty')}</p>
      ) : (
        <ul className="flex flex-col gap-2.5">
          {shown.map((item) => {
            const pct = item.progress ?? 0;
            return (
              <li key={item.mediaId}>
                <Link
                  href={`/media/${item.mediaId}`}
                  className="group -mx-1 flex items-center gap-3 rounded-lg px-1 py-1 transition-colors hover:bg-secondary/50"
                >
                  <Cover
                    title={item.title}
                    type={item.type}
                    posterUrl={item.posterUrl}
                    className="w-10 shrink-0 rounded-md"
                    sizes="40px"
                  />
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-xs font-medium text-foreground group-hover:text-primary">
                      {item.title}
                    </p>
                    <div className="mt-1.5 flex items-center gap-2">
                      <Progress value={pct} className="h-1" />
                      <span className="shrink-0 text-[10px] text-muted-foreground">{pct}%</span>
                    </div>
                  </div>
                </Link>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
