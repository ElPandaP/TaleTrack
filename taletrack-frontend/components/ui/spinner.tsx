import { cn } from '@/lib/utils';

/** Small spinning ring, sized/colored for use inside a primary button by default. */
export function Spinner({ className }: { className?: string }) {
  return (
    <div
      className={cn(
        'size-4 animate-spin rounded-full border-2 border-primary-foreground/30 border-t-primary-foreground',
        className,
      )}
    />
  );
}
