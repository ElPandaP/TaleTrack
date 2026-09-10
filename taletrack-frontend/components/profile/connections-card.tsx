'use client';

import { useEffect, useState } from 'react';
import { Monitor, Puzzle, BookOpen, Smartphone, X } from 'lucide-react';
import { sessionsService, type Session } from '@/lib/api/services';
import { useI18n } from '@/lib/i18n';
import { cn } from '@/lib/utils';

function deviceIcon(device: string) {
  const d = device.toLowerCase();
  if (d.includes('extension')) return Puzzle;
  if (d.includes('koreader') || d.includes('kindle')) return BookOpen;
  if (d.includes('web')) return Monitor;
  return Smartphone;
}

export default function ConnectionsCard() {
  const { t, locale } = useI18n();
  const [sessions, setSessions] = useState<Session[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [revoking, setRevoking] = useState<number | null>(null);

  useEffect(() => {
    let alive = true;
    sessionsService
      .list()
      .then((s) => alive && setSessions(s))
      .catch(() => alive && setError(t('profile.connections.loadError')));
    return () => {
      alive = false;
    };
    // t is stable enough for a mount-only fetch
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const fmt = (iso: string) =>
    new Date(iso).toLocaleDateString(locale, { day: 'numeric', month: 'long', year: 'numeric' });

  const handleRevoke = async (id: number) => {
    setRevoking(id);
    setError(null);
    try {
      await sessionsService.revoke(id);
      setSessions((prev) => prev?.filter((s) => s.id !== id) ?? null);
    } catch {
      setError(t('profile.connections.revokeError'));
    } finally {
      setRevoking(null);
    }
  };

  return (
    <div className="tt-card p-6">
      <h3 className="font-heading text-lg font-semibold">{t('profile.connections')}</h3>
      <p className="mt-1 mb-4 text-xs text-muted-foreground">{t('profile.connections.hint')}</p>

      {error && <p className="mb-3 text-sm text-destructive">{error}</p>}

      {sessions === null && !error && (
        <div className="space-y-2">
          {[0, 1].map((i) => (
            <div key={i} className="h-14 animate-pulse rounded-xl bg-secondary/40" />
          ))}
        </div>
      )}

      {sessions?.length === 0 && (
        <p className="text-sm text-muted-foreground">{t('profile.connections.empty')}</p>
      )}

      {sessions && sessions.length > 0 && (
        <ul className="space-y-2">
          {sessions.map((s) => {
            const Icon = deviceIcon(s.device);
            return (
              <li
                key={s.id}
                className="flex items-center gap-3 rounded-xl border border-border bg-secondary/20 p-3"
              >
                <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary">
                  <Icon className="size-4" />
                </span>
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium text-foreground">{s.device}</p>
                  <p className="truncate text-xs text-muted-foreground">
                    {t('profile.connections.lastUsed', { date: fmt(s.lastUsedAt) })}
                  </p>
                </div>
                <button
                  type="button"
                  onClick={() => handleRevoke(s.id)}
                  disabled={revoking === s.id}
                  className={cn(
                    'flex shrink-0 items-center gap-1.5 rounded-lg border border-border px-2.5 py-1.5',
                    'text-xs font-medium text-muted-foreground transition-colors',
                    'hover:border-destructive/30 hover:bg-destructive/10 hover:text-destructive',
                    'disabled:opacity-50',
                  )}
                >
                  <X className="size-3.5" />
                  {revoking === s.id
                    ? t('profile.connections.revoking')
                    : t('profile.connections.revoke')}
                </button>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
