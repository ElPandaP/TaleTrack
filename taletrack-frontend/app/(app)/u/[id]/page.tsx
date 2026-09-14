import { notFound } from 'next/navigation';
import { getUserProfile, getUserActivity } from '@/lib/api/server';
import type { ActivityItem, PublicProfile } from '@/lib/types';
import PublicProfileClient from './PublicProfileClient';

export default async function UserProfilePage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;

  let profile: PublicProfile | undefined;
  let activity: ActivityItem[] = [];
  try {
    const [pRes, aRes] = await Promise.allSettled([getUserProfile(id), getUserActivity(id)]);
    if (pRes.status === 'fulfilled') profile = pRes.value.data;
    if (aRes.status === 'fulfilled') activity = aRes.value.data ?? [];
  } catch {
    // handled below
  }

  if (!profile) notFound();

  return <PublicProfileClient profile={profile} activity={activity} />;
}
