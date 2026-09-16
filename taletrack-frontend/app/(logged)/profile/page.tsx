import { redirect } from 'next/navigation';
import { getMe, getUserProfile } from '@/lib/api/server';
import type { UserProfile, PublicProfile } from '@/lib/types';
import ProfileClient from './ProfileClient';

export default async function ProfilePage() {
  let profile: UserProfile | null = null;
  let counts: PublicProfile['counts'] | null = null;

  try {
    profile = (await getMe()).data;
  } catch {
    // handled below
  }

  if (!profile) redirect('/login');

  try {
    counts = (await getUserProfile(profile.id)).data.counts;
  } catch {
    // counts stay null; ProfileClient shows zeros
  }

  return <ProfileClient profile={profile} counts={counts} />;
}
