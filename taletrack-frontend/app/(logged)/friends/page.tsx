import { getFriends } from '@/lib/api/server';
import type { Friend, FriendRequest } from '@/lib/types';
import FriendsClient from './FriendsClient';

/** `/friends`: friend list, pending requests and the search to add friends. */
export default async function FriendsPage() {
  let friends: Friend[] = [];
  let incoming: FriendRequest[] = [];
  let outgoing: FriendRequest[] = [];
  try {
    const res = await getFriends();
    friends = res.friends ?? [];
    incoming = res.incoming ?? [];
    outgoing = res.outgoing ?? [];
  } catch {
    // FriendsClient shows the empty lists.
  }
  return <FriendsClient friends={friends} incoming={incoming} outgoing={outgoing} />;
}
