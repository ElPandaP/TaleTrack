'use client';

import { useEffect, useState, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import {
  User, Mail, BookOpen, Film, Tv, BookMarked,
  Activity, Calendar, LogOut, Trash2, Save,
} from 'lucide-react';
import { useAuth } from '@/lib/auth-context';
import { trackingService } from '@/lib/api/services';
import { apiClient } from '@/lib/api/client';
import type { TrackingEvent } from '@/lib/types';

function StatPill({
  label, value, icon: Icon, colorClass, bgClass,
}: {
  label: string; value: number;
  icon: React.ComponentType<{ className?: string }>;
  colorClass: string; bgClass: string;
}) {
  return (
    <div className={`flex items-center gap-3 px-4 py-3 rounded-xl ${bgClass} border border-border`}>
      <Icon className={`w-4 h-4 ${colorClass}`} />
      <div>
        <p className="text-xl font-bold text-foreground">{value}</p>
        <p className={`text-xs ${colorClass} opacity-80`}>{label}</p>
      </div>
    </div>
  );
}

function Avatar({ username }: { username: string }) {
  const initials = username.split(' ').map((w) => w[0]).join('').slice(0, 2).toUpperCase();
  return (
    <div className="w-20 h-20 rounded-2xl bg-primary/20 border border-primary/30 flex items-center justify-center text-2xl font-bold text-primary shadow-lg">
      {initials}
    </div>
  );
}

export default function ProfilePage() {
  const { user, logout } = useAuth();
  const router = useRouter();
  const [events, setEvents] = useState<TrackingEvent[]>([]);
  const [loading, setLoading] = useState(true);
  const [editUsername, setEditUsername] = useState(user?.username ?? '');
  const [editEmail, setEditEmail] = useState(user?.email ?? '');
  const [saving, setSaving] = useState(false);
  const [saveMsg, setSaveMsg] = useState<string | null>(null);
  const [deleteConfirm, setDeleteConfirm] = useState(false);

  const fetchStats = useCallback(async () => {
    try {
      const res = await trackingService.getTrackingEvents(undefined, 200, 'desc');
      setEvents(res.data ?? []);
    } catch {
      // stats are non-critical
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchStats();
    setEditUsername(user?.username ?? '');
    setEditEmail(user?.email ?? '');
  }, [fetchStats, user]);

  const unique = Object.values(
    events.reduce<Record<number, TrackingEvent>>((acc, ev) => {
      if (!acc[ev.mediaId] || new Date(ev.eventDate) > new Date(acc[ev.mediaId].eventDate)) {
        acc[ev.mediaId] = ev;
      }
      return acc;
    }, {})
  );

  const stats = {
    books:  unique.filter((e) => e.media?.type === 'Book').length,
    films:  unique.filter((e) => e.media?.type === 'Film').length,
    series: unique.filter((e) => e.media?.type === 'Serie').length,
    comics: unique.filter((e) => e.media?.type === 'Comic').length,
    total:  events.length,
  };

  const memberSince = (() => {
    if (events.length === 0) return 'N/A';
    const earliest = events.reduce((min, e) =>
      new Date(e.eventDate) < new Date(min.eventDate) ? e : min
    );
    return new Date(earliest.eventDate).toLocaleDateString('en-US', { month: 'long', year: 'numeric' });
  })();

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setSaveMsg(null);
    try {
      await apiClient.put('/user', { username: editUsername, email: editEmail }, true, false);
      setSaveMsg('Profile updated successfully.');
    } catch (err) {
      setSaveMsg(err instanceof Error ? err.message : 'Failed to update profile.');
    } finally {
      setSaving(false);
    }
  };

  const handleLogout = () => { logout(); router.push('/'); };

  const handleDelete = async () => {
    if (!deleteConfirm) { setDeleteConfirm(true); return; }
    try {
      await apiClient.delete('/user', true, false);
      logout();
      router.push('/');
    } catch (err) {
      alert(err instanceof Error ? err.message : 'Failed to delete account.');
    }
  };

  return (
    <div className="p-6 lg:p-8 max-w-3xl mx-auto">
      <div className="mb-8">
        <h1 className="font-heading text-2xl font-semibold">Profile</h1>
        <p className="text-muted-foreground text-sm mt-1">Manage your account and view your stats.</p>
      </div>

      {/* Identity card */}
      <div className="tt-card p-6 mb-6">
        <div className="flex items-start gap-5">
          <Avatar username={user?.username ?? 'U'} />
          <div className="flex-1 min-w-0">
            <h2 className="font-heading text-xl font-semibold">{user?.username}</h2>
            <p className="text-muted-foreground text-sm mt-0.5">{user?.email}</p>
            <div className="flex items-center gap-2 mt-3 text-xs text-muted-foreground/70">
              <Calendar className="w-3.5 h-3.5" />
              <span>{loading ? 'Loading...' : `First tracked: ${memberSince}`}</span>
              <span className="mx-1">·</span>
              <Activity className="w-3.5 h-3.5" />
              <span>{stats.total} total events</span>
            </div>
          </div>
        </div>
      </div>

      {/* Stats */}
      <div className="mb-6">
        <h3 className="text-[11px] font-semibold text-muted-foreground/60 uppercase tracking-widest mb-3">
          Your Library
        </h3>
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
          <StatPill label="Books"  value={stats.books}  icon={BookOpen}   colorClass="text-[oklch(0.65_0.13_65)]"  bgClass="bg-[oklch(0.65_0.13_65)]/10" />
          <StatPill label="Films"  value={stats.films}  icon={Film}       colorClass="text-[oklch(0.55_0.09_5)]"   bgClass="bg-[oklch(0.55_0.09_5)]/10" />
          <StatPill label="Series" value={stats.series} icon={Tv}         colorClass="text-[oklch(0.52_0.09_152)]" bgClass="bg-[oklch(0.52_0.09_152)]/10" />
          <StatPill label="Comics" value={stats.comics} icon={BookMarked} colorClass="text-[oklch(0.52_0.10_295)]" bgClass="bg-[oklch(0.52_0.10_295)]/10" />
        </div>
      </div>

      {/* Edit profile */}
      <div className="tt-card p-6 mb-6">
        <h3 className="font-heading font-semibold text-lg mb-5 flex items-center gap-2">
          <User className="w-4 h-4 text-muted-foreground" />
          Edit Profile
        </h3>
        <form onSubmit={handleSave} className="space-y-4">
          <div>
            <label className="block text-xs font-medium text-muted-foreground mb-1.5">Username</label>
            <div className="relative">
              <User className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground/50" />
              <input
                type="text"
                value={editUsername}
                onChange={(e) => setEditUsername(e.target.value)}
                className="tt-input pl-10 pr-4 py-2.5"
                placeholder="Your username"
              />
            </div>
          </div>
          <div>
            <label className="block text-xs font-medium text-muted-foreground mb-1.5">Email</label>
            <div className="relative">
              <Mail className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground/50" />
              <input
                type="email"
                value={editEmail}
                onChange={(e) => setEditEmail(e.target.value)}
                className="tt-input pl-10 pr-4 py-2.5"
                placeholder="your@email.com"
              />
            </div>
          </div>
          {saveMsg && (
            <p className={`text-sm ${saveMsg.includes('success') ? 'text-[oklch(0.52_0.09_152)]' : 'text-destructive'}`}>
              {saveMsg}
            </p>
          )}
          <button
            type="submit"
            disabled={saving}
            className="flex items-center gap-2 px-5 py-2.5 bg-primary text-primary-foreground font-medium rounded-xl text-sm hover:bg-primary/90 transition-colors disabled:opacity-50 cursor-pointer"
          >
            <Save className="w-4 h-4" />
            {saving ? 'Saving…' : 'Save Changes'}
          </button>
        </form>
      </div>

      {/* Actions */}
      <div className="tt-card p-6">
        <h3 className="font-heading font-semibold text-lg mb-5">Account Actions</h3>
        <div className="space-y-3">
          <button
            onClick={handleLogout}
            className="w-full flex items-center gap-3 px-4 py-3 rounded-xl bg-secondary/50 border border-border text-foreground hover:bg-secondary transition-colors text-sm font-medium cursor-pointer"
          >
            <LogOut className="w-4 h-4" />
            Sign out of TaleTrack
          </button>

          <div className="border-t border-border pt-3">
            <p className="text-xs text-muted-foreground/60 mb-3">
              Danger zone — these actions cannot be undone.
            </p>
            <button
              onClick={handleDelete}
              className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl border text-sm font-medium transition-colors cursor-pointer ${
                deleteConfirm
                  ? 'bg-destructive/20 border-destructive/30 text-destructive hover:bg-destructive/30'
                  : 'bg-secondary/30 border-destructive/20 text-destructive hover:bg-destructive/10 hover:border-destructive/30'
              }`}
            >
              <Trash2 className="w-4 h-4" />
              {deleteConfirm ? 'Click again to confirm deletion' : 'Delete account'}
            </button>
            {deleteConfirm && (
              <button
                onClick={() => setDeleteConfirm(false)}
                className="mt-2 text-xs text-muted-foreground hover:text-foreground transition-colors cursor-pointer"
              >
                Cancel
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
