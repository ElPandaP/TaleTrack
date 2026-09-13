'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import {
  User, Mail, BookOpen, Film, Tv, Activity, Calendar, LogOut, Trash2, Save,
} from 'lucide-react';
import { UserAvatar } from '@/components/media/user-avatar';
import AvatarUpload, { AVATAR_MAX_BYTES } from '@/components/profile/avatar-upload';
import { Button } from '@/components/ui/button';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from '@/components/ui/dialog';
import { useAuth, notifyAuthChange } from '@/lib/auth-context';
import { apiClient } from '@/lib/api/client';
import { userService, authService } from '@/lib/api/services';
import { useI18n } from '@/lib/i18n';
import { cn } from '@/lib/utils';
import type { UserProfile, YearlyStats, FeedPrivacy } from '@/lib/types';

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
  const router = useRouter();

  const [username, setUsername] = useState(profile.username);
  const [email, setEmail] = useState(profile.email);
  const [privacy, setPrivacy] = useState<FeedPrivacy>(profile.privacy);

  // Avatar changes are staged locally and only sent to the server on "Save changes".
  const [avatarFile, setAvatarFile] = useState<File | null>(null);
  const [avatarPreviewUrl, setAvatarPreviewUrl] = useState<string | null>(null);
  const [avatarRemoved, setAvatarRemoved] = useState(false);
  const [avatarError, setAvatarError] = useState<string | null>(null);

  useEffect(() => () => {
    if (avatarPreviewUrl) URL.revokeObjectURL(avatarPreviewUrl);
  }, [avatarPreviewUrl]);

  const displayAvatarUrl = avatarFile
    ? avatarPreviewUrl
    : avatarRemoved
      ? null
      : (profile.avatarUrl ?? null);
  const avatarDirty = avatarFile != null || avatarRemoved;

  const handleAvatarSelect = (file: File) => {
    setAvatarError(null);
    if (!file.type.startsWith('image/')) {
      setAvatarError(t('profile.avatar.invalidType'));
      return;
    }
    if (file.size > AVATAR_MAX_BYTES) {
      setAvatarError(t('profile.avatar.tooLarge'));
      return;
    }
    if (avatarPreviewUrl) URL.revokeObjectURL(avatarPreviewUrl);
    setAvatarPreviewUrl(URL.createObjectURL(file));
    setAvatarFile(file);
    setAvatarRemoved(false);
  };

  const handleAvatarRemove = () => {
    setAvatarError(null);
    if (avatarPreviewUrl) URL.revokeObjectURL(avatarPreviewUrl);
    setAvatarPreviewUrl(null);
    setAvatarFile(null);
    setAvatarRemoved(true);
  };

  const [saving, setSaving] = useState(false);
  const [saveMsg, setSaveMsg] = useState<{ ok: boolean; text: string } | null>(null);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [deleteRequested, setDeleteRequested] = useState(false);

  const memberSince = new Date(profile.createdAt).toLocaleDateString(locale, {
    month: 'long',
    year: 'numeric',
  });

  const setFlag = (k: keyof FeedPrivacy, v: boolean) => setPrivacy((p) => ({ ...p, [k]: v }));

  const isDirty =
    username !== profile.username ||
    email !== profile.email ||
    JSON.stringify(privacy) !== JSON.stringify(profile.privacy) ||
    avatarDirty;

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setSaveMsg(null);
    try {
      if (avatarFile) {
        await userService.uploadAvatar(avatarFile);
      } else if (avatarRemoved) {
        await userService.removeAvatar();
      }

      const res = await userService.updateProfile(profile.id, { username, email, privacy });
      // The access token carries username/email as claims — swap it in so the header/menu
      // and everything else driven by useAuth() stops showing the pre-edit values.
      if (res.token) {
        apiClient.setToken(res.token);
        notifyAuthChange();
      }

      if (avatarPreviewUrl) URL.revokeObjectURL(avatarPreviewUrl);
      setAvatarFile(null);
      setAvatarPreviewUrl(null);
      setAvatarRemoved(false);

      setSaveMsg({ ok: true, text: t('profile.saved') });
      router.refresh();
    } catch {
      setSaveMsg({ ok: false, text: t('profile.saveError') });
    } finally {
      setSaving(false);
    }
  };

  const handleLogout = () => {
    logout();
    window.location.assign('/');
  };

  const handleDelete = async () => {
    setDeleting(true);
    try {
      await authService.requestAccountDeletion(locale);
      setDeleteRequested(true);
      setDeleteDialogOpen(false);
    } catch {
      alert(t('profile.deleteError'));
    } finally {
      setDeleting(false);
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
        <UserAvatar username={username} avatarUrl={displayAvatarUrl} size="xl" />
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

      <div className="mb-6">
        <AvatarUpload
          displayUrl={displayAvatarUrl}
          username={username}
          pending={avatarDirty}
          disabled={saving}
          error={avatarError}
          onSelectFile={handleAvatarSelect}
          onRemove={handleAvatarRemove}
        />
      </div>

      <form onSubmit={handleSave} className="space-y-6">
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
        <div className="flex justify-end">
          <Button type="submit" disabled={saving || !isDirty} className="h-auto px-5 py-2.5">
            <Save className="size-4" />
            {saving ? t('profile.saving') : t('profile.save')}
          </Button>
        </div>
      </form>

      {/* Account actions */}
      <div className="tt-card mt-6 p-6">
        <h3 className="mb-5 font-heading text-lg font-semibold">{t('profile.account')}</h3>
        <Button
          variant="secondary"
          onClick={handleLogout}
          className="h-auto w-full justify-start gap-3 px-4 py-3"
        >
          <LogOut className="size-4" />
          {t('profile.signOut')}
        </Button>

        <div className="mt-3 border-t border-border pt-3">
          <p className="mb-3 text-xs text-muted-foreground/60">{t('profile.danger')}</p>
          {deleteRequested ? (
            <p className="rounded-xl border border-primary/20 bg-primary/10 px-4 py-3 text-sm text-foreground">
              {t('profile.deleteEmailSent')}
            </p>
          ) : (
            <Button
              variant="destructive"
              onClick={() => setDeleteDialogOpen(true)}
              className="h-auto w-full justify-start gap-3 border-destructive/20 bg-secondary/30 px-4 py-3 hover:border-destructive/30"
            >
              <Trash2 className="size-4" />
              {t('profile.delete')}
            </Button>
          )}
        </div>
      </div>

      <Dialog open={deleteDialogOpen} onOpenChange={setDeleteDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('profile.deleteConfirmTitle')}</DialogTitle>
            <DialogDescription>{t('profile.deleteConfirmBody')}</DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteDialogOpen(false)}>
              {t('profile.cancel')}
            </Button>
            <Button
              variant="destructive"
              onClick={handleDelete}
              disabled={deleting}
              className="bg-destructive text-white hover:bg-destructive/90"
            >
              {t('profile.deleteConfirmAction')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
