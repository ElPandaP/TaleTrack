'use client';

import { useState } from 'react';
import {
  User, Mail, BookOpen, Film, Tv, Activity, Calendar, LogOut, Trash2, Save, Check,
} from 'lucide-react';
import { UserAvatar } from '@/components/media/user-avatar';
import ConnectionsCard from '@/components/profile/connections-card';
import { useAuth } from '@/lib/auth-context';
import { userService } from '@/lib/api/services';
import { useI18n } from '@/lib/i18n';
import { cn } from '@/lib/utils';
import type { UserProfile, YearlyStats, FeedPrivacy } from '@/lib/types';

const AVATAR_SEEDS = ['Aneka', 'Felix', 'Luna', 'Milo', 'Nova', 'Sage'];
const avatarUrlFor = (seed: string) =>
  `https://api.dicebear.com/9.x/notionists/svg?seed=${encodeURIComponent(seed)}`;

const PRIVACY_GROUPS = [
  { key: 'book', labelKey: 'typePlural.Book', progress: 'bookProgress', reviews: 'bookReviews' },
  { key: 'movie', labelKey: 'typePlural.Movie', progress: 'movieProgress', reviews: 'movieReviews' },
  { key: 'series', labelKey: 'typePlural.Series', progress: 'seriesProgress', reviews: 'seriesReviews' },
] as const;

function StatPill({
  label, value, icon: Icon, colorClass, bgClass,
}: {
  label: string; value: number;
  icon: React.ComponentType<{ className?: string }>;
  colorClass: string; bgClass: string;
}) {
  return (
    <div className={`flex items-center gap-3 rounded-xl border border-border px-4 py-3 ${bgClass}`}>
      <Icon className={`size-4 ${colorClass}`} />
      <div>
        <p className="text-xl font-bold text-foreground">{value}</p>
        <p className={`text-xs ${colorClass} opacity-80`}>{label}</p>
      </div>
    </div>
  );
}

function Toggle({
  checked, onChange, label,
}: {
  checked: boolean; onChange: (v: boolean) => void; label: string;
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      onClick={() => onChange(!checked)}
      className="flex items-center gap-2 text-sm"
    >
      <span
        className={cn(
          'relative inline-block h-5 w-9 shrink-0 rounded-full transition-colors',
          checked ? 'bg-primary' : 'bg-input',
        )}
      >
        <span
          className={cn(
            'absolute top-0.5 left-0.5 size-4 rounded-full bg-card shadow transition-transform',
            checked && 'translate-x-4',
          )}
        />
      </span>
      <span className="text-muted-foreground">{label}</span>
    </button>
  );
}

export default function ProfileClient({
  profile,
  stats,
}: {
  profile: UserProfile;
  stats: YearlyStats | null;
}) {
  const { logout } = useAuth();
  const { t, locale } = useI18n();

  const [username, setUsername] = useState(profile.username);
  const [email, setEmail] = useState(profile.email);
  const [avatarUrl, setAvatarUrl] = useState(profile.avatarUrl ?? '');
  const [privacy, setPrivacy] = useState<FeedPrivacy>(profile.privacy);

  const [saving, setSaving] = useState(false);
  const [saveMsg, setSaveMsg] = useState<{ ok: boolean; text: string } | null>(null);
  const [deleteConfirm, setDeleteConfirm] = useState(false);

  const memberSince = new Date(profile.createdAt).toLocaleDateString(locale, {
    month: 'long',
    year: 'numeric',
  });

  const setFlag = (k: keyof FeedPrivacy, v: boolean) => setPrivacy((p) => ({ ...p, [k]: v }));

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setSaveMsg(null);
    try {
      await userService.updateProfile(profile.id, { username, email, avatarUrl, privacy });
      setSaveMsg({ ok: true, text: t('profile.saved') });
    } catch (err) {
      setSaveMsg({ ok: false, text: err instanceof Error ? err.message : t('profile.saveError') });
    } finally {
      setSaving(false);
    }
  };

  const handleLogout = () => {
    logout();
    window.location.assign('/');
  };

  const handleDelete = async () => {
    if (!deleteConfirm) {
      setDeleteConfirm(true);
      return;
    }
    try {
      await userService.deleteAccount(profile.id);
      logout();
      window.location.assign('/');
    } catch (err) {
      alert(err instanceof Error ? err.message : t('profile.deleteError'));
    }
  };

  return (
    <div className="mx-auto max-w-2xl">
      <div className="mb-8">
        <h1 className="font-heading text-2xl font-semibold">{t('profile.title')}</h1>
        <p className="mt-1 text-sm text-muted-foreground">{t('profile.subtitle')}</p>
      </div>

      {/* Identity */}
      <div className="tt-card mb-6 flex items-start gap-5 p-6">
        <UserAvatar username={username} avatarUrl={avatarUrl} size="xl" />
        <div className="min-w-0 flex-1">
          <h2 className="font-heading text-xl font-semibold">@{username}</h2>
          <p className="mt-0.5 text-sm text-muted-foreground">{email}</p>
          <div className="mt-3 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted-foreground/70">
            <Calendar className="size-3.5" />
            <span>{t('profile.firstTracked', { date: memberSince })}</span>
            <span className="mx-1">·</span>
            <Activity className="size-3.5" />
            <span>{t('profile.totalEvents', { count: stats?.total ?? 0 })}</span>
          </div>
        </div>
      </div>

      {/* Stats */}
      <div className="mb-6">
        <h3 className="mb-3 text-[11px] font-semibold tracking-widest text-muted-foreground/60 uppercase">
          {t('profile.library')}
        </h3>
        <div className="grid grid-cols-3 gap-3">
          <StatPill label={t('home.stats.books')} value={stats?.byType.book ?? 0} icon={BookOpen} colorClass="text-chart-1" bgClass="bg-chart-1/10" />
          <StatPill label={t('home.stats.movies')} value={stats?.byType.movie ?? 0} icon={Film} colorClass="text-chart-2" bgClass="bg-chart-2/10" />
          <StatPill label={t('home.stats.series')} value={stats?.byType.series ?? 0} icon={Tv} colorClass="text-chart-3" bgClass="bg-chart-3/10" />
        </div>
      </div>

      <form onSubmit={handleSave} className="space-y-6">
        {/* Avatar */}
        <div className="tt-card p-6">
          <h3 className="font-heading text-lg font-semibold">{t('profile.avatar')}</h3>
          <p className="mt-1 mb-4 text-xs text-muted-foreground">{t('profile.avatar.pick')}</p>

          <div className="flex flex-wrap gap-2">
            {AVATAR_SEEDS.map((seed) => {
              const url = avatarUrlFor(seed);
              const active = avatarUrl === url;
              return (
                <button
                  key={seed}
                  type="button"
                  onClick={() => setAvatarUrl(url)}
                  className={cn(
                    'relative rounded-full border-2 transition-colors',
                    active ? 'border-primary' : 'border-transparent hover:border-border',
                  )}
                >
                  <UserAvatar username={seed} avatarUrl={url} size="lg" />
                  {active && (
                    <span className="absolute -right-0.5 -bottom-0.5 flex size-4 items-center justify-center rounded-full bg-primary text-primary-foreground">
                      <Check aria-hidden="true" className="size-3" />
                    </span>
                  )}
                </button>
              );
            })}
          </div>

          <div className="mt-4 flex items-center gap-2">
            <input
              type="url"
              value={avatarUrl}
              onChange={(e) => setAvatarUrl(e.target.value)}
              placeholder={t('profile.avatar.customUrl')}
              className="tt-input px-3 py-2 text-sm"
            />
            {avatarUrl && (
              <button
                type="button"
                onClick={() => setAvatarUrl('')}
                className="shrink-0 text-xs text-muted-foreground hover:text-destructive"
              >
                {t('profile.avatar.remove')}
              </button>
            )}
          </div>
        </div>

        {/* Edit fields */}
        <div className="tt-card p-6">
          <h3 className="mb-5 flex items-center gap-2 font-heading text-lg font-semibold">
            <User className="size-4 text-muted-foreground" />
            {t('profile.edit')}
          </h3>
          <div className="space-y-4">
            <div>
              <label className="mb-1.5 block text-xs font-medium text-muted-foreground">
                {t('auth.username')}
              </label>
              <div className="relative">
                <User className="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground/50" />
                <input
                  type="text"
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                  minLength={3}
                  maxLength={50}
                  className="tt-input py-2.5 pr-4 pl-10"
                />
              </div>
            </div>
            <div>
              <label className="mb-1.5 block text-xs font-medium text-muted-foreground">
                {t('auth.email')}
              </label>
              <div className="relative">
                <Mail className="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground/50" />
                <input
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  className="tt-input py-2.5 pr-4 pl-10"
                />
              </div>
            </div>
          </div>
        </div>

        {/* Privacy — per media type */}
        <div className="tt-card p-6">
          <h3 className="font-heading text-lg font-semibold">{t('profile.privacy')}</h3>
          <p className="mt-1 mb-4 text-xs text-muted-foreground">{t('profile.privacy.hint')}</p>
          <div className="space-y-4">
            {PRIVACY_GROUPS.map((g) => (
              <div key={g.key} className="rounded-xl border border-border bg-secondary/20 p-3">
                <p className="mb-2 text-sm font-medium text-foreground">{t(g.labelKey)}</p>
                <div className="flex flex-wrap gap-x-6 gap-y-2">
                  <Toggle
                    checked={privacy[g.progress]}
                    onChange={(v) => setFlag(g.progress, v)}
                    label={t('profile.privacy.progress')}
                  />
                  <Toggle
                    checked={privacy[g.reviews]}
                    onChange={(v) => setFlag(g.reviews, v)}
                    label={t('profile.privacy.reviews')}
                  />
                </div>
              </div>
            ))}
          </div>
        </div>

        {saveMsg && (
          <p className={cn('text-sm', saveMsg.ok ? 'text-primary' : 'text-destructive')}>{saveMsg.text}</p>
        )}
        <button
          type="submit"
          disabled={saving}
          className="flex items-center gap-2 rounded-xl bg-primary px-5 py-2.5 text-sm font-medium text-primary-foreground transition-colors hover:bg-primary/90 disabled:opacity-50"
        >
          <Save className="size-4" />
          {saving ? t('profile.saving') : t('profile.save')}
        </button>
      </form>

      {/* Connections / active sessions */}
      <div className="mt-6">
        <ConnectionsCard />
      </div>

      {/* Account actions */}
      <div className="tt-card mt-6 p-6">
        <h3 className="mb-5 font-heading text-lg font-semibold">{t('profile.account')}</h3>
        <button
          onClick={handleLogout}
          className="flex w-full items-center gap-3 rounded-xl border border-border bg-secondary/50 px-4 py-3 text-sm font-medium text-foreground transition-colors hover:bg-secondary"
        >
          <LogOut className="size-4" />
          {t('profile.signOut')}
        </button>

        <div className="mt-3 border-t border-border pt-3">
          <p className="mb-3 text-xs text-muted-foreground/60">{t('profile.danger')}</p>
          <button
            onClick={handleDelete}
            className={cn(
              'flex w-full items-center gap-3 rounded-xl border px-4 py-3 text-sm font-medium transition-colors',
              deleteConfirm
                ? 'border-destructive/30 bg-destructive/20 text-destructive hover:bg-destructive/30'
                : 'border-destructive/20 bg-secondary/30 text-destructive hover:border-destructive/30 hover:bg-destructive/10',
            )}
          >
            <Trash2 className="size-4" />
            {deleteConfirm ? t('profile.deleteConfirm') : t('profile.delete')}
          </button>
          {deleteConfirm && (
            <button
              onClick={() => setDeleteConfirm(false)}
              className="mt-2 text-xs text-muted-foreground hover:text-foreground"
            >
              {t('profile.cancel')}
            </button>
          )}
        </div>
      </div>
    </div>
  );
}
