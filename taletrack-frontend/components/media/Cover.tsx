import { BookOpen, Film, Tv } from 'lucide-react';
import type { LibraryType } from '@/lib/types';
import { cn } from '@/lib/utils';

// Styling / icon per media type. Labels are translated at the call site via
// `t(`type.${type}`)` / `t(`typePlural.${type}`)`.
export const typeMeta: Record<
  LibraryType,
  { text: string; bg: string; border: string; Icon: typeof BookOpen }
> = {
  Book:   { text: 'text-chart-1', bg: 'bg-chart-1/10', border: 'border-chart-1/20', Icon: BookOpen },
  Movie:  { text: 'text-chart-2', bg: 'bg-chart-2/10', border: 'border-chart-2/20', Icon: Film },
  Series: { text: 'text-chart-3', bg: 'bg-chart-3/10', border: 'border-chart-3/20', Icon: Tv },
};

/**
 * A poster tile with a 2:3 aspect ratio. Falls back to a typed placeholder
 * when the media has no cover image yet.
 */
export function Cover({
  title,
  type,
  posterUrl,
  className,
}: {
  title: string;
  type: LibraryType;
  posterUrl?: string | null;
  className?: string;
}) {
  const meta = typeMeta[type];
  const { Icon } = meta;

  return (
    <div
      className={cn(
        'relative aspect-2/3 overflow-hidden rounded-xl border',
        meta.bg,
        meta.border,
        className,
      )}
    >
      {posterUrl ? (
        // Plain <img>, not next/image: its server-side optimizer proxy gets rejected
        // by some cover sources (e.g. OpenLibrary/archive.org) — see user-avatar.tsx
        // for the same fix applied to avatars.
        // eslint-disable-next-line @next/next/no-img-element
        <img
          src={posterUrl}
          alt={`${title} cover`}
          loading="lazy"
          className="absolute inset-0 h-full w-full object-cover"
        />
      ) : (
        <div className="flex h-full w-full flex-col items-center justify-center gap-1.5 p-2 text-center">
          <Icon aria-hidden="true" className={cn('size-5 opacity-50', meta.text)} />
          <span className={cn('line-clamp-3 text-[11px] leading-tight font-medium opacity-60', meta.text)}>
            {title}
          </span>
        </div>
      )}
    </div>
  );
}
