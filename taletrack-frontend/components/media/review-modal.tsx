'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Cover } from '@/components/media/cover';
import { toStars, toRating10 } from '@/components/media/star-rating';
import { StarRatingInput } from '@/components/media/star-rating-input';
import { reviewService } from '@/lib/api/services';
import { useT } from '@/lib/i18n';
import type { LibraryType } from '@/lib/types';

const MAX_COMMENT = 400;

export interface ReviewTarget {
  mediaId: number;
  title: string;
  type: LibraryType;
  posterUrl?: string | null;
  reviewId?: number | null;
  rating?: number | null; // 1–10 (backend scale)
  comment?: string | null;
}

export function ReviewModal({
  target,
  open,
  onOpenChange,
  onSaved,
}: {
  target: ReviewTarget | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved?: () => void;
}) {
  const router = useRouter();
  const t = useT();
  const [stars, setStars] = useState(0);
  const [comment, setComment] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const isEdit = !!target?.reviewId;

  useEffect(() => {
    if (open && target) {
      setStars(toStars(target.rating));
      setComment(target.comment ?? '');
      setError(null);
    }
  }, [open, target]);

  const handleSubmit = async () => {
    if (!target || stars < 1) {
      setError(t('reviewModal.pickRating'));
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      const rating10 = toRating10(stars);
      const body = comment.trim() || undefined;
      if (isEdit && target.reviewId) {
        await reviewService.editReview(target.reviewId, rating10, body);
      } else {
        await reviewService.addReview(target.mediaId, rating10, body);
      }
      onOpenChange(false);
      onSaved?.();
      router.refresh();
    } catch {
      setError(t('reviewModal.saveError'));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>{t(isEdit ? 'reviewModal.titleEdit' : 'reviewModal.titleNew')}</DialogTitle>
          <DialogDescription>
            {target
              ? t('reviewModal.subtitle', { type: t(`type.${target.type}`), title: target.title })
              : ''}
          </DialogDescription>
        </DialogHeader>

        {target && (
          <div className="flex gap-4">
            <Cover
              title={target.title}
              type={target.type}
              posterUrl={target.posterUrl}
              className="w-28 shrink-0 self-start"
            />
            <div className="flex min-w-0 flex-1 flex-col gap-3">
              <div>
                <p className="mb-1.5 text-xs font-medium text-muted-foreground">
                  {t('reviewModal.rating')}
                </p>
                <StarRatingInput value={stars} onChange={setStars} />
              </div>
              <div>
                <label
                  htmlFor="review-comment"
                  className="mb-1.5 block text-xs font-medium text-muted-foreground"
                >
                  {t('reviewModal.reviewLabel')}{' '}
                  <span className="text-muted-foreground/60">
                    {t('reviewModal.maxChars', { max: MAX_COMMENT })}
                  </span>
                </label>
                <textarea
                  id="review-comment"
                  value={comment}
                  maxLength={MAX_COMMENT}
                  onChange={(e) => setComment(e.target.value)}
                  rows={4}
                  placeholder={t('reviewModal.placeholder')}
                  className="tt-input resize-none px-3 py-2"
                />
                <p className="mt-1 text-right text-[11px] text-muted-foreground/60">
                  {comment.length}/{MAX_COMMENT}
                </p>
              </div>
            </div>
          </div>
        )}

        {error && (
          <p className="rounded-lg bg-destructive/10 px-3 py-2 text-sm text-destructive">{error}</p>
        )}

        <DialogFooter>
          <Button variant="ghost" onClick={() => onOpenChange(false)} disabled={submitting}>
            {t('reviewModal.cancel')}
          </Button>
          <Button onClick={handleSubmit} disabled={submitting || stars < 1}>
            {submitting
              ? t('reviewModal.saving')
              : t(isEdit ? 'reviewModal.save' : 'reviewModal.publish')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
