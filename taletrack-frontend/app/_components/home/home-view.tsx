'use client';

import { useState } from 'react';
import { ReviewModal, type ReviewTarget } from '@/components/media/review-modal';
import { useT } from '@/lib/i18n';
import type { LibraryItem, LibraryType, YearlyStats } from '@/lib/types';
import { ProfileCard } from './profile-card';
import { MonthlyStatsCard } from './monthly-stats-card';
import { TypeCarousel } from './type-carousel';
import { InProgressCard } from './in-progress-card';
import { PendingReviewsCard } from './pending-reviews-card';

export interface HomeData {
  username: string;
  avatarUrl: string | null;
  stats: YearlyStats | null;
  books: LibraryItem[];
  movies: LibraryItem[];
  series: LibraryItem[];
  totals: Record<LibraryType, number>;
  inProgress: LibraryItem[];
  pending: LibraryItem[];
}

export default function HomeView({ data }: { data: HomeData }) {
  const t = useT();
  const [target, setTarget] = useState<ReviewTarget | null>(null);
  const [modalOpen, setModalOpen] = useState(false);

  const openReview = (next: ReviewTarget) => {
    setTarget(next);
    setModalOpen(true);
  };

  const [greetBefore, greetAfter = ''] = t('home.greeting').split('{name}');

  return (
    <>
      <h1 className="mb-6 font-heading text-3xl font-semibold">
        {greetBefore}
        <span className="text-primary italic">{data.username}</span>
        {greetAfter}
      </h1>

      <div className="grid gap-6 lg:grid-cols-[240px_minmax(0,1fr)] xl:grid-cols-[240px_minmax(0,1fr)_280px]">
        {/* Left: identity + stats */}
        <aside className="flex flex-col gap-4">
          <ProfileCard
            username={data.username}
            avatarUrl={data.avatarUrl}
            total={data.stats?.total ?? 0}
            reviewCount={data.stats?.reviewCount ?? 0}
          />
          <MonthlyStatsCard stats={data.stats} />
        </aside>

        {/* Center: carousels by type */}
        <div className="flex min-w-0 flex-col gap-7">
          <TypeCarousel type="Book" items={data.books} total={data.totals.Book} />
          <TypeCarousel type="Movie" items={data.movies} total={data.totals.Movie} />
          <TypeCarousel type="Series" items={data.series} total={data.totals.Series} />
        </div>

        {/* Right: quick access (wraps below on small screens) */}
        <aside className="grid gap-4 sm:grid-cols-2 lg:col-span-2 xl:col-span-1 xl:block xl:space-y-4">
          <InProgressCard items={data.inProgress} />
          <PendingReviewsCard items={data.pending} onReview={openReview} />
        </aside>
      </div>

      <ReviewModal target={target} open={modalOpen} onOpenChange={setModalOpen} />
    </>
  );
}
