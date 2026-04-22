'use client';

import { useEffect, useState, useCallback } from 'react';
import Image from 'next/image';
import {
  BookOpen, Film, Tv, Activity,
  Clock, TrendingUp, BookMarked, RefreshCw,
} from 'lucide-react';
import { useAuth } from '@/lib/auth-context';
import { trackingService } from '@/lib/api/services';
import type { TrackingEvent, Book } from '@/lib/types';

function timeOfDay() {
  const h = new Date().getHours();
  if (h < 12) return 'morning';
  if (h < 18) return 'afternoon';
  return 'evening';
}

function formatDate(date: Date) {
  return date.toLocaleDateString('en-US', {
    weekday: 'long', year: 'numeric', month: 'long', day: 'numeric',
  });
}

function typeColor(type?: string) {
  switch (type) {
    case 'Book':  return { text: 'text-[oklch(0.65_0.13_65)]',    bg: 'bg-[oklch(0.65_0.13_65)]/10',    border: 'border-[oklch(0.65_0.13_65)]/20',    dot: 'bg-[oklch(0.65_0.13_65)]' };
    case 'Film':  return { text: 'text-[oklch(0.55_0.09_5)]',     bg: 'bg-[oklch(0.55_0.09_5)]/10',     border: 'border-[oklch(0.55_0.09_5)]/20',     dot: 'bg-[oklch(0.55_0.09_5)]' };
    case 'Serie': return { text: 'text-[oklch(0.52_0.09_152)]',   bg: 'bg-[oklch(0.52_0.09_152)]/10',   border: 'border-[oklch(0.52_0.09_152)]/20',   dot: 'bg-[oklch(0.52_0.09_152)]' };
    case 'Comic': return { text: 'text-[oklch(0.52_0.10_295)]',   bg: 'bg-[oklch(0.52_0.10_295)]/10',   border: 'border-[oklch(0.52_0.10_295)]/20',   dot: 'bg-[oklch(0.52_0.10_295)]' };
    default:      return { text: 'text-muted-foreground', bg: 'bg-secondary', border: 'border-border', dot: 'bg-muted-foreground' };
  }
}

function typeIcon(type?: string) {
  switch (type) {
    case 'Book':  return BookOpen;
    case 'Film':  return Film;
    case 'Serie': return Tv;
    case 'Comic': return BookMarked;
    default:      return Activity;
  }
}

interface Stats { books: number; films: number; series: number; comics: number; inProgress: number; }

function StatCard({
  label, value, icon: Icon, colorClass, bgClass,
}: { label: string; value: number; icon: React.ComponentType<{ className?: string }>; colorClass: string; bgClass: string }) {
  return (
    <div className="tt-card p-5 hover:shadow-md transition-all duration-200">
      <div className="flex items-start justify-between">
        <div>
          <p className="text-sm text-muted-foreground mb-1">{label}</p>
          <p className="text-3xl font-bold text-foreground">{value}</p>
        </div>
        <div className={`p-2.5 rounded-xl ${bgClass}`}>
          <Icon className={`w-5 h-5 ${colorClass}`} />
        </div>
      </div>
    </div>
  );
}

function ProgressBar({ value }: { value: number }) {
  const pct = Math.min(100, Math.max(0, value));
  return (
    <div className="w-full h-1.5 bg-secondary rounded-full overflow-hidden">
      <div
        className="h-full bg-primary rounded-full transition-all duration-500"
        style={{ width: `${pct}%` }}
      />
    </div>
  );
}

function InProgressCard({ event }: { event: TrackingEvent }) {
  const colors = typeColor(event.media?.type);
  const Icon = typeIcon(event.media?.type);
  return (
    <div className="tt-card p-4 hover:shadow-md transition-all duration-200">
      <div className="flex items-start gap-3">
        <div className={`p-2 rounded-xl ${colors.bg} flex-shrink-0`}>
          <Icon className={`w-4 h-4 ${colors.text}`} />
        </div>
        <div className="flex-1 min-w-0">
          <p className="font-medium text-foreground truncate text-sm">
            {event.media?.title ?? `Media #${event.mediaId}`}
          </p>
          <div className="flex items-center gap-2 mt-1 mb-3">
            <span className={`text-xs font-medium ${colors.text}`}>{event.media?.type}</span>
            <span className="text-xs text-muted-foreground/40">·</span>
            <span className="text-xs text-muted-foreground">{event.progress}%</span>
          </div>
          <ProgressBar value={event.progress} />
        </div>
      </div>
    </div>
  );
}

function ActivityItem({ event }: { event: TrackingEvent }) {
  const colors = typeColor(event.media?.type);
  const date = new Date(event.eventDate);
  const relativeTime = new Intl.RelativeTimeFormat('en', { numeric: 'auto' });
  const diffMs = date.getTime() - Date.now();
  const diffDays = Math.round(diffMs / 86400000);
  const diffHours = Math.round(diffMs / 3600000);
  const timeStr = Math.abs(diffDays) >= 1
    ? relativeTime.format(diffDays, 'day')
    : relativeTime.format(diffHours, 'hour');

  return (
    <div className="flex items-start gap-3 py-3 border-b border-border last:border-0">
      <div className={`w-2 h-2 rounded-full mt-1.5 flex-shrink-0 ${colors.dot}`} />
      <div className="flex-1 min-w-0">
        <p className="text-sm text-foreground truncate">
          {event.media?.title ?? `Media #${event.mediaId}`}
        </p>
        <div className="flex items-center gap-2 mt-0.5">
          <span className={`text-xs ${colors.text}`}>{event.media?.type}</span>
          <span className="text-xs text-muted-foreground/40">·</span>
          <span className="text-xs text-muted-foreground">{event.progress}% complete</span>
        </div>
      </div>
      <span className="text-xs text-muted-foreground/60 flex-shrink-0">{timeStr}</span>
    </div>
  );
}

export default function DashboardPage() {
  const { user } = useAuth();
  const [events, setEvents] = useState<TrackingEvent[]>([]);
  const [books, setBooks] = useState<Book[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [eventsRes, booksRes] = await Promise.all([
        trackingService.getTrackingEvents(undefined, 50, 'desc'),
        trackingService.getBooks(),
      ]);
      setEvents(eventsRes.data ?? []);
      setBooks(booksRes.data ?? []);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load data');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { fetchData(); }, [fetchData]);

  const latestPerMedia = Object.values(
    events.reduce<Record<number, TrackingEvent>>((acc, ev) => {
      if (!acc[ev.mediaId] || new Date(ev.eventDate) > new Date(acc[ev.mediaId].eventDate)) {
        acc[ev.mediaId] = ev;
      }
      return acc;
    }, {})
  );

  const stats: Stats = {
    books:      latestPerMedia.filter((e) => e.media?.type === 'Book').length,
    films:      latestPerMedia.filter((e) => e.media?.type === 'Film').length,
    series:     latestPerMedia.filter((e) => e.media?.type === 'Serie').length,
    comics:     latestPerMedia.filter((e) => e.media?.type === 'Comic').length,
    inProgress: latestPerMedia.filter((e) => e.progress < 100).length,
  };

  const inProgressEvents = latestPerMedia
    .filter((e) => e.progress < 100)
    .sort((a, b) => new Date(b.eventDate).getTime() - new Date(a.eventDate).getTime())
    .slice(0, 6);

  const recentActivity = [...events]
    .sort((a, b) => new Date(b.eventDate).getTime() - new Date(a.eventDate).getTime())
    .slice(0, 12);

  return (
    <div className="p-6 lg:p-8 max-w-7xl mx-auto">
      {/* Header */}
      <div className="mb-8 flex items-start justify-between">
        <div>
          <h1 className="font-heading text-2xl font-semibold">
            Good {timeOfDay()},{' '}
            <span className="text-primary italic">{user?.username ?? 'reader'}</span>
          </h1>
          <p className="text-muted-foreground mt-1 text-sm">{formatDate(new Date())}</p>
        </div>
        <button
          onClick={fetchData}
          disabled={loading}
          className="flex items-center gap-2 px-3 py-2 rounded-xl tt-card text-muted-foreground hover:text-foreground hover:bg-secondary/60 transition-colors text-sm cursor-pointer disabled:opacity-50"
          aria-label="Refresh"
        >
          <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
          <span className="hidden sm:inline">Refresh</span>
        </button>
      </div>

      {error && (
        <div className="mb-6 p-4 bg-destructive/10 border border-destructive/20 rounded-xl text-destructive text-sm">
          {error}
        </div>
      )}

      {/* Stats */}
      <div className="grid grid-cols-2 lg:grid-cols-5 gap-3 mb-8">
        <StatCard label="Books"       value={stats.books}      icon={BookOpen}   colorClass="text-[oklch(0.65_0.13_65)]"  bgClass="bg-[oklch(0.65_0.13_65)]/10" />
        <StatCard label="Films"       value={stats.films}      icon={Film}       colorClass="text-[oklch(0.55_0.09_5)]"   bgClass="bg-[oklch(0.55_0.09_5)]/10" />
        <StatCard label="Series"      value={stats.series}     icon={Tv}         colorClass="text-[oklch(0.52_0.09_152)]" bgClass="bg-[oklch(0.52_0.09_152)]/10" />
        <StatCard label="Comics"      value={stats.comics}     icon={BookMarked} colorClass="text-[oklch(0.52_0.10_295)]" bgClass="bg-[oklch(0.52_0.10_295)]/10" />
        <StatCard label="In Progress" value={stats.inProgress} icon={TrendingUp} colorClass="text-primary"               bgClass="bg-primary/10" />
      </div>

      {/* Main grid */}
      <div className="grid grid-cols-1 xl:grid-cols-3 gap-6">
        {/* In Progress */}
        <div className="xl:col-span-2">
          <div className="flex items-center justify-between mb-4">
            <h2 className="font-heading font-semibold text-lg">Currently In Progress</h2>
            <span className="text-xs text-muted-foreground bg-secondary border border-border px-2 py-1 rounded-full">
              {inProgressEvents.length} items
            </span>
          </div>

          {loading ? (
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              {Array.from({ length: 4 }).map((_, i) => (
                <div key={i} className="tt-card p-4 h-24 animate-pulse" />
              ))}
            </div>
          ) : inProgressEvents.length === 0 ? (
            <div className="tt-card p-12 text-center">
              <Activity className="w-8 h-8 text-muted-foreground/40 mx-auto mb-3" />
              <p className="text-muted-foreground text-sm">Nothing in progress yet.</p>
              <p className="text-muted-foreground/60 text-xs mt-1">Start reading or watching and your progress will appear here.</p>
            </div>
          ) : (
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              {inProgressEvents.map((event) => (
                <InProgressCard key={event.id} event={event} />
              ))}
            </div>
          )}

          {/* Books shelf */}
          {books.length > 0 && (
            <div className="mt-8">
              <div className="flex items-center justify-between mb-4">
                <h2 className="font-heading font-semibold text-lg">Book Shelf</h2>
                <span className="text-xs text-muted-foreground bg-secondary border border-border px-2 py-1 rounded-full">
                  {books.length} books
                </span>
              </div>
              <div className="flex gap-3 overflow-x-auto pb-2 scrollbar-hide">
                {books.slice(0, 12).map((book) => (
                  <div key={book.id} className="flex-shrink-0 w-20 group cursor-pointer">
                    {book.coverUrl ? (
                      <Image
                        src={book.coverUrl}
                        alt={book.title}
                        width={80}
                        height={112}
                        className="object-cover rounded-xl shadow-md group-hover:shadow-lg transition-shadow"
                      />
                    ) : (
                      <div className="w-20 h-28 bg-[oklch(0.65_0.13_65)]/15 border border-[oklch(0.65_0.13_65)]/20 rounded-xl flex items-center justify-center">
                        <BookOpen className="w-5 h-5 text-[oklch(0.65_0.13_65)]/60" />
                      </div>
                    )}
                    <p className="text-xs text-muted-foreground mt-1.5 line-clamp-2 leading-tight">
                      {book.title}
                    </p>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* Recent Activity */}
        <div>
          <div className="flex items-center gap-2 mb-4">
            <Clock className="w-4 h-4 text-muted-foreground" />
            <h2 className="font-heading font-semibold text-lg">Recent Activity</h2>
          </div>
          <div className="tt-card p-4">
            {loading ? (
              <div className="space-y-3">
                {Array.from({ length: 6 }).map((_, i) => (
                  <div key={i} className="h-10 bg-secondary/60 rounded-lg animate-pulse" />
                ))}
              </div>
            ) : recentActivity.length === 0 ? (
              <div className="py-8 text-center">
                <Clock className="w-6 h-6 text-muted-foreground/40 mx-auto mb-2" />
                <p className="text-muted-foreground text-sm">No activity yet.</p>
              </div>
            ) : (
              <div>
                {recentActivity.map((event) => (
                  <ActivityItem key={event.id} event={event} />
                ))}
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
