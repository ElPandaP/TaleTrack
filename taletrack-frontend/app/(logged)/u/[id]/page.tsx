import { notFound } from 'next/navigation';
import { getUserProfile, getUserActivity } from '@/lib/api/server';
import type { ActivityItem, PublicProfile } from '@/lib/types';
import PublicProfileClient from './PublicProfileClient';

/** `/u/[id]`: another user's public profile and the activity the viewer may see. */
export default async function UserProfilePage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;

  let profile: PublicProfile | undefined;
  let activity: ActivityItem[] = [];
  // allSettled never throws: a failed profile request just leaves `profile` empty (404 below).
  const [pRes, aRes] = await Promise.allSettled([getUserProfile(id), getUserActivity(id)]);
  if (pRes.status === 'fulfilled') profile = pRes.value.data;
  if (aRes.status === 'fulfilled') activity = aRes.value.data ?? [];

  if (!profile) notFound();

  return <PublicProfileClient profile={profile} activity={activity} />;
}
