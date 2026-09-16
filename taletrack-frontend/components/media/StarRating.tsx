'use client';

import { Star } from 'lucide-react';
import { useT } from '@/lib/i18n';
import { cn } from '@/lib/utils';

/**
 * The backend stores `rating` on a 1–10 scale; the UI works on a 1–5 scale.
 * Use these helpers at the API boundary.
 */
export const toStars = (rating10: number | null | undefined): number =>
  rating10 == null ? 0 : Math.round(rating10 / 2);
export const toRating10 = (stars: number): number => Math.max(1, Math.min(10, stars * 2));

export const starSize = {
  sm: 'size-3',
  md: 'size-4',
  lg: 'size-6',
} as const;

/** Read-only rating display. */
export function StarRating({
  stars,
  size = 'sm',
  className,
}: {
  stars: number;
  size?: keyof typeof starSize;
  className?: string;
}) {
  const t = useT();
  return (
    <div
      className={cn('inline-flex items-center gap-0.5 text-primary', className)}
      role="img"
      aria-label={t('a11y.starsOf5', { count: stars })}
    >
      {[1, 2, 3, 4, 5].map((n) => (
        <Star
          key={n}
          aria-hidden="true"
          className={cn(
            starSize[size],
            n <= stars ? 'fill-current' : 'fill-transparent text-muted-foreground/35',
          )}
        />
      ))}
    </div>
  );
}
