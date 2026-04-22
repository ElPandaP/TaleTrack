'use client';

import { useEffect, useState, useCallback } from 'react';
import Image from 'next/image';
import { Tv, Search, SortAsc, RefreshCw, CheckCircle, Play } from 'lucide-react';
import { trackingService } from '@/lib/api/services';
import type { TrackingEvent } from '@/lib/types';

const seriesColor = 'text-[oklch(0.52_0.09_152)]';
const seriesBg    = 'bg-[oklch(0.52_0.09_152)]/10';
const seriesBdr   = 'border-[oklch(0.52_0.09_152)]/20';

function ProgressBar({ value }: { value: number }) {
  return (
    <div className="w-full h-1.5 bg-secondary rounded-full overflow-hidden">
      <div
        className="h-full bg-[oklch(0.52_0.09_152)] rounded-full transition-all duration-700"
        style={{ width: `${Math.min(100, value)}%` }}
      />
    </div>
  );
}

function SeriesCard({ event }: { event: TrackingEvent }) {
  const done = event.progress >= 100;
  const watchDate = new Date(event.eventDate).toLocaleDateString('en-US', {
    month: 'short', year: 'numeric',
  });

  return (
    <div className={`group tt-card overflow-hidden hover:shadow-md transition-all duration-200 cursor-pointer`}>
      <div className={`w-full h-28 ${seriesBg} flex items-center justify-center relative overflow-hidden`}>
        {event.media?.posterUrl ? (
          <Image
            src={event.media.posterUrl}
            alt={event.media.title}
            fill
            className="object-cover group-hover:scale-105 transition-transform duration-500"
          />
        ) : (
          <Tv className={`w-8 h-8 ${seriesColor} opacity-30`} />
        )}
        <div className={`absolute top-2 right-2 flex items-center gap-1 px-2 py-1 rounded-full backdrop-blur-sm text-[10px] font-medium ${
          done
            ? `bg-[oklch(0.52_0.09_152)]/80 text-primary-foreground`
            : 'bg-foreground/60 ' + seriesColor
        }`}>
          {done
            ? <><CheckCircle className="w-3 h-3" /> Done</>
            : <><Play className="w-3 h-3" /> Watching</>
          }
        </div>
      </div>
      <div className="p-4">
        <h3 className="font-medium text-foreground text-sm line-clamp-1 mb-1">
          {event.media?.title ?? `Series #${event.mediaId}`}
        </h3>
        <p className="text-xs text-muted-foreground mb-3">{watchDate}</p>
        <ProgressBar value={event.progress} />
        <div className="flex items-center justify-between mt-2">
          <span className="text-xs text-muted-foreground">Progress</span>
          <span className={`text-xs font-medium ${seriesColor}`}>{event.progress}%</span>
        </div>
      </div>
    </div>
  );
}

type FilterMode = 'all' | 'done' | 'watching';
const controlClass = 'bg-secondary/60 border border-border rounded-xl text-sm text-foreground px-3 py-2.5 focus:outline-none focus:border-primary/50 cursor-pointer';

export default function SeriesPage() {
  const [events, setEvents] = useState<TrackingEvent[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [sort, setSort] = useState<'recent' | 'title' | 'progress'>('recent');
  const [filter, setFilter] = useState<FilterMode>('all');

  const fetchSeries = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await trackingService.getTrackingEvents('Serie', 200, 'desc');
      const latest = Object.values(
        (res.data ?? []).reduce<Record<number, TrackingEvent>>((acc, ev) => {
          if (!acc[ev.mediaId] || new Date(ev.eventDate) > new Date(acc[ev.mediaId].eventDate)) {
            acc[ev.mediaId] = ev;
          }
          return acc;
        }, {})
      );
      setEvents(latest);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load series');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { fetchSeries(); }, [fetchSeries]);

  const filtered = events
    .filter((e) => {
      const matchSearch = (e.media?.title ?? '').toLowerCase().includes(search.toLowerCase());
      const matchFilter =
        filter === 'all' ||
        (filter === 'done' && e.progress >= 100) ||
        (filter === 'watching' && e.progress < 100);
      return matchSearch && matchFilter;
    })
    .sort((a, b) => {
      if (sort === 'title') return (a.media?.title ?? '').localeCompare(b.media?.title ?? '');
      if (sort === 'progress') return b.progress - a.progress;
      return new Date(b.eventDate).getTime() - new Date(a.eventDate).getTime();
    });

  const filterOptions: { value: FilterMode; label: string }[] = [
    { value: 'all', label: 'All' },
    { value: 'watching', label: 'Watching' },
    { value: 'done', label: 'Finished' },
  ];

  return (
    <div className="p-6 lg:p-8 max-w-7xl mx-auto">
      <div className="mb-8">
        <div className="flex items-center gap-3 mb-2">
          <div className={`p-2 rounded-xl ${seriesBg}`}>
            <Tv className={`w-5 h-5 ${seriesColor}`} />
          </div>
          <h1 className="font-heading text-2xl font-semibold">Series</h1>
          {!loading && (
            <span className="ml-1 text-sm text-muted-foreground bg-secondary border border-border px-2.5 py-0.5 rounded-full">
              {events.length}
            </span>
          )}
        </div>
        <p className="text-muted-foreground text-sm ml-13">
          Every series you&apos;re following, tracked from Netflix automatically.
        </p>
      </div>

      <div className="flex flex-col sm:flex-row items-stretch sm:items-center gap-3 mb-6">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground/50" />
          <input
            type="text"
            placeholder="Search series…"
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
            onClick={fetchSeries}
            disabled={loading}
            className="p-2.5 rounded-xl bg-secondary/60 border border-border text-muted-foreground hover:text-foreground transition-colors cursor-pointer disabled:opacity-50 shrink-0"
          >
            <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
          </button>
        </div>
      </div>

      <div className="flex gap-1 mb-8 p-1 bg-secondary/40 border border-border rounded-xl w-fit">
        {filterOptions.map((opt) => (
          <button
            key={opt.value}
            onClick={() => setFilter(opt.value)}
            className={`px-4 py-1.5 rounded-lg text-sm font-medium transition-all duration-150 cursor-pointer ${
              filter === opt.value
                ? `${seriesBg} ${seriesColor} border ${seriesBdr}`
                : 'text-muted-foreground hover:text-foreground'
            }`}
          >
            {opt.label}
          </button>
        ))}
      </div>

      {error && (
        <div className="mb-6 p-4 bg-destructive/10 border border-destructive/20 rounded-xl text-destructive text-sm">
          {error}
        </div>
      )}

      {loading ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
          {Array.from({ length: 8 }).map((_, i) => (
            <div key={i} className="tt-card overflow-hidden animate-pulse">
              <div className="h-28 bg-secondary/60" />
              <div className="p-4 space-y-2">
                <div className="h-3 bg-secondary rounded w-3/4" />
                <div className="h-2.5 bg-secondary/60 rounded w-1/4" />
                <div className="h-1.5 bg-secondary rounded" />
              </div>
            </div>
          ))}
        </div>
      ) : filtered.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-24 text-center">
          <div className={`w-16 h-16 rounded-2xl ${seriesBg} border ${seriesBdr} flex items-center justify-center mb-4`}>
            <Tv className={`w-7 h-7 ${seriesColor} opacity-60`} />
          </div>
          <p className="text-foreground font-medium mb-1">
            {search ? 'No series found' : 'No series yet'}
          </p>
          <p className="text-muted-foreground text-sm max-w-xs">
            {search
              ? 'Try a different search term.'
              : 'Install the Netflix extension and start watching a series to track it here.'}
          </p>
        </div>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
          {filtered.map((event) => (
            <SeriesCard key={event.id} event={event} />
          ))}
        </div>
      )}
    </div>
  );
}
