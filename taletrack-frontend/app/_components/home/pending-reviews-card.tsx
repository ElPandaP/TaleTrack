'use client';

import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { Cover } from '@/components/media/cover';
import type { ReviewTarget } from '@/components/media/review-modal';
import { useT } from '@/lib/i18n';
import type { LibraryItem } from '@/lib/types';

// Show at most this many covers; the button still counts every pending item.
const MAX_THUMBS = 8;

export function PendingReviewsCard({
  items,
  onReview,
}: {
  items: LibraryItem[];
  onReview: (target: ReviewTarget) => void;
}) {
  const t = useT();

  const toTarget = (item: LibraryItem): ReviewTarget => ({
    mediaId: item.mediaId,
    title: item.title,
    type: item.type,
    posterUrl: item.posterUrl,
  });

  return (
    <div className="tt-card flex flex-col gap-3 p-4">
      <div className="flex items-baseline justify-between">
        <Link
          href="/library?reviews=todo"
          className="text-[10px] font-semibold tracking-widest text-muted-foreground/70 uppercase transition-colors hover:text-foreground"
        >
          {t('home.toReview')}
        </Link>
        {items.length > 0 && (
          <span className="text-[10px] text-muted-foreground/70">{items.length}</span>
        )}
      </div>

      {items.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t('home.toReview.empty')}</p>
      ) : (
        <>
          <ul className="grid grid-cols-[repeat(auto-fill,minmax(3.25rem,1fr))] gap-2">
            {items.slice(0, MAX_THUMBS).map((item) => (
              <li key={item.mediaId}>
                <button
                  type="button"
                  onClick={() => onReview(toTarget(item))}
                  title={t('home.toReview.reviewItem', { title: item.title })}
                  className="block w-full cursor-pointer transition hover:brightness-[1.03] focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
                >
                  <Cover
                    title={item.title}
                    type={item.type}
                    posterUrl={item.posterUrl}
                    className="rounded-md"
                  />
                </button>
              </li>
            ))}
          </ul>
          <Button size="sm" className="w-full" onClick={() => onReview(toTarget(items[0]))}>
            {t('home.toReview.cta', { count: items.length })}
          </Button>
        </>
      )}
    </div>
  );
}
