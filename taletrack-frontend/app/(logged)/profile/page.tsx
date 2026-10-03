import { redirect } from 'next/navigation';
import { getMe, getUserProfile } from '@/lib/api/server';
import type { UserProfile, PublicProfile } from '@/lib/types';
import ProfileClient from './ProfileClient';

/** `/profile`: the user's own profile, settings and account actions. */
export default async function ProfilePage() {
  let profile: UserProfile | null = null;
  let counts: PublicProfile['counts'] | null = null;

  try {
    profile = (await getMe()).data;
  } catch {
    // Without a profile the session is unusable: redirect below.
  }

  if (!profile) redirect('/login');

  try {
    counts = (await getUserProfile(profile.id)).data.counts;
  } catch {
    // Counts stay null and ProfileClient shows zeros.
  }

  return <ProfileClient profile={profile} counts={counts} />;
}
