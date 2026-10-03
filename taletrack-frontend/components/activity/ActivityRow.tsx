'use client';

import Link from 'next/link';
import { UserAvatar } from '@/components/media/UserAvatar';
import { Cover } from '@/components/media/Cover';
import { StarRating, toStars } from '@/components/media/StarRating';
import { pickTitle, useI18n } from '@/lib/i18n';
import type { ActivityItem } from '@/lib/types';

/** Returns a formatter that turns an ISO date into a localized relative time ("3 hours ago"). */
export function useRelativeTime() {
  const { locale } = useI18n();
  const rtf = new Intl.RelativeTimeFormat(locale, { numeric: 'auto' });
  return (iso: string) => {
    const mins = Math.round((Date.now() - new Date(iso).getTime()) / 60000);
    if (Math.abs(mins) < 60) return rtf.format(-mins, 'minute');
    const hours = Math.round(mins / 60);
    if (Math.abs(hours) < 24) return rtf.format(-hours, 'hour');
    const days = Math.round(hours / 24);
    if (Math.abs(days) < 30) return rtf.format(-days, 'day');
    return rtf.format(-Math.round(days / 30), 'month');
  };
}

/**
 * One activity entry as a list item: what happened to which media and when, with the rating and
 * comment of a review, and the media's cover.
 *
 * @param props - Component props.
 * @param props.item - The activity entry.
 * @param props.showUser - Shows who did it (avatar and username); off on a user's own profile.
 */
export function ActivityRow({ item, showUser = true }: { item: ActivityItem; showUser?: boolean }) {
  const { t, locale } = useI18n();
  const rel = useRelativeTime();
  const title = pickTitle(item.mediaTitleEN, item.mediaTitleES, locale);

  return (
    <li className="tt-card flex gap-3 p-4">
      {showUser && (
        <Link href={`/u/${item.userId}`} className="shrink-0">
          <UserAvatar username={item.username} avatarUrl={item.avatarUrl} size="md" />
        </Link>
      )}
      <div className="min-w-0 flex-1">
        <div className="flex items-start justify-between gap-2">
          <p className="text-sm text-foreground">
            {showUser && (
              <>
                <Link href={`/u/${item.userId}`} className="font-medium hover:text-primary">
                  @{item.username}
                </Link>{' '}
              </>
            )}
            <span className="font-semibold">{t(`activity.${item.kind}`)}</span>{' '}
            <Link href={`/media/${item.mediaId}`} className="font-medium text-primary hover:underline">
              {title}
            </Link>
          </p>
          <span className="shrink-0 text-xs text-muted-foreground">{rel(item.date)}</span>
        </div>
        {item.kind === 'reviewed' && item.rating != null && (
          <StarRating stars={toStars(item.rating)} className="mt-1.5" />
        )}
        {item.kind === 'reviewed' && item.comment && (
          <p className="mt-1.5 line-clamp-3 text-sm text-foreground/80">{item.comment}</p>
        )}
      </div>
      <Link href={`/media/${item.mediaId}`} className="shrink-0">
        <Cover title={title} type={item.mediaType} posterUrl={item.mediaPosterUrl} className="w-10 rounded-md" />
      </Link>
    </li>
  );
}
