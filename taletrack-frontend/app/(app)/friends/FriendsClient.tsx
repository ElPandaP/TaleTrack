'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { Check, X, UserPlus, UserMinus, Search, Clock } from 'lucide-react';
import { UserAvatar } from '@/components/media/user-avatar';
import { Button } from '@/components/ui/button';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from '@/components/ui/dialog';
import { friendService } from '@/lib/api/services';
import { sendErrorKey } from '@/lib/api/friend-errors';
import { useT } from '@/lib/i18n';
import type { Friend, FriendRequest, UserRelationship } from '@/lib/types';

interface SearchState {
  loading: boolean;
  done: boolean;
  user: { userId: number; username: string; avatarUrl?: string | null } | null;
  relationship?: UserRelationship;
  sent: boolean;
  error: string | null;
}

const emptySearch: SearchState = { loading: false, done: false, user: null, sent: false, error: null };

function PersonLink({
  userId,
  username,
  avatarUrl,
  size = 'md',
}: {
  userId: number;
  username: string;
  avatarUrl?: string | null;
  size?: 'sm' | 'md';
}) {
  return (
    <Link href={`/u/${userId}`} className="group flex min-w-0 flex-1 items-center gap-3">
      <UserAvatar username={username} avatarUrl={avatarUrl} size={size} />
      <span className="truncate text-sm font-medium group-hover:text-primary">@{username}</span>
    </Link>
  );
}

export default function FriendsClient({
  friends,
  incoming,
  outgoing,
}: {
  friends: Friend[];
  incoming: FriendRequest[];
  outgoing: FriendRequest[];
}) {
  const router = useRouter();
  const t = useT();
  const [busy, setBusy] = useState<number | null>(null);
  const [query, setQuery] = useState('');
  const [search, setSearch] = useState<SearchState>(emptySearch);
  const [removeTarget, setRemoveTarget] = useState<Friend | null>(null);

  const respond = async (requestId: number, accept: boolean) => {
    setBusy(requestId);
    try {
      await friendService.respond(requestId, accept);
      router.refresh();
    } finally {
      setBusy(null);
    }
  };

  const confirmRemove = async () => {
    const f = removeTarget;
    if (!f) return;
    setBusy(f.userId);
    try {
      await friendService.remove(f.userId);
      router.refresh();
    } finally {
      setBusy(null);
      setRemoveTarget(null);
    }
  };

  const runSearch = async (e: React.FormEvent) => {
    e.preventDefault();
    const q = query.trim().replace(/^@/, '');
    if (!q) return;
    setSearch({ ...emptySearch, loading: true });
    try {
      const res = await friendService.searchByUsername(q);
      setSearch({
        loading: false,
        done: true,
        user: res.user,
        relationship: res.relationship,
        sent: false,
        error: null,
      });
    } catch {
      setSearch({ ...emptySearch, done: true, error: t('friends.searchError') });
    }
  };

  const sendRequest = async () => {
    if (!search.user) return;
    setSearch((s) => ({ ...s, loading: true }));
    try {
      await friendService.sendRequest(search.user.userId);
      setSearch((s) => ({ ...s, loading: false, sent: true }));
      router.refresh();
    } catch (err) {
      setSearch((s) => ({ ...s, loading: false, error: t(sendErrorKey(err)) }));
    }
  };

  const relMessage: Record<Exclude<UserRelationship, 'none'>, string> = {
    self: t('friends.rel.self'),
    friends: t('friends.rel.friends'),
    outgoing: t('friends.rel.outgoing'),
    incoming: t('friends.rel.incoming'),
  };

  return (
    <div>
      <h1 className="mb-6 font-heading text-2xl font-semibold">{t('friends.title')}</h1>

      {/* Add a friend */}
      <section className="tt-card mb-6 p-5">
        <h2 className="flex items-center gap-2 font-heading text-lg font-semibold">
          <UserPlus aria-hidden="true" className="size-4 text-muted-foreground" />
          {t('friends.add')}
        </h2>
        <p className="mt-1 mb-3 text-xs text-muted-foreground">{t('friends.add.hint')}</p>

        <form onSubmit={runSearch} className="flex max-w-md gap-2">
          <div className="relative flex-1">
            <Search
              aria-hidden="true"
              className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground/50"
            />
            <input
              type="text"
              required
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder={t('friends.add.placeholder')}
              className="tt-input py-2 pr-3 pl-9"
            />
          </div>
          <Button type="submit" disabled={search.loading}>
            {t('friends.add.search')}
          </Button>
        </form>

        {search.done && !search.error && (
          <div className="mt-3 max-w-md rounded-xl border border-border bg-secondary/30 p-3">
            {search.sent ? (
              <p className="text-sm text-primary">{t('friends.add.sent')}</p>
            ) : !search.user ? (
              <p className="text-sm text-muted-foreground">{t('friends.add.notFound')}</p>
            ) : (
              <div className="flex items-center gap-3">
                <PersonLink {...search.user} />
                {search.relationship && search.relationship !== 'none' ? (
                  <span className="shrink-0 text-xs text-muted-foreground">
                    {relMessage[search.relationship]}
                  </span>
                ) : (
                  <Button size="sm" onClick={sendRequest} disabled={search.loading}>
                    {t('friends.add.send')}
                  </Button>
                )}
              </div>
            )}
          </div>
        )}
        {search.error && <p className="mt-3 text-sm text-destructive">{search.error}</p>}
      </section>

      {/* Incoming requests */}
      <section className="mb-6">
        <h2 className="mb-3 font-heading text-lg font-semibold">{t('friends.requests')}</h2>
        {incoming.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t('friends.requests.empty')}</p>
        ) : (
          <ul className="flex flex-col gap-2">
            {incoming.map((r) => (
              <li key={r.requestId} className="tt-card flex items-center gap-3 p-3">
                <PersonLink userId={r.userId} username={r.username} avatarUrl={r.avatarUrl} />
                <Button
                  type="button"
                  variant="ghost"
                  size="icon-sm"
                  onClick={() => respond(r.requestId, true)}
                  disabled={busy === r.requestId}
                  aria-label={t('friends.accept')}
                  className="bg-[oklch(0.55_0.15_150)]/15 text-[oklch(0.5_0.16_150)] hover:bg-[oklch(0.55_0.15_150)]/25 dark:text-[oklch(0.7_0.17_150)]"
                >
                  <Check aria-hidden="true" className="size-4" />
                </Button>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon-sm"
                  onClick={() => respond(r.requestId, false)}
                  disabled={busy === r.requestId}
                  aria-label={t('friends.decline')}
                  className="bg-destructive/10 text-destructive hover:bg-destructive/20"
                >
                  <X aria-hidden="true" className="size-4" />
                </Button>
              </li>
            ))}
          </ul>
        )}
      </section>

      {/* Friends list */}
      <section>
        <h2 className="mb-3 font-heading text-lg font-semibold">{t('friends.list')}</h2>
        {friends.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t('friends.list.empty')}</p>
        ) : (
          <ul className="grid gap-2 sm:grid-cols-2">
            {friends.map((f) => (
              <li key={f.userId} className="tt-card flex items-center gap-3 p-3">
                <PersonLink userId={f.userId} username={f.username} avatarUrl={f.avatarUrl} />
                <Button
                  type="button"
                  variant="ghost"
                  size="icon-sm"
                  onClick={() => setRemoveTarget(f)}
                  disabled={busy === f.userId}
                  aria-label={t('friends.remove')}
                  title={t('friends.remove')}
                  className="text-muted-foreground/60 hover:bg-destructive/10 hover:text-destructive"
                >
                  <UserMinus aria-hidden="true" className="size-4" />
                </Button>
              </li>
            ))}
          </ul>
        )}

        {outgoing.length > 0 && (
          <div className="mt-5">
            <p className="mb-2 text-[11px] font-semibold tracking-widest text-muted-foreground/70 uppercase">
              {t('friends.outgoing')}
            </p>
            <ul className="flex flex-col gap-1">
              {outgoing.map((r) => (
                <li
                  key={r.requestId}
                  className="flex items-center gap-3 px-3 py-2 text-sm text-muted-foreground"
                >
                  <PersonLink userId={r.userId} username={r.username} avatarUrl={r.avatarUrl} size="sm" />
                  <span className="flex shrink-0 items-center gap-1 text-xs">
                    <Clock aria-hidden="true" className="size-3" />
                    {t('friends.pendingBadge')}
                  </span>
                </li>
              ))}
            </ul>
          </div>
        )}
      </section>

      <Dialog open={removeTarget != null} onOpenChange={(open) => !open && setRemoveTarget(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('friends.removeConfirmTitle')}</DialogTitle>
            {removeTarget && (
              <DialogDescription>
                {t('friends.removeConfirm', { name: `@${removeTarget.username}` })}
              </DialogDescription>
            )}
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRemoveTarget(null)}>
              {t('friends.cancel')}
            </Button>
            <Button
              variant="destructive"
              onClick={confirmRemove}
              disabled={busy === removeTarget?.userId}
              className="bg-destructive text-white hover:bg-destructive/90"
            >
              {t('friends.remove')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
