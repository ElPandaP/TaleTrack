'use client';

import { Star } from 'lucide-react';
import { useT } from '@/lib/i18n';
import { cn } from '@/lib/utils';

/**
 * Converts a backend rating (1-10 scale) to stars (1-5 scale), as the UI shows them.
 *
 * @returns The rounded number of stars, or 0 when there is no rating.
 */
export const toStars = (rating10: number | null | undefined): number =>
  rating10 == null ? 0 : Math.round(rating10 / 2);
/** Converts stars (1-5) to the backend's 1-10 rating scale, clamped to 1-10. */
export const toRating10 = (stars: number): number => Math.max(1, Math.min(10, stars * 2));

/** Icon size classes for the star components. */
export const starSize = {
  sm: 'size-3',
  md: 'size-4',
  lg: 'size-6',
} as const;

/**
 * Read-only row of five stars with the first `stars` filled.
 *
 * @param props - Component props.
 * @param props.stars - Number of filled stars (0-5).
 * @param props.size - Icon size.
 */
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
