import { getActivity } from '@/lib/api/server';
import type { ActivityItem } from '@/lib/types';
import ActivityClient, { type Scope } from './ActivityClient';

const FEED_LIMIT = 80;

/** Fetches one scope of the feed; a failed request yields an empty list (ActivityClient shows the empty state). */
async function loadFeed(scope: Scope): Promise<ActivityItem[]> {
  try {
    const res = await getActivity(scope, FEED_LIMIT);
    return res.data ?? [];
  } catch {
    return [];
  }
}

/** `/activity`: the feed of the user's and their friends' recent activity. */
export default async function ActivityPage() {
  const [all, mine, friends] = await Promise.all([
    loadFeed('all'),
    loadFeed('mine'),
    loadFeed('friends'),
  ]);
  return <ActivityClient feeds={{ all, mine, friends }} />;
}
