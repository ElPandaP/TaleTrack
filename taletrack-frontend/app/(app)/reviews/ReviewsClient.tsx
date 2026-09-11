'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { Pencil, Trash2 } from 'lucide-react';
import { Cover } from '@/components/media/cover';
import { StarRating, toStars } from '@/components/media/star-rating';
import { ReviewModal, type ReviewTarget } from '@/components/media/review-modal';
import { reviewService } from '@/lib/api/services';
import { useI18n } from '@/lib/i18n';
import type { ReviewItem } from '@/lib/types';

export default function ReviewsClient({ reviews }: { reviews: ReviewItem[] }) {
  const router = useRouter();
  const { t, tp, locale } = useI18n();
  const [target, setTarget] = useState<ReviewTarget | null>(null);
  const [modalOpen, setModalOpen] = useState(false);
  const [deleting, setDeleting] = useState<number | null>(null);

  const fmtDate = (iso: string) =>
    new Date(iso).toLocaleDateString(locale, { day: 'numeric', month: 'short', year: 'numeric' });

  const edit = (r: ReviewItem) => {
    if (!r.media) return;
    setTarget({
      mediaId: r.mediaId,
      title: r.media.title,
      type: r.media.type,
      posterUrl: r.media.posterUrl,
      reviewId: r.id,
      rating: r.rating,
      comment: r.comment,
    });
    setModalOpen(true);
  };

  const remove = async (id: number) => {
    if (!confirm(t('reviews.deleteConfirm'))) return;
    setDeleting(id);
    try {
      await reviewService.deleteReview(id);
      router.refresh();
    } finally {
      setDeleting(null);
    }
  };

  return (
    <div>
      <h1 className="mb-1 font-heading text-2xl font-semibold">{t('reviews.title')}</h1>
      <p className="mb-6 text-sm text-muted-foreground">{tp('common.reviews', reviews.length)}</p>

      {reviews.length === 0 ? (
        <p className="rounded-xl border border-dashed border-border px-4 py-16 text-center text-sm text-muted-foreground">
          {t('reviews.empty')}
        </p>
      ) : (
        <ul className="flex flex-col gap-4">
          {reviews.map((r) => (
            <li key={r.id} className="tt-card flex gap-4 p-4">
              {r.media && (
                <Cover
                  title={r.media.title}
                  type={r.media.type}
                  posterUrl={r.media.posterUrl}
                  className="w-14 shrink-0"
                />
              )}
              <div className="min-w-0 flex-1">
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0">
                    <p className="truncate font-medium text-foreground">
                      {r.media?.title ?? `#${r.mediaId}`}
                    </p>
                    <p className="text-xs text-muted-foreground">
                      {r.media ? t(`type.${r.media.type}`) : ''} · {fmtDate(r.createdAt)}
                    </p>
                  </div>
                  <div className="flex shrink-0 gap-1">
                    <button
                      type="button"
                      onClick={() => edit(r)}
                      aria-label={t('reviews.edit')}
                      className="rounded-lg p-1.5 text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground"
                    >
                      <Pencil aria-hidden="true" className="size-4" />
                    </button>
                    <button
                      type="button"
                      onClick={() => remove(r.id)}
                      disabled={deleting === r.id}
                      aria-label={t('reviews.delete')}
                      className="rounded-lg p-1.5 text-muted-foreground transition-colors hover:bg-destructive/10 hover:text-destructive disabled:opacity-50"
                    >
                      <Trash2 aria-hidden="true" className="size-4" />
                    </button>
                  </div>
                </div>
                <StarRating stars={toStars(r.rating)} className="mt-1.5" />
                {r.comment && <p className="mt-2 text-sm text-foreground/90">{r.comment}</p>}
              </div>
            </li>
          ))}
        </ul>
      )}

      <ReviewModal target={target} open={modalOpen} onOpenChange={setModalOpen} />
    </div>
  );
}
