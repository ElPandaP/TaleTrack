'use client';

import { ChevronLeft, ChevronRight } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useT } from '@/lib/i18n';
import { cn } from '@/lib/utils';

/** Prev/next pager shared by every paginated list (library, activity, public profile). */
export function Pagination({
  page,
  totalPages,
  onChange,
  className,
}: {
  page: number;
  totalPages: number;
  onChange: (page: number) => void;
  className?: string;
}) {
  const t = useT();
  if (totalPages <= 1) return null;

  return (
    <nav className={cn('flex items-center justify-center gap-2', className)} aria-label="Pagination">
      <Button
        type="button"
        variant="outline"
        size="icon"
        onClick={() => onChange(page - 1)}
        disabled={page === 1}
        aria-label={t('a11y.previousPage')}
        className="disabled:opacity-30"
      >
        <ChevronLeft aria-hidden="true" className="size-4" />
      </Button>
      <span className="px-2 text-sm text-muted-foreground">
        {t('pagination.pageLabel')} <span className="font-medium text-foreground">{page}</span>{' '}
        {t('pagination.of')} {totalPages}
      </span>
      <Button
        type="button"
        variant="outline"
        size="icon"
        onClick={() => onChange(page + 1)}
        disabled={page === totalPages}
        aria-label={t('a11y.nextPage')}
        className="disabled:opacity-30"
      >
        <ChevronRight aria-hidden="true" className="size-4" />
      </Button>
    </nav>
  );
}
