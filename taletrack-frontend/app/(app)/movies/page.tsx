'use client';

import { useEffect, useState, useCallback } from 'react';
import Image from 'next/image';
import { Film, Search, SortAsc, RefreshCw, CheckCircle, Clock } from 'lucide-react';
import { trackingService } from '@/lib/api/services';
import type { TrackingEvent } from '@/lib/types';

const filmColor = 'text-[oklch(0.55_0.09_5)]';
const filmBg    = 'bg-[oklch(0.55_0.09_5)]/10';
const filmBdr   = 'border-[oklch(0.55_0.09_5)]/20';
const filmStroke = 'oklch(0.55 0.09 5)';

function ProgressRing({ progress }: { progress: number }) {
  const r = 16;
  const circ = 2 * Math.PI * r;
  const offset = circ - (progress / 100) * circ;
  return (
    <svg className="w-10 h-10 -rotate-90" viewBox="0 0 40 40">
      <circle cx="20" cy="20" r={r} fill="none" stroke="currentColor" strokeWidth="3" className="text-border" />
      <circle
        cx="20" cy="20" r={r}
        fill="none" stroke={filmStroke}
        strokeWidth="3" strokeLinecap="round"
        strokeDasharray={circ} strokeDashoffset={offset}
        className="transition-all duration-700"
      />
    </svg>
  );
}

function MovieCard({ event }: { event: TrackingEvent }) {
  const done = event.progress >= 100;
  const watchDate = new Date(event.eventDate).toLocaleDateString('en-US', {
    month: 'short', day: 'numeric', year: 'numeric',
  });

  return (
    <div className={`group tt-card p-4 hover:shadow-md hover:${filmBdr} transition-all duration-200 cursor-pointer`}>
      <div className={`w-full aspect-2/3 ${filmBg} border ${filmBdr} rounded-xl mb-4 flex items-center justify-center overflow-hidden relative`}>
        {event.media?.posterUrl ? (
          <Image
            src={event.media.posterUrl}
            alt={event.media.title}
            fill
            className="object-cover group-hover:scale-105 transition-transform duration-500"
          />
        ) : (
          <Film className={`w-10 h-10 ${filmColor} opacity-30`} />
        )}
        {done && (
          <div className={`absolute top-2 right-2 ${filmBg} border ${filmBdr} backdrop-blur-sm rounded-full p-1`}>
            <CheckCircle className={`w-3.5 h-3.5 ${filmColor}`} />
          </div>
        )}
      </div>
      <h3 className="font-medium text-foreground text-sm line-clamp-2 leading-snug mb-2">
        {event.media?.title ?? `Film #${event.mediaId}`}
      </h3>
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-1.5">
          <Clock className="w-3 h-3 text-muted-foreground/50" />
          <span className="text-xs text-muted-foreground">{watchDate}</span>
        </div>
        <div className="relative flex items-center justify-center">
          <ProgressRing progress={event.progress} />
          <span className={`absolute text-[9px] font-bold ${filmColor}`}>{event.progress}</span>
        </div>
      </div>
    </div>
  );
}

type FilterMode = 'all' | 'done' | 'progress';
const controlClass = 'bg-secondary/60 border border-border rounded-xl text-sm text-foreground px-3 py-2.5 focus:outline-none focus:border-primary/50 cursor-pointer';

export default function MoviesPage() {
  const [events, setEvents] = useState<TrackingEvent[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [sort, setSort] = useState<'recent' | 'title' | 'progress'>('recent');
  const [filter, setFilter] = useState<FilterMode>('all');

  const fetchMovies = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await trackingService.getTrackingEvents('Film', 200, 'desc');
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
      setError(e instanceof Error ? e.message : 'Failed to load movies');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { fetchMovies(); }, [fetchMovies]);

  const filtered = events
    .filter((e) => {
      const matchSearch = (e.media?.title ?? '').toLowerCase().includes(search.toLowerCase());
      const matchFilter =
        filter === 'all' ||
        (filter === 'done' && e.progress >= 100) ||
        (filter === 'progress' && e.progress < 100);
      return matchSearch && matchFilter;
    })
    .sort((a, b) => {
      if (sort === 'title') return (a.media?.title ?? '').localeCompare(b.media?.title ?? '');
      if (sort === 'progress') return b.progress - a.progress;
      return new Date(b.eventDate).getTime() - new Date(a.eventDate).getTime();
    });

  const filterOptions: { value: FilterMode; label: string }[] = [
    { value: 'all', label: 'All' },
    { value: 'done', label: 'Watched' },
    { value: 'progress', label: 'In Progress' },
  ];

  return (
    <div className="p-6 lg:p-8 max-w-7xl mx-auto">
      <div className="mb-8">
        <div className="flex items-center gap-3 mb-2">
          <div className={`p-2 rounded-xl ${filmBg}`}>
            <Film className={`w-5 h-5 ${filmColor}`} />
          </div>
          <h1 className="font-heading text-2xl font-semibold">Movies</h1>
          {!loading && (
            <span className="ml-1 text-sm text-muted-foreground bg-secondary border border-border px-2.5 py-0.5 rounded-full">
              {events.length}
            </span>
          )}
        </div>
        <p className="text-muted-foreground text-sm ml-13">
          Your film collection, tracked automatically via the Netflix extension.
        </p>
      </div>

      <div className="flex flex-col sm:flex-row items-stretch sm:items-center gap-3 mb-6">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground/50" />
          <input
            type="text"
            placeholder="Search movies…"
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
            onClick={fetchMovies}
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
                ? `${filmBg} ${filmColor} border ${filmBdr}`
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
        <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-4">
          {Array.from({ length: 10 }).map((_, i) => (
            <div key={i} className="tt-card p-4 animate-pulse">
              <div className="aspect-2/3 bg-secondary/60 rounded-xl mb-3" />
              <div className="h-3 bg-secondary rounded w-3/4 mb-2" />
              <div className="h-2.5 bg-secondary/60 rounded w-1/2" />
            </div>
          ))}
        </div>
      ) : filtered.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-24 text-center">
          <div className={`w-16 h-16 rounded-2xl ${filmBg} border ${filmBdr} flex items-center justify-center mb-4`}>
            <Film className={`w-7 h-7 ${filmColor} opacity-60`} />
          </div>
          <p className="text-foreground font-medium mb-1">
            {search ? 'No movies found' : 'No movies yet'}
          </p>
          <p className="text-muted-foreground text-sm max-w-xs">
            {search
              ? 'Try a different search term.'
              : 'Install the Netflix browser extension and watch something to start tracking.'}
          </p>
        </div>
      ) : (
        <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-4">
          {filtered.map((event) => (
            <MovieCard key={event.id} event={event} />
          ))}
        </div>
      )}
    </div>
  );
}
