import { cn } from '@/lib/utils';

function initials(name: string) {
  return name.split(/\s+/).map((w) => w[0]).join('').slice(0, 2).toUpperCase();
}

const sizes = {
  xs: 'size-7 text-[11px]',
  sm: 'size-8 text-xs',
  md: 'size-10 text-sm',
  lg: 'size-14 text-lg',
  xl: 'size-20 text-2xl rounded-2xl',
} as const;

/**
 * A user's picture, or their initials on a sage circle. Uses a plain `<img>`
 * (avatars are tiny and can be SVG, which next/image blocks by default).
 */
export function UserAvatar({
  username,
  avatarUrl,
  size = 'sm',
  className,
}: {
  username: string;
  avatarUrl?: string | null;
  size?: keyof typeof sizes;
  className?: string;
}) {
  const rounded = size === 'xl' ? 'rounded-2xl' : 'rounded-full';

  if (avatarUrl) {
    return (
      // eslint-disable-next-line @next/next/no-img-element
      <img
        src={avatarUrl}
        alt=""
        className={cn('shrink-0 border border-border object-cover', rounded, sizes[size], className)}
      />
    );
  }

  return (
    <span
      aria-hidden="true"
      className={cn(
        'flex shrink-0 items-center justify-center border border-primary/30 bg-primary/15 font-heading font-semibold text-primary',
        rounded,
        sizes[size],
        className,
      )}
    >
      {initials(username || 'U')}
    </span>
  );
}
