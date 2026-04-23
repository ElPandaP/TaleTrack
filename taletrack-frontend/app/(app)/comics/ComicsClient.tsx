'use client';

import { useTransition, useState } from 'react';
import Image from 'next/image';
import { useRouter } from 'next/navigation';
import { BookMarked, Search, SortAsc, RefreshCw } from 'lucide-react';
import type { TrackingEvent } from '@/lib/types';

const comicColor = 'text-[oklch(0.52_0.10_295)]';
const comicBg    = 'bg-[oklch(0.52_0.10_295)]/10';
const comicBdr   = 'border-[oklch(0.52_0.10_295)]/20';

function ProgressBar({ value }: { value: number }) {
  return (
    <div className="w-full h-1.5 bg-secondary rounded-full overflow-hidden">
      <div
        className="h-full bg-[oklch(0.52_0.10_295)] rounded-full transition-all duration-700"
        style={{ width: `${Math.min(100, value)}%` }}
      />
    </div>
  );
}

function ComicCard({ event }: { event: TrackingEvent }) {
  const done = event.progress >= 100;
  const date = new Date(event.eventDate).toLocaleDateString('en-US', {
    month: 'short', year: 'numeric',
  });

  return (
    <div className="group tt-card p-4 hover:shadow-md transition-all duration-200 cursor-pointer">
      <div className={`w-full aspect-2/3 ${comicBg} border ${comicBdr} rounded-xl mb-3 flex items-center justify-center overflow-hidden relative`}>
        {event.media?.posterUrl ? (
          <Image
            src={event.media.posterUrl}
            alt={event.media.title}
            fill
            className="object-cover group-hover:scale-105 transition-transform duration-500"
          />
        ) : (
          <BookMarked className={`w-8 h-8 ${comicColor} opacity-30`} />
        )}
      </div>
      <h3 className="font-medium text-foreground text-sm line-clamp-2 leading-snug mb-1">
        {event.media?.title ?? `Comic #${event.mediaId}`}
      </h3>
      <p className="text-xs text-muted-foreground mb-3">{date}</p>
      <ProgressBar value={event.progress} />
      <div className="flex items-center justify-between mt-1.5">
        <span className="text-xs text-muted-foreground">{done ? 'Read' : 'Reading'}</span>
        <span className={`text-xs font-medium ${comicColor}`}>{event.progress}%</span>
      </div>
    </div>
  );
}

const controlClass = 'bg-secondary/60 border border-border rounded-xl text-sm text-foreground px-3 py-2.5 focus:outline-none focus:border-primary/50 cursor-pointer';

export default function ComicsClient({ initialEvents }: { initialEvents: TrackingEvent[] }) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();
  const [search, setSearch] = useState('');
  const [sort, setSort] = useState<'recent' | 'title' | 'progress'>('recent');

  const refresh = () => startTransition(() => { router.refresh(); });

  const filtered = initialEvents
    .filter((e) => (e.media?.title ?? '').toLowerCase().includes(search.toLowerCase()))
    .sort((a, b) => {
      if (sort === 'title') return (a.media?.title ?? '').localeCompare(b.media?.title ?? '');
      if (sort === 'progress') return b.progress - a.progress;
      return new Date(b.eventDate).getTime() - new Date(a.eventDate).getTime();
    });

  return (
    <div className="p-6 lg:p-8 max-w-7xl mx-auto">
      <div className="mb-8">
        <div className="flex items-center gap-3 mb-2">
          <div className={`p-2 rounded-xl ${comicBg}`}>
            <BookMarked className={`w-5 h-5 ${comicColor}`} />
          </div>
          <h1 className="font-heading text-2xl font-semibold">Comics</h1>
          <span className="ml-1 text-sm text-muted-foreground bg-secondary border border-border px-2.5 py-0.5 rounded-full">
            {initialEvents.length}
          </span>
        </div>
        <p className="text-muted-foreground text-sm ml-13">
          Your comic collection, tracked from KOReader.
        </p>
      </div>

      <div className="flex flex-col sm:flex-row items-stretch sm:items-center gap-3 mb-8">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground/50" />
          <input
            type="text"
            placeholder="Search comics…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="tt-input pl-10 pr-4 py-2.5"
          />
        </div>
        <div className="flex items-center gap-2">
          <SortAsc className="w-4 h-4 text-muted-foreground/50 shrink-0" />
          <select
            value={sort}
            onChange={(e) => setSort(e.target.value as 'recent' | 'title' | 'progress')}
            className={controlClass}
          >
            <option value="recent">Most recent</option>
            <option value="title">Title A–Z</option>
            <option value="progress">Progress</option>
          </select>
          <button
            onClick={refresh}
            disabled={isPending}
            className="p-2.5 rounded-xl bg-secondary/60 border border-border text-muted-foreground hover:text-foreground transition-colors cursor-pointer disabled:opacity-50 shrink-0"
          >
            <RefreshCw className={`w-4 h-4 ${isPending ? 'animate-spin' : ''}`} />
          </button>
        </div>
      </div>

      {filtered.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-24 text-center">
          <div className={`w-16 h-16 rounded-2xl ${comicBg} border ${comicBdr} flex items-center justify-center mb-4`}>
            <BookMarked className={`w-7 h-7 ${comicColor} opacity-60`} />
          </div>
          <p className="text-foreground font-medium mb-1">
            {search ? 'No comics found' : 'No comics yet'}
          </p>
          <p className="text-muted-foreground text-sm max-w-xs">
            {search
              ? 'Try a different search term.'
              : 'Start reading comics on KOReader and they will appear here.'}
          </p>
        </div>
      ) : (
        <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4">
          {filtered.map((event) => (
            <ComicCard key={event.id} event={event} />
          ))}
        </div>
      )}
    </div>
  );
}
