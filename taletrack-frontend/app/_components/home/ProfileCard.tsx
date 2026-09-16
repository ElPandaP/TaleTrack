'use client';

import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { UserAvatar } from '@/components/media/UserAvatar';
import { useI18n } from '@/lib/i18n';

export function ProfileCard({
  username,
  avatarUrl,
  total,
  reviewCount,
}: {
  username: string;
  avatarUrl?: string | null;
  total: number;
  reviewCount: number;
}) {
  const { t, tp } = useI18n();

  return (
    <div className="tt-card flex flex-col items-center gap-2 p-4 text-center">
      <UserAvatar username={username} avatarUrl={avatarUrl} size="lg" />
      <p className="font-medium text-foreground">@{username}</p>
      <p className="text-xs text-muted-foreground">
        {t('common.thisYear', { count: total })} · {tp('common.reviews', reviewCount)}
      </p>
      <Button asChild variant="outline" size="sm" className="mt-1 w-full">
        <Link href="/profile">{t('home.profile.viewProfile')}</Link>
      </Button>
    </div>
  );
}
