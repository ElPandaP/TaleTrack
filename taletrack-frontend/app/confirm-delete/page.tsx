'use client';

import { Suspense, useState } from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { Check, TriangleAlert } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { authService } from '@/lib/api/services';
import { ApiError } from '@/lib/api/client';
import { useI18n } from '@/lib/i18n';
import AuthShell from '@/app/_components/auth-shell';

function ConfirmDeleteInner() {
  const { t } = useI18n();
  const token = useSearchParams().get('token') ?? '';
  const [phase, setPhase] = useState<'idle' | 'working' | 'done' | 'error'>('idle');
  const [error, setError] = useState<string | null>(null);

  const run = async () => {
    setPhase('working');
    try {
      await authService.confirmAccountDeletion(token);
      setPhase('done');
    } catch (err) {
      const code = err instanceof ApiError ? err.code : undefined;
      setError(t(code === 'invalid_or_expired' ? 'auth.confirmDelete.expired' : 'auth.confirmDelete.error'));
      setPhase('error');
    }
  };

  if (!token) {
    return <p className="text-sm text-destructive">{t('auth.confirmDelete.noToken')}</p>;
  }

  if (phase === 'done') {
    return (
      <div className="flex flex-col items-center gap-3 py-2 text-center">
        <span className="flex size-10 items-center justify-center rounded-full bg-primary/15 text-primary">
          <Check className="size-5" />
        </span>
        <h1 className="font-heading text-xl font-semibold">{t('auth.confirmDelete.doneTitle')}</h1>
        <p className="text-sm text-muted-foreground">{t('auth.confirmDelete.doneBody')}</p>
        <Link href="/" className="mt-2 text-sm text-primary hover:underline">{t('auth.confirmDelete.home')}</Link>
      </div>
    );
  }

  return (
    <>
      <div className="mb-3 flex items-center gap-2 text-destructive">
        <TriangleAlert className="size-5" />
        <h1 className="font-heading text-xl font-semibold">{t('auth.confirmDelete.title')}</h1>
      </div>
      <p className="mb-6 text-sm text-muted-foreground">{t('auth.confirmDelete.body')}</p>
      {error && <p className="mb-4 text-sm text-destructive">{error}</p>}
      <Button
        type="button"
        variant="destructive"
        onClick={run}
        disabled={phase === 'working'}
        className="h-auto w-full bg-destructive py-3 text-white hover:bg-destructive/90"
      >
        {phase === 'working' ? t('auth.confirmDelete.working') : t('auth.confirmDelete.confirm')}
      </Button>
      <Link href="/" className="mt-4 inline-block text-sm text-muted-foreground hover:text-foreground">
        {t('auth.confirmDelete.cancel')}
      </Link>
    </>
  );
}

export default function ConfirmDeletePage() {
  return (
    <AuthShell>
      <Suspense fallback={<div className="py-6 text-center text-sm text-muted-foreground">…</div>}>
        <ConfirmDeleteInner />
      </Suspense>
    </AuthShell>
  );
}
