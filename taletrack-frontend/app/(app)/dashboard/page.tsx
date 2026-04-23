import { getTrackingEvents, getBooks } from '@/lib/api/server';
import { headers } from 'next/headers';
import DashboardClient from './DashboardClient';

export default async function DashboardPage() {
  const [eventsRes, booksRes] = await Promise.allSettled([
    getTrackingEvents(undefined, 50),
    getBooks(),
  ]);

  const events = eventsRes.status === 'fulfilled' ? (eventsRes.value.data ?? []) : [];
  const books  = booksRes.status  === 'fulfilled' ? (booksRes.value.data  ?? []) : [];
  const requestDateHeader = (await headers()).get('date');
  const initialNow = requestDateHeader ? Date.parse(requestDateHeader) : 0;

  return <DashboardClient initialEvents={events} initialBooks={books} initialNow={initialNow} />;
}
