import { cn } from '@/lib/utils';

/** Dashed placeholder box shared by every empty list (library, activity, reviews, carousels…). */
export function EmptyState({
  children,
  className,
}: {
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <p
      className={cn(
        'rounded-xl border border-dashed border-border px-4 py-16 text-center text-sm text-muted-foreground',
        className,
      )}
    >
      {children}
    </p>
  );
}
