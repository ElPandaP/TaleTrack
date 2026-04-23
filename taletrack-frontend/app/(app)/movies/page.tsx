import { getTrackingEvents } from '@/lib/api/server';
import MoviesClient from './MoviesClient';
import type { TrackingEvent } from '@/lib/types';

function deduplicateLatest(events: TrackingEvent[]): TrackingEvent[] {
  return Object.values(
    events.reduce<Record<number, TrackingEvent>>((acc, ev) => {
      if (!acc[ev.mediaId] || new Date(ev.eventDate) > new Date(acc[ev.mediaId].eventDate)) {
        acc[ev.mediaId] = ev;
      }
      return acc;
    }, {})
  );
}

export default async function MoviesPage() {
  let events: TrackingEvent[] = [];
  try {
    const res = await getTrackingEvents('Film', 200);
    events = deduplicateLatest(res.data ?? []);
  } catch {
    // empty state shown in client
  }
  return <MoviesClient initialEvents={events} />;
}
