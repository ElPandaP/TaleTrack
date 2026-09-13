'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Leaf, Check, ArrowRight } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { API_CONFIG } from '@/lib/api/config';
import { sessionsService } from '@/lib/api/services';
import { useAuth } from '@/lib/auth-context';
import { useT } from '@/lib/i18n';
import LocaleToggle from '@/components/layout/locale-toggle';

// Minimal shape of the API the extension's manifest.json (externally_connectable)
// grants this origin — no @types/chrome dependency needed for this one call.
interface ExternalRuntime {
  sendMessage: (extensionId: string, message: unknown, callback: (response: unknown) => void) => void;
  lastError?: { message?: string };
}

function getExtensionRuntime(): ExternalRuntime | null {
  const w = window as unknown as { chrome?: { runtime?: ExternalRuntime } };
  return w.chrome?.runtime ?? null;
}

function Shell({ children }: { children: React.ReactNode }) {
  return (
    <div className="relative flex min-h-screen items-center justify-center overflow-hidden bg-background px-4">
      <div className="absolute top-4 right-4 z-20">
        <LocaleToggle />
      </div>
      <div className="pointer-events-none absolute top-0 left-1/4 h-96 w-96 rounded-full bg-primary/8 blur-3xl" />
      <div className="relative z-10 w-full max-w-md">
        <div className="mb-8 flex items-center justify-center gap-2.5">
          <div className="flex size-9 items-center justify-center rounded-xl border border-primary/25 bg-primary/15">
            <Leaf className="size-4.5 text-primary" />
          </div>
          <span className="font-heading text-lg font-semibold tracking-tight">TaleTrack</span>
        </div>
        <div className="tt-card p-8">{children}</div>
      </div>
    </div>
  );
}

export default function ExtensionAuthPage() {
  const router = useRouter();
  const t = useT();
  const { isAuthenticated, loading, user } = useAuth();

  const [phase, setPhase] = useState<'idle' | 'working' | 'done' | 'error' | 'no-extension'>('idle');

  // Bounce anonymous visitors through login, then straight back here.
  useEffect(() => {
    if (loading || isAuthenticated) return;
    router.replace(`/login?next=${encodeURIComponent('/extension-auth')}`);
  }, [loading, isAuthenticated, router]);

  const authorize = async () => {
    const runtime = getExtensionRuntime();
    if (!runtime) {
      setPhase('no-extension');
      return;
    }

    setPhase('working');
    try {
      const { token, refreshToken, expiresIn } = await sessionsService.extensionGrant();
      await new Promise<void>((resolve, reject) => {
        runtime.sendMessage(
          API_CONFIG.netflixExtensionId,
          { type: 'TALETRACK_AUTH', access: token, refresh: refreshToken, expiresIn },
          (response) => {
            const ok = !runtime.lastError && (response as { ok?: boolean } | undefined)?.ok;
            if (ok) resolve();
            else reject(new Error(runtime.lastError?.message ?? 'extension-rejected'));
          },
        );
      });
      setPhase('done');
    } catch {
      setPhase('error');
    }
  };

  if (loading || !isAuthenticated) {
    return (
      <Shell>
        <div className="flex justify-center py-6">
          <div className="size-5 animate-spin rounded-full border-2 border-primary/30 border-t-primary" />
        </div>
      </Shell>
    );
  }

  if (phase === 'done') {
    return (
      <Shell>
        <div className="flex flex-col items-center gap-3 py-4 text-center">
          <span className="flex size-10 items-center justify-center rounded-full bg-primary/15 text-primary">
            <Check className="size-5" />
          </span>
          <p className="text-sm text-muted-foreground">{t('extAuth.done')}</p>
        </div>
      </Shell>
    );
  }

  return (
    <Shell>
      <h1 className="font-heading text-2xl font-semibold">{t('extAuth.title')}</h1>
      <p className="mt-1 mb-6 text-sm text-muted-foreground">{t('extAuth.subtitle')}</p>

      <div className="mb-6 rounded-xl border border-border bg-secondary/20 p-3">
        <p className="text-[11px] font-semibold tracking-widest text-muted-foreground/60 uppercase">
          {t('extAuth.account')}
        </p>
        <p className="mt-0.5 truncate text-sm font-medium text-foreground">
          {user?.username} · {user?.email}
        </p>
      </div>

      {phase === 'error' && <p className="mb-4 text-sm text-destructive">{t('extAuth.error')}</p>}
      {phase === 'no-extension' && (
        <p className="mb-4 text-sm text-destructive">{t('extAuth.extensionNotFound')}</p>
      )}

      <Button type="button" onClick={authorize} disabled={phase === 'working'} className="h-auto w-full py-3">
        {phase === 'working' ? (
          <Spinner />
        ) : (
          <>
            {t('extAuth.authorize')} <ArrowRight className="size-4" />
          </>
        )}
      </Button>
    </Shell>
  );
}
