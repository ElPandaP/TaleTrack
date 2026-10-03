import { Leaf } from 'lucide-react';
import LocaleToggle from '@/components/layout/LocaleToggle';
import { cn } from '@/lib/utils';

/**
 * Full-screen layout for the auth pages: the TaleTrack logo over a centred column, with a
 * language toggle and soft background blobs.
 *
 * @param props - Component props.
 * @param props.children - The page content.
 * @param props.card - Wraps the content in a card; off for pages that lay out their own cards.
 * @param props.accent - Adds a second, warm blob on the opposite side: `start` puts the main blob
 * on the left (login), `end` on the right (register). Without it there is a single blob.
 */
export default function AuthShell({
  children,
  card = true,
  accent,
}: {
  children: React.ReactNode;
  card?: boolean;
  accent?: 'start' | 'end';
}) {
  return (
    <div className="relative flex min-h-screen items-center justify-center overflow-hidden bg-background px-4">
      <div className="absolute top-4 right-4 z-20">
        <LocaleToggle />
      </div>
      <div
        className={cn(
          'pointer-events-none absolute top-0 h-96 w-96 rounded-full bg-primary/8 blur-3xl',
          accent === 'end' ? 'right-1/4' : 'left-1/4',
        )}
      />
      {accent && (
        <div
          className={cn(
            'pointer-events-none absolute bottom-0 h-80 w-80 rounded-full bg-[oklch(0.65_0.13_65)]/8 blur-3xl',
            accent === 'end' ? 'left-1/4' : 'right-1/4',
          )}
        />
      )}
      <div className="relative z-10 w-full max-w-md py-10">
        <div className="mb-8 flex items-center justify-center gap-2">
          <Leaf aria-hidden="true" className="size-5 text-primary" />
          <span className="font-heading text-2xl font-semibold leading-none">TaleTrack</span>
        </div>
        {card ? <div className="tt-card p-8">{children}</div> : children}
      </div>
    </div>
  );
}
