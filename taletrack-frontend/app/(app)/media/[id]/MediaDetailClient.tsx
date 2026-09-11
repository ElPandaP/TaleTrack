'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { ArrowLeft } from 'lucide-react';
import { Cover, typeMeta } from '@/components/media/cover';
import { StarRating, toStars } from '@/components/media/star-rating';
import { Progress } from '@/components/ui/progress';
import { Button } from '@/components/ui/button';
import { ReviewModal, type ReviewTarget } from '@/components/media/review-modal';
import { useI18n } from '@/lib/i18n';
import type { MediaDetail } from '@/lib/types';

export default function MediaDetailClient({ detail }: { detail: MediaDetail }) {
  const { t, tp, locale } = useI18n();
  const router = useRouter();
  const [modalOpen, setModalOpen] = useState(false);
  // Next.js exposes the client-side history depth as history.state.idx — only
  // > 0 once the user has navigated within the app, so `back()` is safe to use
  // (it returns to wherever they came from: home, library, a friend's profile…).
  // A direct load / refresh / external link has no such history, so we fall
  // back to the library instead of leaving the app. Not used in the render
  // output, so computing it lazily on mount (client-only) needs no effect.
  const [hasAppHistory] = useState(
    () => typeof window !== 'undefined' && (window.history.state?.idx ?? 0) > 0,
  );
  const goBack = () => (hasAppHistory ? router.back() : router.push('/library'));
  const meta = typeMeta[detail.type];

  const fmtDate = (iso: string) =>
    new Date(iso).toLocaleDateString(locale, { day: 'numeric', month: 'short', year: 'numeric' });

  const target: ReviewTarget = {
    mediaId: detail.id,
    title: detail.title,
    type: detail.type,
    posterUrl: detail.posterUrl,
    reviewId: detail.myReviewId,
    rating: detail.myRating,
    comment: detail.myComment,
  };

  const unitKey =
    detail.type === 'Book' ? 'media.unit.pages' : detail.type === 'Series' ? 'media.unit.episodes' : 'media.unit.min';
  const progress = detail.myProgress ?? 0;

  return (
    <div>
      <button
        type="button"
        onClick={goBack}
        className="mb-6 inline-flex cursor-pointer items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground"
      >
        <ArrowLeft aria-hidden="true" className="size-4" />
        {t('common.back')}
      </button>

      <div className="flex flex-col gap-6 sm:flex-row">
        <Cover
          title={detail.title}
          type={detail.type}
          posterUrl={detail.posterUrl}
          className="w-40 shrink-0 self-center sm:self-start"
        />

        <div className="min-w-0 flex-1">
          <p className={`text-xs font-medium ${meta.text}`}>{t(`type.${detail.type}`)}</p>
          <h1 className="mt-1 font-heading text-2xl font-semibold">{detail.title}</h1>
          {detail.author && <p className="mt-0.5 text-muted-foreground">{detail.author}</p>}

          <div className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-muted-foreground">
            {detail.length > 0 && (
              <span>
                {detail.length} {t(unitKey)}
              </span>
            )}
            {detail.avgRating != null && (
              <span className="flex items-center gap-1.5">
                <StarRating stars={toStars(detail.avgRating)} />
                {t('media.avgRatings', {
                  avg: (detail.avgRating / 2).toFixed(1),
                  reviews: tp('common.reviews', detail.reviewCount),
                })}
              </span>
            )}
          </div>

          {detail.description && (
            <p className="mt-4 text-sm leading-relaxed text-foreground/90">{detail.description}</p>
          )}

          {/* Your status */}
          <div className="tt-card mt-5 flex flex-col gap-3 p-4">
            <p className="text-[10px] font-semibold tracking-widest text-muted-foreground/70 uppercase">
              {t('media.yourStatus')}
            </p>
            {detail.myProgress != null ? (
              <div className="flex items-center gap-3">
                <Progress value={progress} className="h-1.5" />
                <span className="shrink-0 text-xs text-muted-foreground">
                  {progress}%{detail.myLastEventDate ? ` · ${fmtDate(detail.myLastEventDate)}` : ''}
                </span>
              </div>
            ) : (
              <p className="text-sm text-muted-foreground">{t('media.notTracked')}</p>
            )}

            <div className="flex items-center justify-between gap-3">
              {detail.myRating != null ? (
                <StarRating stars={toStars(detail.myRating)} size="md" />
              ) : (
                <span className="text-sm text-muted-foreground">{t('media.noRating')}</span>
              )}
              <Button
                size="sm"
                variant={detail.myReviewId ? 'outline' : 'default'}
                onClick={() => setModalOpen(true)}
              >
                {t(detail.myReviewId ? 'media.editReview' : 'media.writeReview')}
              </Button>
            </div>
          </div>
        </div>
      </div>

      {/* All reviews */}
      <h2 className="mt-10 mb-4 font-heading text-lg font-semibold">
        {t('media.reviewsHeading')}{' '}
        {detail.reviewCount > 0 && (
          <span className="text-muted-foreground">· {detail.reviewCount}</span>
        )}
      </h2>

      {detail.reviews.length === 0 ? (
        <p className="rounded-xl border border-dashed border-border px-4 py-10 text-center text-sm text-muted-foreground">
          {t('media.reviewsEmpty')}
        </p>
      ) : (
        <ul className="flex flex-col gap-3">
          {detail.reviews.map((r) => (
            <li key={r.id} className="tt-card p-4">
              <div className="flex items-center justify-between gap-2">
                <span className="text-sm font-medium text-foreground">
                  @{r.username ?? 'user'}
                  {r.mine && (
                    <span className="ml-1.5 text-xs font-normal text-muted-foreground">
                      {t('media.you')}
                    </span>
                  )}
                </span>
                <span className="text-xs text-muted-foreground">{fmtDate(r.createdAt)}</span>
              </div>
              <StarRating stars={toStars(r.rating)} className="mt-1.5" />
              {r.comment && <p className="mt-2 text-sm text-foreground/90">{r.comment}</p>}
            </li>
          ))}
        </ul>
      )}

      <ReviewModal target={target} open={modalOpen} onOpenChange={setModalOpen} />
    </div>
  );
}
