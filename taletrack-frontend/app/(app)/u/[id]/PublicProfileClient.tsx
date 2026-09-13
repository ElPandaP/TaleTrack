'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { UserPlus, UserMinus, Clock, BookOpen, Film, Tv } from 'lucide-react';
import { UserAvatar } from '@/components/media/user-avatar';
import { Cover } from '@/components/media/cover';
import { StarRating, toStars } from '@/components/media/star-rating';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/ui/empty-state';
import { Pagination } from '@/components/ui/pagination';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from '@/components/ui/dialog';
import { friendService } from '@/lib/api/services';
import { useI18n } from '@/lib/i18n';
import { sendErrorKey } from '@/lib/api/friend-errors';
import type { ActivityItem, PublicProfile } from '@/lib/types';

const PAGE_SIZE = 15;

function useRelativeTime() {
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

export default function PublicProfileClient({
  profile,
  activity,
}: {
  profile: PublicProfile;
  activity: ActivityItem[];
}) {
  const router = useRouter();
  const { t, locale } = useI18n();
  const rel = useRelativeTime();
  const [busy, setBusy] = useState(false);
  const [rel_, setRel] = useState(profile.relationship);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [removeDialogOpen, setRemoveDialogOpen] = useState(false);

  const memberSince = new Date(profile.createdAt).toLocaleDateString(locale, {
    month: 'long',
    year: 'numeric',
  });

  const totalPages = Math.max(1, Math.ceil(activity.length / PAGE_SIZE));
  const currentPage = Math.min(page, totalPages);
  const pageItems = activity.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE);

  const add = async () => {
    setBusy(true);
    setError(null);
    try {
      await friendService.sendRequest(profile.id);
      setRel('outgoing');
      router.refresh();
    } catch (err) {
      setError(t(sendErrorKey(err)));
    } finally {
      setBusy(false);
    }
  };

  const confirmRemove = async () => {
    setBusy(true);
    setError(null);
    try {
      await friendService.remove(profile.id);
      setRel('none');
      setRemoveDialogOpen(false);
      router.refresh();
    } catch {
      setError(t('publicProfile.actionError'));
    } finally {
      setBusy(false);
    }
  };

  const stats: Array<[React.ComponentType<{ className?: string }>, string, number]> = [
    [BookOpen, 'text-chart-1', profile.counts.book],
    [Film, 'text-chart-2', profile.counts.movie],
    [Tv, 'text-chart-3', profile.counts.series],
  ];

  return (
    <div className="mx-auto max-w-2xl">
      <div className="tt-card flex flex-col items-center gap-3 p-6 text-center sm:flex-row sm:text-left">
        <UserAvatar username={profile.username} avatarUrl={profile.avatarUrl} size="xl" />
        <div className="min-w-0 flex-1">
          <h1 className="font-heading text-2xl font-semibold">@{profile.username}</h1>
          <p className="mt-0.5 text-xs text-muted-foreground">
            {t('publicProfile.memberSince', { date: memberSince })}
          </p>
          <div className="mt-2 flex justify-center gap-4 text-sm text-muted-foreground sm:justify-start">
            {stats.map(([Icon, color, n], i) => (
              <span key={i} className="flex items-center gap-1.5">
                <Icon className={`size-4 ${color}`} />
                {n}
              </span>
            ))}
          </div>
        </div>

        <div className="shrink-0">
          {rel_ === 'self' ? (
            <Button asChild variant="outline" size="sm">
              <Link href="/profile">{t('publicProfile.editYours')}</Link>
            </Button>
          ) : rel_ === 'friends' ? (
            <Button variant="outline" size="sm" onClick={() => setRemoveDialogOpen(true)} disabled={busy}>
              <UserMinus aria-hidden="true" className="size-4" />
              {t('publicProfile.removeFriend')}
            </Button>
          ) : rel_ === 'outgoing' ? (
            <span className="flex items-center gap-1.5 text-xs text-muted-foreground">
              <Clock aria-hidden="true" className="size-3.5" />
              {t('publicProfile.requestPending')}
            </span>
          ) : rel_ === 'incoming' ? (
            <Button asChild variant="outline" size="sm">
              <Link href="/friends">{t('publicProfile.respondAtFriends')}</Link>
            </Button>
          ) : (
            <Button size="sm" onClick={add} disabled={busy}>
              <UserPlus aria-hidden="true" className="size-4" />
              {t('publicProfile.addFriend')}
            </Button>
          )}
        </div>
      </div>

      {error && <p className="mt-3 text-sm text-destructive">{error}</p>}

      <h2 className="mt-8 mb-4 font-heading text-lg font-semibold">
        {t('publicProfile.activityTitle')}
      </h2>

      {activity.length === 0 ? (
        <EmptyState className="py-12">{t('publicProfile.activityPrivate')}</EmptyState>
      ) : (
        <>
          <ul className="flex flex-col gap-3">
            {pageItems.map((it) => (
              <li key={it.id} className="tt-card flex items-start gap-3 p-4">
                <div className="min-w-0 flex-1">
                  <p className="text-sm text-foreground">
                    {t(`activity.${it.kind}`)}{' '}
                    <Link href={`/media/${it.mediaId}`} className="font-medium text-primary hover:underline">
                      {it.mediaTitle}
                    </Link>
                    <span className="ml-2 text-xs text-muted-foreground">{rel(it.date)}</span>
                  </p>
                  {it.kind === 'reviewed' && it.rating != null && (
                    <StarRating stars={toStars(it.rating)} className="mt-1.5" />
                  )}
                  {it.kind === 'reviewed' && it.comment && (
                    <p className="mt-1.5 line-clamp-3 text-sm text-foreground/80">{it.comment}</p>
                  )}
                </div>
                <Link href={`/media/${it.mediaId}`} className="shrink-0">
                  <Cover
                    title={it.mediaTitle}
                    type={it.mediaType}
                    posterUrl={it.mediaPosterUrl}
                    className="w-10 rounded-md"
                  />
                </Link>
              </li>
            ))}
          </ul>

          <Pagination page={currentPage} totalPages={totalPages} onChange={setPage} className="mt-6" />
        </>
      )}

      <Dialog open={removeDialogOpen} onOpenChange={setRemoveDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('friends.removeConfirmTitle')}</DialogTitle>
            <DialogDescription>
              {t('friends.removeConfirm', { name: `@${profile.username}` })}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRemoveDialogOpen(false)}>
              {t('friends.cancel')}
            </Button>
            <Button
              variant="destructive"
              onClick={confirmRemove}
              disabled={busy}
              className="bg-destructive text-white hover:bg-destructive/90"
            >
              {t('friends.remove')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
