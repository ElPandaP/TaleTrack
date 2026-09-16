'use client';

import { useState } from 'react';
import { Star } from 'lucide-react';
import { useI18n } from '@/lib/i18n';
import { cn } from '@/lib/utils';
import { starSize } from './StarRating';

/** Interactive 1–5 star picker. */
export function StarRatingInput({
  value,
  onChange,
  size = 'lg',
}: {
  value: number;
  onChange: (stars: number) => void;
  size?: keyof typeof starSize;
}) {
  const { tp } = useI18n();
  const [hover, setHover] = useState(0);
  const shown = hover || value;

  return (
    <div className="inline-flex items-center gap-1" onMouseLeave={() => setHover(0)}>
      {[1, 2, 3, 4, 5].map((n) => (
        <button
          key={n}
          type="button"
          onClick={() => onChange(n)}
          onMouseEnter={() => setHover(n)}
          aria-label={tp('a11y.starN', n)}
          aria-pressed={value === n}
          className="cursor-pointer rounded-sm p-0.5 text-primary transition-transform hover:scale-110 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
        >
          <Star
            aria-hidden="true"
            className={cn(
              starSize[size],
              n <= shown ? 'fill-current' : 'fill-transparent text-muted-foreground/35',
            )}
          />
        </button>
      ))}
    </div>
  );
}
