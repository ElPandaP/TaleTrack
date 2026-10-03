'use client';

import { Suspense, useEffect, useState, useSyncExternalStore } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { Check, ArrowRight } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { API_CONFIG } from '@/lib/api/config';
import { sessionsService } from '@/lib/api/services';
import { useAuth } from '@/lib/auth-context';
import { useT } from '@/lib/i18n';
import AuthShell from '@/components/auth/AuthShell';

// Minimal shape of the extension messaging API that the extension's manifest
// (`externally_connectable`) exposes to this origin, typed here to avoid a dependency
// on @types/chrome for this one call.
interface ExternalRuntime {
  sendMessage: (extensionId: string, message: unknown, callback: (response: unknown) => void) => void;
  lastError?: { message?: string };
}

/** The browser's `chrome.runtime`, or `null` when the browser exposes none. */
function getExtensionRuntime(): ExternalRuntime | null {
  const w = window as unknown as { chrome?: { runtime?: ExternalRuntime } };
  return w.chrome?.runtime ?? null;
}

/** Chrome extension ids are 32 letters from a to p. */
const EXTENSION_ID_PATTERN = /^[a-p]{32}$/;

/** Longest session label the backend accepts. */
const MAX_NAME_LENGTH = 60;

/** Which extension the page is authorizing, read from the `ext` and `name` query parameters. */
type Target =
  | { kind: 'netflix'; extensionId: string }
  | { kind: 'other'; extensionId: string; name: string }
  | { kind: 'invalid' };

/**
 * Without `ext` the target is TaleTrack's own Netflix extension. Any other extension passes its
 * id in `ext` and the name to show (and to label its session with) in `name`; both are required.
 */
function readTarget(params: URLSearchParams): Target {
  const ext = params.get('ext');
  if (!ext || ext === API_CONFIG.netflixExtensionId) {
    return { kind: 'netflix', extensionId: API_CONFIG.netflixExtensionId };
  }
  const name = params.get('name')?.trim() ?? '';
  if (!EXTENSION_ID_PATTERN.test(ext) || !name || name.length > MAX_NAME_LENGTH) return { kind: 'invalid' };
  return { kind: 'other', extensionId: ext, name };
}

/**
 * `/extension-auth`: lets a signed-in user authorize a browser extension, TaleTrack's Netflix
 * extension by default or any other one named in the query string. It requests an extension
 * token pair from the backend and hands it to the extension through `chrome.runtime.sendMessage`.
 * Anonymous visitors are sent through login and back.
 */
export default function ExtensionAuthPage() {
  return (
    <Suspense fallback={null}>
      <ExtensionAuth />
    </Suspense>
  );
}

/** The page body; split out because reading the query string needs a Suspense boundary. */
function ExtensionAuth() {
  const router = useRouter();
  const t = useT();
  const { isAuthenticated, loading, user } = useAuth();
  const params = useSearchParams();
  const target = readTarget(params);

  const [phase, setPhase] = useState<'idle' | 'working' | 'done' | 'error' | 'no-extension'>('idle');

  // useAuth() cannot read localStorage on the server, so the first client render reports
  // isAuthenticated: false to match it, whatever the real session is. The redirect effect
  // below must not act on that placeholder render.
  const hasHydrated = useSyncExternalStore(
    () => () => {}, // never changes after mount, so there is nothing to subscribe to
    () => true, // real client render
    () => false, // server / hydration-matching render
  );

  // Bounce anonymous visitors through login, then straight back here.
  useEffect(() => {
    if (!hasHydrated || loading || isAuthenticated) return;
    const query = params.toString();
    const back = query ? `/extension-auth?${query}` : '/extension-auth';
    router.replace(`/login?next=${encodeURIComponent(back)}`);
  }, [hasHydrated, loading, isAuthenticated, router, params]);

  const authorize = async () => {
    if (target.kind === 'invalid') return;
    const runtime = getExtensionRuntime();
    if (!runtime) {
      setPhase('no-extension');
      return;
    }

    setPhase('working');
    let grantedRefresh: string | null = null;
    try {
      const { token, refreshToken, expiresIn } = await sessionsService.extensionGrant(
        target.kind === 'other' ? target.name : undefined,
      );
      grantedRefresh = refreshToken;
      await new Promise<void>((resolve, reject) => {
        runtime.sendMessage(
          target.extensionId,
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
      // The extension did not take the pair, so its session would be left behind unused.
      if (grantedRefresh) sessionsService.discardExtensionGrant(grantedRefresh);
      setPhase('error');
    }
  };

  if (loading || !isAuthenticated) {
    return (
      <AuthShell>
        <div className="flex justify-center py-6">
          <Spinner className="size-5 border-primary/30 border-t-primary" />
        </div>
      </AuthShell>
    );
  }

  if (phase === 'done') {
    return (
      <AuthShell>
        <div className="flex flex-col items-center gap-3 py-4 text-center">
          <span className="flex size-10 items-center justify-center rounded-full bg-primary/15 text-primary">
            <Check className="size-5" />
          </span>
          <p className="text-sm text-muted-foreground">{t('extAuth.done')}</p>
        </div>
      </AuthShell>
    );
  }

  if (target.kind === 'invalid') {
    return (
      <AuthShell>
        <h1 className="font-heading text-2xl font-semibold">{t('extAuth.invalidTitle')}</h1>
        <p className="mt-1 text-sm text-muted-foreground">{t('extAuth.invalidBody')}</p>
      </AuthShell>
    );
  }

  return (
    <AuthShell>
      <h1 className="font-heading text-2xl font-semibold">
        {target.kind === 'netflix' ? t('extAuth.title') : t('extAuth.titleOther', { name: target.name })}
      </h1>
      <p className="mt-1 mb-6 text-sm text-muted-foreground">
        {target.kind === 'netflix' ? t('extAuth.subtitle') : t('extAuth.subtitleOther', { name: target.name })}
      </p>

      <div className="mb-6 rounded-xl border border-border bg-secondary/20 p-3">
        <p className="text-[11px] font-semibold tracking-widest text-muted-foreground/60 uppercase">
          {t('extAuth.account')}
        </p>
        <p className="mt-0.5 truncate text-sm font-medium text-foreground">
          {user?.username} · {user?.email}
        </p>
      </div>

      <p className="mb-6 text-xs text-muted-foreground">{t('extAuth.warning')}</p>

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
    </AuthShell>
  );
}
