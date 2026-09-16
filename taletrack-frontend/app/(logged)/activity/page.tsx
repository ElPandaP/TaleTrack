import { getActivity } from '@/lib/api/server';
import type { ActivityItem } from '@/lib/types';
import ActivityClient from './ActivityClient';

export default async function ActivityPage() {
  let items: ActivityItem[] = [];
  try {
    const res = await getActivity('all', 80);
    items = res.data ?? [];
  } catch {
    // empty state in the client
  }
  return <ActivityClient items={items} />;
}
