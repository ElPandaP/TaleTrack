'use client';

import { Suspense, useState } from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { Check, TriangleAlert } from 'lucide-react';
import { authService } from '@/lib/api/services';
import { ApiError } from '@/lib/api/client';
import { useI18n } from '@/lib/i18n';
import AuthShell from '@/app/_components/auth-shell';

function RevokeSignupInner() {
  const { t } = useI18n();
  const token = useSearchParams().get('token') ?? '';
  const [phase, setPhase] = useState<'idle' | 'working' | 'done' | 'error'>('idle');
  const [error, setError] = useState<string | null>(null);

  const run = async () => {
    setPhase('working');
    try {
      await authService.revokeSignup(token);
      setPhase('done');
    } catch (err) {
      const code = err instanceof ApiError ? err.code : undefined;
      setError(t(code === 'invalid_or_expired' ? 'auth.revokeSignup.expired' : 'auth.revokeSignup.error'));
      setPhase('error');
    }
  };

  if (!token) {
    return <p className="text-sm text-destructive">{t('auth.revokeSignup.noToken')}</p>;
  }

  if (phase === 'done') {
    return (
      <div className="flex flex-col items-center gap-3 py-2 text-center">
        <span className="flex size-10 items-center justify-center rounded-full bg-primary/15 text-primary">
          <Check className="size-5" />
        </span>
        <h1 className="font-heading text-xl font-semibold">{t('auth.revokeSignup.doneTitle')}</h1>
        <p className="text-sm text-muted-foreground">{t('auth.revokeSignup.doneBody')}</p>
        <Link href="/" className="mt-2 text-sm text-primary hover:underline">{t('auth.revokeSignup.home')}</Link>
      </div>
    );
  }

  return (
    <>
      <div className="mb-3 flex items-center gap-2 text-destructive">
        <TriangleAlert className="size-5" />
        <h1 className="font-heading text-xl font-semibold">{t('auth.revokeSignup.title')}</h1>
      </div>
      <p className="mb-6 text-sm text-muted-foreground">{t('auth.revokeSignup.body')}</p>
      {error && <p className="mb-4 text-sm text-destructive">{error}</p>}
      <button
        type="button"
        onClick={run}
        disabled={phase === 'working'}
        className="w-full rounded-xl bg-destructive py-3 text-sm font-medium text-white transition-opacity hover:opacity-90 disabled:opacity-50"
      >
        {phase === 'working' ? t('auth.revokeSignup.working') : t('auth.revokeSignup.confirm')}
      </button>
      <Link href="/" className="mt-4 inline-block text-sm text-muted-foreground hover:text-foreground">
        {t('auth.revokeSignup.keep')}
      </Link>
    </>
  );
}

export default function RevokeSignupPage() {
  return (
    <AuthShell>
      <Suspense fallback={<div className="py-6 text-center text-sm text-muted-foreground">…</div>}>
        <RevokeSignupInner />
      </Suspense>
    </AuthShell>
  );
}
